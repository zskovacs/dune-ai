using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;

namespace Dune.Engine.Turns;

/// <summary>Incremental original Imperium player turns over trusted setup state; stops before Combat.</summary>
public sealed class TurnEngine
{
    private readonly GameState _state;
    private readonly TurnRuleSet _rules;
    private readonly Dictionary<ContentId, TurnDiagnostic> _cardErrors = [];
    private readonly Dictionary<BoardSpaceId, TurnDiagnostic> _spaceErrors = [];
    private readonly Dictionary<ContentId, CardRule> _cards;
    private readonly Dictionary<BoardSpaceId, BoardRule> _spaces;
    private readonly Dictionary<ContentId, LeaderRule> _leaders;
    private readonly List<EffectExecutionFrame> _frames = [];
    private IEnumerable<PendingEffect> Pending => _frames.SelectMany(x => x.Remaining);
    private ImmutableArray<PendingEffect> CurrentPending => _frames.Count == 0 ? [] : _frames[^1].Remaining;
    private DecisionFrame? Decision => _frames.Count == 0 ? null : _frames[^1].Decision;
    private readonly EffectGraphValidator _graphValidator = new();
    private readonly Dictionary<ImmutableArray<Effect>, ImmutableArray<TurnDiagnostic>> _shapes = [];
    private readonly Dictionary<(ImmutableArray<Effect> Effects, int Owner, bool Card, bool Board, int Depth), TurnDiagnostic?> _support = [];
    private readonly HashSet<UsedSpace> _used = [];
    private readonly List<GameAction> _history = [];
    private readonly string _initialHash;
    private readonly int _cardCount;
    private readonly Dictionary<int, int> _troopTotals;
    private readonly string _rulesIdentity;
    private int _nextEffectId;
    private int _nextFrameId;
    private bool _revealing;
    private bool _deploying;
    private int _recruited;
    private int _oldGarrison;

    public TurnEngine(GameState state, TurnRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rules);
        if (state.Definition.Setup?.Id != "imperium.setup.standard.v1" ||
            state.Definition.Modules.Length != 1 || state.Definition.Modules[0].Id.Value != "base.imperium" ||
            state.Board.Definition.Id.Value != "imperium:board:main:v1" || state.Players.Count is not (3 or 4))
            throw new NotSupportedException("Turn execution supports only unchanged original Imperium Standard setup.");
        if (rules.Strength.TroopStrength < 0 || rules.Strength.SwordStrength < 0)
            throw new ArgumentException("Strength coefficients must be nonnegative.", nameof(rules));
        _state = state;
        foreach (var card in rules.Cards)
            if (card.Cost < 0 || OrEmpty(card.Icons).Any(x => !Enum.IsDefined(x)))
                _cardErrors[card.Id] = Content("card-shape", "Card cost or icon is invalid.");
        foreach (var space in rules.Spaces)
            if (!Enum.IsDefined(space.Icon) || space.Faction is { } faction && !Enum.IsDefined(faction) ||
                space.ControlReward is { } reward && !Enum.IsDefined(reward) || space.RequiredInfluence < 0 ||
                space.Capacity < 1 || ValidateCost(space.Cost) is not null ||
                OrEmpty(space.PlacementOptions).Any(x => x is null || ValidateCost(x.Cost) is not null))
                _spaceErrors[space.Id] = Content("space-shape", "Board cost, requirement, capacity or enum is invalid.");
        // Normalize optional immutable arrays and canonicalize definitions for stable replay identity.
        _rules = rules with
        {
            Cards = [.. rules.Cards.OrderBy(x => x.Id.Value, StringComparer.Ordinal)
                .Select(x => x with
                {
                    Icons = [.. OrEmpty(x.Icons).Where(Enum.IsDefined)], AgentEffects = Normalize(x.AgentEffects, definitionError: _cardErrors.GetValueOrDefault(x.Id)),
                    RevealEffects = Normalize(x.RevealEffects, definitionError: _cardErrors.GetValueOrDefault(x.Id)), AcquireEffects = Normalize(x.AcquireEffects, definitionError: _cardErrors.GetValueOrDefault(x.Id)),
                    Sources = OrEmpty(x.Sources),
                })],
            Spaces = [.. rules.Spaces.OrderBy(x => x.Id.Value, StringComparer.Ordinal).Select(x => x with
            {
                Icon = Enum.IsDefined(x.Icon) ? x.Icon : AgentIcon.Emperor,
                Faction = x.Faction is { } faction && Enum.IsDefined(faction) ? faction : null,
                ControlReward = x.ControlReward is { } reward && Enum.IsDefined(reward) ? reward : null,
                Effects = Normalize(x.Effects, definitionError: _spaceErrors.GetValueOrDefault(x.Id)), RevealEffects = Normalize(x.RevealEffects, definitionError: _spaceErrors.GetValueOrDefault(x.Id)),
                PlacementOptions = [.. OrEmpty(x.PlacementOptions).Where(o => o is not null).Select(o => o with { Effects = Normalize(o.Effects) })],
                Sources = OrEmpty(x.Sources),
            })],
            Leaders = [.. rules.Leaders.OrderBy(x => x.Id.Value, StringComparer.Ordinal)
                .Select(x => x with { SignetEffects = Normalize(x.SignetEffects) })],
        };
        _cards = _rules.Cards.ToDictionary(x => x.Id);
        _spaces = _rules.Spaces.ToDictionary(x => x.Id);
        _leaders = _rules.Leaders.ToDictionary(x => x.Id);
        _rulesIdentity = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(_rules, ConfigurationJson.Options))));
        _initialHash = state.CaptureTrustedSnapshot().Hash;
        _cardCount = state.Zones.Sum(x => x.Count);
        _troopTotals = state.Players.ToDictionary(x => x.Id,
            x => x.Units.TroopsInSupply + x.Units.TroopsInGarrison + x.Units.TroopsInConflict);
        AssertInvariants();
    }

    public int? CurrentPlayerId => _state.CurrentPlayerId;
    public int? DecisionOwnerId => _state.DecisionOwnerId;
    public GamePhase Phase => _state.Phase;

    public StepResult StartRound() => Step(new StartRoundAction());

    public TurnObservation Observe(int playerId)
    {
        var game = _state.Observe(playerId);
        // Only pending work owned by the viewer is visible. Effect payloads may contain private targets.
        var effects = CurrentPending.Where(x => x.Context.OwnerId == playerId)
            .Select(x => new ObservedPendingEffect(x.Id, playerId, x.Effect.GetType().Name)).ToImmutableArray();
        PendingDecisionSummary? decision = null;
        if (Decision is { } frame && frame.Context.OwnerId == playerId)
            decision = new(playerId, frame.Effect.GetType().Name, frame.Effect switch
            {
                OptionalPaymentEffect => ["Decline", "Pay"],
                ChoiceEffect choice => [.. choice.Options.Select(x => x.Label)],
                _ => throw new InvalidOperationException("Unknown pending decision."),
            });
        return new(game, effects, decision, LegalActions(playerId));
    }

    public ImmutableArray<GameAction> LegalActions(int playerId)
    {
        Player(playerId); // Validate viewer even when inactive.
        if (_state.Phase != GamePhase.PlayerTurns || _state.DecisionOwnerId != playerId)
            return [];
        return Candidates(playerId).Where(x => Validate(x) is null).ToImmutableArray();
    }

    public ImmutableArray<TurnDiagnostic> SupportDiagnostics(int playerId)
    {
        Player(playerId);
        if (_state.Phase != GamePhase.PlayerTurns || _state.DecisionOwnerId != playerId)
            return [];
        return Candidates(playerId).Select(Validate).Where(x => x is not null &&
                x.Status is StepStatus.Unsupported or StepStatus.ContentError)
            .Cast<TurnDiagnostic>().Distinct().ToImmutableArray();
    }

    public StepResult Step(GameAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        AssertInvariants();
        var diagnostic = Validate(action);
        if (diagnostic is not null) return new(diagnostic.Status, [diagnostic]);
        switch (action)
        {
            case StartRoundAction:
                BeginRound();
                break;
            case PlaceAgentAction place:
                Place(place);
                break;
            case RevealAction:
                Reveal();
                break;
            case ResolveEffectAction resolve:
                var work = Take(resolve.EffectId);
                Execute(work.Effect, work.Context);
                ContinueTurn();
                break;
            case ChooseOptionAction choose:
                Choose(choose.Option);
                ContinueTurn();
                break;
            case DeployAction deploy:
                var player = Player(deploy.PlayerId);
                player.Units = player.Units with
                {
                    TroopsInGarrison = player.Units.TroopsInGarrison - deploy.Troops,
                    TroopsInConflict = player.Units.TroopsInConflict + deploy.Troops,
                };
                _deploying = false;
                Resume(PopFrame().Continuation);
                break;
            case AcquireCardAction acquire:
                var source = _state.Zones.Single(x => x.Id == acquire.Source);
                var card = Find(source, acquire.Card)!;
                var rule = _cards[card.Definition];
                var buyer = Player(acquire.PlayerId);
                buyer.Temporary = buyer.Temporary with { Persuasion = buyer.Temporary.Persuasion - rule.Cost };
                Acquire(card, source, buyer, RootContext(buyer.Id, card.Id, card.Definition, null, EffectProvenance.Acquisition));
                ContinueTurn();
                break;
            case EndRevealAction:
                EndReveal();
                break;
            default:
                throw new InvalidOperationException("Validated unknown action.");
        }
        _history.Add(action);
        AssertInvariants();
        return new(StepStatus.Applied, []);
    }

    public TrustedTurnSnapshot CaptureTrustedSnapshot() => new(new("dune-turn-snapshot-v2", _rulesIdentity, _initialHash,
        _state.CaptureTrustedSnapshot().Data, [.. _frames], _nextFrameId, _revealing, _deploying,
        _recruited, _oldGarrison, _nextEffectId,
        [.. _used.OrderBy(x => x.PlayerId).ThenBy(x => x.Space.Value, StringComparer.Ordinal)], [.. _history]));

    public TurnReplay CaptureReplay() => new("dune-turn-replay-v2",
        typeof(TurnEngine).Assembly.GetName().Version!.ToString(),
        System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        _rulesIdentity, _state.Definition.Fingerprint, _state.ConfigurationIdentity, _state.ContentIdentity,
        _rules.SupportVersion, _state.Random.Checkpoint.Seed, _state.LeadersRandom.Checkpoint.Seed,
        _initialHash, [.. _history], CaptureTrustedSnapshot().Hash);

    public static TurnReplayResult Replay(TurnReplay replay, Func<GameState> initialStateFactory, TurnRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(initialStateFactory);
        var engine = new TurnEngine(initialStateFactory(), rules);
        var header = engine.CaptureReplay();
        var expected = JsonSerializer.Serialize(replay with { Actions = [], FinalStateHash = "" }, ConfigurationJson.Options);
        var actual = JsonSerializer.Serialize(header with { Actions = [], FinalStateHash = "" }, ConfigurationJson.Options);
        if (expected != actual)
            return new(null, [Unsupported("replay-version", "Replay metadata or initial state does not match.")]);
        foreach (var action in replay.Actions)
        {
            var result = engine.Step(action);
            if (!result.IsSuccess) return new(null, result.Diagnostics);
        }
        return engine.CaptureTrustedSnapshot().Hash == replay.FinalStateHash
            ? new(engine, [])
            : new(null, [Unsupported("replay-hash", "Replay final trusted state differs.")]);
    }

    private IEnumerable<GameAction> Candidates(int id)
    {
        if (Decision is { } frame)
        {
            var count = frame.Effect is ChoiceEffect choice ? choice.Options.Length : 2;
            for (var i = 0; i < count; i++) yield return new ChooseOptionAction(id, i);
            yield break;
        }
        if (_frames.Count > 0)
        {
            var current = _frames[^1];
            if (current.ParentId is null && current.Continuation.Kind == ExecutionContinuationKind.AgentTurn &&
                _deploying && current.Remaining.IsEmpty)
            {
                var max = Math.Min(Player(id).Units.TroopsInGarrison, _recruited + Math.Min(2, _oldGarrison));
                for (var i = 0; i <= max; i++) yield return new DeployAction(id, i);
                yield break;
            }
            if (IsRevealRoot)
            {
                foreach (var zone in PurchaseZones())
                    foreach (var selector in Selectors(zone))
                        yield return new AcquireCardAction(id, selector, zone.Id);
            }
            foreach (var work in current.Remaining)
                yield return new ResolveEffectAction(id, work.Id);
            if (IsRevealRoot) yield return new EndRevealAction(id);
            yield break;
        }
        foreach (var selector in Selectors(Player(id).Hand))
            foreach (var space in _state.Board.Spaces.OrderBy(x => x.Definition.Id.Value, StringComparer.Ordinal))
            {
                var count = _spaces.TryGetValue(space.Definition.Id, out var rule) && !rule.PlacementOptions.IsEmpty
                    ? rule.PlacementOptions.Length : 1;
                for (var option = 0; option < count; option++)
                    yield return new PlaceAgentAction(id, selector, space.Definition.Id, option);
            }
        yield return new RevealAction(id);
    }

    private TurnDiagnostic? Validate(GameAction action)
    {
        var leaderError = _state.Players.Select(LeaderSupport).FirstOrDefault(x => x is not null);
        if (action is StartRoundAction)
        {
            if (_state.Phase != GamePhase.BeforeRoundStart || _state.Round != 0)
                return Illegal("phase", "Round Start is available only before the first round.");
            if (leaderError is not null) return leaderError;
            return _state.Conflict.Deck.Count == 0 ? Content("conflict-empty", "No Conflict card is available.") : null;
        }
        if (_state.Phase != GamePhase.PlayerTurns)
            return Illegal("phase", "Player Turns are not active.");
        if (_state.DecisionOwnerId != action.PlayerId)
            return Illegal("decision-owner", "This player does not own the current decision.");
        var active = Player(_state.CurrentPlayerId!.Value);
        if (leaderError is not null) return leaderError;
        if (Decision is { } frame)
        {
            if (action is not ChooseOptionAction choose)
                return Illegal("pending-decision", "Resolve the pending decision first.");
            if (frame.Effect is OptionalPaymentEffect && choose.Option == 0) return ValidateRemaining();
            var selected = frame.Effect switch
            {
                OptionalPaymentEffect optional when choose.Option == 1 => new EffectOption("Pay", optional.Cost, optional.Effects),
                ChoiceEffect choice when choose.Option >= 0 && choose.Option < choice.Options.Length => choice.Options[choose.Option],
                _ => null,
            };
            if (selected is null || !CanPay(Player(frame.Context.PayerId), selected.Cost))
                return Illegal("option", "This option is unavailable or unaffordable.");
            return Preflight(selected.Effects, frame.Context.RecipientId, frame.Context.Space) ??
                ValidateRemaining(selected.Effects.Select(x => (x, frame.Context.RecipientId)), frame.Context.Space);
        }
        if (_frames.Count > 1 && (action is not ResolveEffectAction immediate ||
            !CurrentPending.Any(x => x.Id == immediate.EffectId)))
            return Illegal("child-effects", "Complete the current child before continuing suspended work.");
        switch (action)
        {
            case PlaceAgentAction place:
                if (_frames.Count > 0 || _revealing || _deploying || active.Temporary.HasRevealed ||
                    active.Units.AvailableAgents == 0)
                    return Illegal("agent-turn", "An Agent turn is unavailable.");
                var physical = Find(active.Hand, place.Card);
                if (physical is null) return Illegal("card", "The selected visible card is unavailable.");
                var board = _state.Board.Spaces.FirstOrDefault(x => x.Definition.Id == place.Space);
                if (board is null) return Illegal("space", "This board space does not exist.");
                if (!_cards.TryGetValue(physical.Definition, out var cardRule))
                    return Unsupported("card-support", "The selected card has no executable definition.");
                if (!_spaces.TryGetValue(place.Space, out var spaceRule))
                    return Unsupported("space-support", "The selected space has no executable definition.");
                if (_cardErrors.TryGetValue(cardRule.Id, out var cardError)) return cardError;
                if (_spaceErrors.TryGetValue(spaceRule.Id, out var spaceError)) return spaceError;
                if (ValidateCost(spaceRule.Cost) is { } invalidCost) return invalidCost;
                if (spaceRule.Capacity < 1) return Content("capacity", "Board occupancy capacity must be positive.");
                if (!cardRule.Icons.Contains(spaceRule.Icon) || board.Occupants.Length >= spaceRule.Capacity ||
                    !CanPay(active, spaceRule.Cost) ||
                    (spaceRule.Faction is { } faction && Influence(active, faction) < spaceRule.RequiredInfluence) ||
                    (spaceRule.OncePerGame && _used.Contains(new(active.Id, place.Space))))
                    return Illegal("placement", "Access, occupancy, cost or influence requirements are not met.");
                if (spaceRule.OncePerGame && (spaceRule.Effects.OfType<HighCouncilEffect>().Any() && active.HasHighCouncilSeat ||
                    spaceRule.Effects.OfType<SwordmasterEffect>().Any() && active.Units.UnacquiredSwordmasters == 0))
                    return Illegal("once-per-game", "This permanent upgrade is already acquired.");
                var optionError = PlacementOption(spaceRule, place.PaymentOption, active);
                if (optionError is not null) return optionError;
                var optionEffects = spaceRule.PlacementOptions.IsEmpty ? [] : spaceRule.PlacementOptions[place.PaymentOption].Effects;
                var selectedEffects = cardRule.AgentEffects.Concat(spaceRule.Effects).Concat(optionEffects);
                var contextError = ValidateContext(cardRule.AgentEffects, cardSource: true, boardSource: true, active.Id) ??
                    ValidateContext(spaceRule.Effects.Concat(optionEffects), cardSource: false, boardSource: true, active.Id);
                if (contextError is not null) return contextError;
                var issue = Preflight(selectedEffects, active.Id, place.Space);
                if (issue is null && spaceRule.ControlReward is { } controlReward && board.ControllerId is { } controller)
                    issue = ValidateResourceBounds(selectedEffects.Select(x => (x, active.Id))
                        .Append((new GainEffect(controlReward, 1), controller)), place.Space);
                if (issue is not null) return issue;
                if (spaceRule.ControlReward is { } reward && !Enum.IsDefined(reward))
                    return Content("control-reward", "Invalid control reward.");
                return null;
            case RevealAction:
                if (_frames.Count > 0 || _deploying || _revealing || active.Temporary.HasRevealed)
                    return Illegal("reveal", "Reveal is unavailable while another turn is resolving.");
                var revealEffects = new List<Effect>();
                var revealResources = new List<(Effect Effect, int Owner, BoardSpaceId? Space)>();
                foreach (var card in active.Hand.Cards)
                {
                    if (!_cards.TryGetValue(card.Definition, out var rule))
                        return Unsupported("reveal-support", "A remaining hand card has no executable Reveal definition.");
                    if (_cardErrors.TryGetValue(rule.Id, out var revealCardError)) return revealCardError;
                    if (ValidateContext(rule.RevealEffects, cardSource: true, boardSource: false, active.Id) is { } error) return error;
                    revealEffects.AddRange(rule.RevealEffects);
                    revealResources.AddRange(rule.RevealEffects.Select(x => (x, active.Id, (BoardSpaceId?)null)));
                }
                foreach (var space in _state.Board.Spaces.Where(x => x.Occupants.Any(a => a.PlayerId == active.Id)))
                    if (_spaces.TryGetValue(space.Definition.Id, out var rule))
                    {
                        if (_spaceErrors.TryGetValue(rule.Id, out var revealSpaceError)) return revealSpaceError;
                        if (ValidateContext(rule.RevealEffects, cardSource: false, boardSource: true, active.Id) is { } error) return error;
                        revealEffects.AddRange(rule.RevealEffects);
                        revealResources.AddRange(rule.RevealEffects.Select(x => (x, active.Id, (BoardSpaceId?)space.Definition.Id)));
                    }
                return Preflight(revealEffects, active.Id) ??
                    ValidateResourceBounds(revealResources.Concat(active.HasHighCouncilSeat
                        ? [(new GainEffect(ResourceKind.Persuasion, 2), active.Id, (BoardSpaceId?)null)] : []));
            case ResolveEffectAction resolve:
                var work = CurrentPending.FirstOrDefault(x => x.Id == resolve.EffectId && x.Context.DecisionOwnerId == action.PlayerId);
                return work is null ? Illegal("effect", "This pending effect is unavailable.") :
                    Preflight([work.Effect], work.Context.RecipientId, work.Context.Space) ?? ValidateRemaining();
            case DeployAction deploy:
                if (!_deploying || _frames.Count != 1 || !CurrentPending.IsEmpty ||
                    _frames[0].Continuation.Kind != ExecutionContinuationKind.AgentTurn || _revealing || deploy.Troops < 0 ||
                    deploy.Troops > Math.Min(active.Units.TroopsInGarrison, _recruited + Math.Min(2, _oldGarrison)))
                    return Illegal("deployment", "Deployment exceeds recruits plus at most two old garrison troops.");
                return null;
            case AcquireCardAction acquire:
                if (!IsRevealRoot || action.PlayerId != active.Id)
                    return Illegal("acquisition", "Purchases are available only during this player's Reveal.");
                var source = PurchaseZones().FirstOrDefault(x => x.Id == acquire.Source);
                var selected = source is null ? null : Find(source, acquire.Card);
                if (selected is null) return Illegal("card", "The selected public card is unavailable.");
                if (!_cards.TryGetValue(selected.Definition, out var purchase))
                    return Unsupported("acquire-support", "The selected card has no executable acquisition definition.");
                if (_cardErrors.TryGetValue(purchase.Id, out var purchaseError)) return purchaseError;
                if (!purchase.Purchasable) return Illegal("not-purchasable", "This card cannot be bought with Persuasion.");
                if (purchase.Cost < 0) return Content("cost", "Card cost must be nonnegative.");
                if (purchase.Cost > active.Temporary.Persuasion)
                    return Illegal("persuasion", "Insufficient Persuasion.");
                return ValidateContext(purchase.AcquireEffects, cardSource: true, boardSource: false, active.Id) ??
                    Preflight(purchase.AcquireEffects, active.Id) ??
                    ValidateResourceBounds(Pending.Select(x => (x.Effect, x.Context.RecipientId, x.Context.Space))
                        .Concat(purchase.AcquireEffects.Select(x => (x, active.Id, (BoardSpaceId?)null)))) ??
                    ValidatePermanentOperations(Pending.Select(x => (x.Effect, x.Context.RecipientId))
                        .Concat(purchase.AcquireEffects.Select(x => (x, active.Id)))) ??
                    ValidateInfluenceBounds(Pending.Select(x => (x.Effect, x.Context.RecipientId))
                        .Concat(purchase.AcquireEffects.Select(x => (x, active.Id)))) ??
                    ValidateAcquisitionStock(Pending.Select(x => (x.Effect, x.Context.RecipientId))
                        .Concat(purchase.AcquireEffects.Select(x => (x, active.Id))), selected);
            case EndRevealAction:
                return !IsRevealRoot || Pending.Any() ? Illegal("pending-effects", "Resolve all Reveal effects before ending Reveal.") : null;
            default:
                return Illegal("action", "This action is unavailable.");
        }
    }

    private static TurnDiagnostic? PlacementOption(BoardRule rule, int index, PlayerState player)
    {
        if (rule.PlacementOptions.IsEmpty)
            return index == 0 ? null : Illegal("payment-option", "This placement has no payment choice.");
        if (index < 0 || index >= rule.PlacementOptions.Length)
            return Illegal("payment-option", "This payment choice does not exist.");
        var cost = rule.PlacementOptions[index].Cost;
        if (ValidateCost(cost) is { } issue) return issue;
        return CanPay(player, new(rule.Cost.Solari + cost.Solari, rule.Cost.Spice + cost.Spice,
            rule.Cost.Water + cost.Water)) ? null : Illegal("payment-option", "The selected placement payment is unaffordable.");
    }

    private TurnDiagnostic? LeaderSupport(PlayerState player)
    {
        if (!_leaders.TryGetValue(player.Leader, out var leader) || !leader.FullySupported)
            return Unsupported("leader-support", "The active leader's passive and Signet contract is unsupported.");
        if (!leader.Synthetic)
            return Unsupported("leader-support", "Official leader passive and Signet implementations remain unsupported in this slice.");
        if (leader.Synthetic && (_state.Authenticity != ContentAuthenticity.SyntheticFixture ||
            !leader.Id.Value.Contains("fixture", StringComparison.Ordinal)))
            return Content("leader-provenance", "Synthetic leader support requires an explicit fixture leader and inventory.");
        return null;
    }

    private TurnDiagnostic? Preflight(IEnumerable<Effect> effects, int owner, BoardSpaceId? space = null)
    {
        var materialized = effects.ToImmutableArray();
        // Executable boxes were checked with their actual source/role context before activation.
        return ValidateResourceBounds(materialized.Select(x => (x, owner)), space) ??
            ValidateInfluenceBounds(materialized.Select(x => (x, owner))) ??
            ValidateAcquisitionStock(materialized.Select(x => (x, owner))) ??
            ValidatePermanentOperations(materialized.Select(x => (x, owner)));
    }

    private TurnDiagnostic? Support(ImmutableArray<Effect> effects, int owner, bool card, bool board, int depth = 0)
    {
        if (depth > 32) return Content("effect-cycle", "Effect or acquisition/Signet graph is cyclic or too deep.");
        var key = (effects, owner, card, board, depth);
        if (_support.TryGetValue(key, out var cached)) return cached;
        var shape = _shapes.TryGetValue(effects, out var known) ? known : _graphValidator.Validate(effects);
        TurnDiagnostic? issue = shape.FirstOrDefault();
        if (issue?.Status == StepStatus.Unsupported)
            issue = Unsupported(issue.Code, "A selected effect is unsupported.");
        foreach (var effect in effects)
        {
            if (issue is not null) break;
            issue = effect switch
            {
                TrashSelfEffect when !card => Content("effect-context", "Self-trash requires a source card."),
                HarvestSpiceEffect when !board => Content("effect-context", "Spice harvest requires a board space."),
                SequenceEffect sequence => Support(sequence.Effects, owner, card, board, depth + 1),
                PlayerOrderedEffect ordered => Support(ordered.Effects, owner, card, board, depth + 1),
                OptionalPaymentEffect optional => ValidateRoles(optional.DecisionOwnerId, optional.PayerId, optional.RecipientId) ??
                    Support(optional.Effects, optional.RecipientId ?? optional.DecisionOwnerId ?? owner, card, board, depth + 1),
                ChoiceEffect choice => choice.Options.Select(x => Support(x.Effects, owner, card, board, depth + 1)).FirstOrDefault(x => x is not null),
                ForPlayerEffect target => ValidateOwner(target.PlayerId) ?? ValidateRoles(target.DecisionOwnerId, target.PayerId, target.RecipientId) ??
                    Support(target.Effects, target.RecipientId ?? target.PlayerId, card, board, depth + 1),
                AcquireEffect acquire => !_cards.TryGetValue(acquire.Card, out var rule)
                    ? Unsupported("acquire-effect", "The granted card is unsupported.")
                    : _cardErrors.GetValueOrDefault(rule.Id) ?? Support(rule.AcquireEffects, owner, true, false, depth + 1),
                SignetEffect => Support(_leaders[Player(owner).Leader].SignetEffects, owner, card, board, depth + 1),
                _ => null,
            };
        }
        _support[key] = issue;
        return issue;
    }

    private TurnDiagnostic? ValidateRemaining(IEnumerable<(Effect Effect, int Owner)>? selected = null, BoardSpaceId? sourceSpace = null)
    {
        var work = Pending.Select(x => (x.Effect, x.Context.RecipientId)).Concat(selected ?? []).ToArray();
        var resources = Pending.Select(x => (x.Effect, x.Context.RecipientId, x.Context.Space))
            .Concat((selected ?? []).Select(x => (x.Effect, x.Owner, sourceSpace)));
        return ValidateResourceBounds(resources) ?? ValidateInfluenceBounds(work) ?? ValidateAcquisitionStock(work) ?? ValidatePermanentOperations(work);
    }

    private TurnDiagnostic? ValidatePermanentOperations(IEnumerable<(Effect Effect, int Owner)> work)
    {
        var totals = new Dictionary<(int Owner, bool Swordmaster), int>();
        foreach (var (effect, owner) in work) Accumulate([effect], owner, totals);
        foreach (var (key, count) in totals)
            if (count > (key.Swordmaster ? Player(key.Owner).Units.UnacquiredSwordmasters : Player(key.Owner).HasHighCouncilSeat ? 0 : 1))
                return Illegal("permanent-upgrade", "A selected permanent upgrade is already acquired or granted more than once.");
        return null;

        void Accumulate(IEnumerable<Effect> effects, int owner, Dictionary<(int Owner, bool Swordmaster), int> target)
        {
            foreach (var effect in effects)
                switch (effect)
                {
                    case SwordmasterEffect or HighCouncilEffect:
                        var key = (owner, effect is SwordmasterEffect);
                        target[key] = target.GetValueOrDefault(key) + 1;
                        break;
                    case SequenceEffect sequence: Accumulate(sequence.Effects, owner, target); break;
                    case PlayerOrderedEffect ordered: Accumulate(ordered.Effects, owner, target); break;
                    case ForPlayerEffect player: Accumulate(player.Effects, player.RecipientId ?? player.PlayerId, target); break;
                    case AcquireEffect acquire: Accumulate(_cards[acquire.Card].AcquireEffects, owner, target); break;
                    case SignetEffect: Accumulate(_leaders[Player(owner).Leader].SignetEffects, owner, target); break;
                        // Optional and choice branches are checked when selected, before any payment or frame mutation.
                }
        }
    }

    private TurnDiagnostic? ValidateResourceBounds(IEnumerable<(Effect Effect, int Owner)> work, BoardSpaceId? space = null) =>
        ValidateResourceBounds(work.Select(x => (x.Effect, x.Owner, space)));

    private TurnDiagnostic? ValidateResourceBounds(IEnumerable<(Effect Effect, int Owner, BoardSpaceId? Space)> work)
    {
        var totals = new Dictionary<(int Owner, ResourceKind Resource), long>();
        foreach (var (effect, owner, space) in work) Accumulate([effect], owner, space, totals);
        foreach (var (key, amount) in totals)
        {
            var player = Player(key.Owner);
            var current = key.Resource switch
            {
                ResourceKind.Solari => player.Resources.Solari,
                ResourceKind.Spice => player.Resources.Spice,
                ResourceKind.Water => player.Resources.Water,
                ResourceKind.VictoryPoints => player.Resources.VictoryPoints,
                ResourceKind.Persuasion => player.Temporary.Persuasion,
                ResourceKind.Swords => player.Temporary.Swords,
                _ => 0,
            };
            if (current + amount > int.MaxValue) return Content("resource-bounds", "Selected gains exceed the resource bounds.");
        }
        return null;

        void Accumulate(IEnumerable<Effect> effects, int owner, BoardSpaceId? space, Dictionary<(int Owner, ResourceKind Resource), long> target)
        {
            foreach (var effect in effects)
                switch (effect)
                {
                    case HarvestSpiceEffect:
                        var spice = (owner, ResourceKind.Spice);
                        target[spice] = target.GetValueOrDefault(spice) + (space is { } board ? _state.Board.Spaces.Single(x => x.Definition.Id == board).BonusSpice : 0);
                        break;
                    case GainEffect gain:
                        var key = (owner, gain.Resource);
                        target[key] = target.GetValueOrDefault(key) + gain.Amount;
                        break;
                    case InfluenceEffect influence when Influence(Player(owner), influence.Faction) < 2 &&
                        (long)Influence(Player(owner), influence.Faction) + influence.Amount >= 2:
                        var vp = (owner, ResourceKind.VictoryPoints);
                        target[vp] = target.GetValueOrDefault(vp) + 1;
                        break;
                    case SequenceEffect sequence: Accumulate(sequence.Effects, owner, space, target); break;
                    case PlayerOrderedEffect ordered: Accumulate(ordered.Effects, owner, space, target); break;
                    case OptionalPaymentEffect optional: Accumulate(optional.Effects, optional.RecipientId ?? optional.DecisionOwnerId ?? owner, space, target); break;
                    case ForPlayerEffect player: Accumulate(player.Effects, player.RecipientId ?? player.PlayerId, space, target); break;
                    case AcquireEffect acquire: Accumulate(_cards[acquire.Card].AcquireEffects, owner, null, target); break;
                    case SignetEffect: Accumulate(_leaders[Player(owner).Leader].SignetEffects, owner, space, target); break;
                    case ChoiceEffect choice:
                        var maxima = new Dictionary<(int Owner, ResourceKind Resource), long>();
                        foreach (var option in choice.Options)
                        {
                            var branch = new Dictionary<(int Owner, ResourceKind Resource), long>();
                            Accumulate(option.Effects, owner, space, branch);
                            foreach (var (resource, value) in branch) maxima[resource] = Math.Max(maxima.GetValueOrDefault(resource), value);
                        }
                        foreach (var (resource, value) in maxima) target[resource] = target.GetValueOrDefault(resource) + value;
                        break;
                }
        }
    }

    private TurnDiagnostic? ValidateInfluenceBounds(IEnumerable<(Effect Effect, int Owner)> work)
    {
        // Immediate acquisitions and suspended work must remain jointly resolvable for every owner.
        var influenceBounds = new Dictionary<(int Owner, Faction Faction), long>();
        foreach (var (effect, owner) in work)
            AccumulateInfluence([effect], owner, influenceBounds);
        foreach (var (key, amount) in influenceBounds)
            if (Influence(Player(key.Owner), key.Faction) + amount >= 4)
                return Unsupported("alliance-bonus", "The faction alliance bonus is not yet supported.");
        return null;
    }

    private TurnDiagnostic? ValidateAcquisitionStock(IEnumerable<(Effect Effect, int Owner)> work,
        CardInstance? consumed = null)
    {
        var demand = new Dictionary<ContentId, long>();
        var mandatory = new HashSet<ContentId>();
        foreach (var (effect, owner) in work)
            AccumulateAcquisitionDemand([effect], owner, demand, mandatory);
        foreach (var target in mandatory)
        {
            // Only visible stock is safe to reserve; a market refill may not assume hidden deck content.
            var stock = PurchaseZones().Sum(zone => zone.Cards.Count(card => card.Definition == target &&
                card.Id != consumed?.Id));
            if (demand.GetValueOrDefault(target) > stock)
                return Unsupported("acquire-stock", "The complete effect graph could exhaust public stock required by a mandatory acquisition.");
        }
        return null;
    }

    private void AccumulateAcquisitionDemand(IEnumerable<Effect> effects, int owner,
        Dictionary<ContentId, long> totals, HashSet<ContentId> mandatory, int depth = 0, bool required = true)
    {
        if (depth > 32) throw new InvalidOperationException("A cyclic acquisition graph passed preflight.");
        foreach (var effect in effects)
            switch (effect)
            {
                case AcquireEffect acquire:
                    totals[acquire.Card] = totals.GetValueOrDefault(acquire.Card) + 1;
                    if (required && !acquire.AllowEmpty) mandatory.Add(acquire.Card);
                    AccumulateAcquisitionDemand(_cards[acquire.Card].AcquireEffects, owner, totals, mandatory, depth + 1, required);
                    break;
                case SequenceEffect sequence:
                    AccumulateAcquisitionDemand(sequence.Effects, owner, totals, mandatory, depth + 1, required);
                    break;
                case PlayerOrderedEffect ordered:
                    AccumulateAcquisitionDemand(ordered.Effects, owner, totals, mandatory, depth + 1, required);
                    break;
                case OptionalPaymentEffect optional:
                    AccumulateAcquisitionDemand(optional.Effects, optional.RecipientId ?? optional.DecisionOwnerId ?? owner, totals, mandatory, depth + 1, false);
                    break;
                case ForPlayerEffect player:
                    AccumulateAcquisitionDemand(player.Effects, player.RecipientId ?? player.PlayerId, totals, mandatory, depth + 1, required);
                    break;
                case SignetEffect:
                    AccumulateAcquisitionDemand(_leaders[Player(owner).Leader].SignetEffects, owner, totals, mandatory, depth + 1, required);
                    break;
                case ChoiceEffect choice:
                    var maxima = new Dictionary<ContentId, long>();
                    foreach (var option in choice.Options)
                    {
                        var branch = new Dictionary<ContentId, long>();
                        AccumulateAcquisitionDemand(option.Effects, owner, branch, mandatory, depth + 1, required);
                        foreach (var (target, amount) in branch)
                            maxima[target] = Math.Max(maxima.GetValueOrDefault(target), amount);
                    }
                    foreach (var (target, amount) in maxima)
                        totals[target] = totals.GetValueOrDefault(target) + amount;
                    break;
            }
    }

    private void AccumulateInfluence(IEnumerable<Effect> effects, int owner,
        Dictionary<(int Owner, Faction Faction), long> totals, int depth = 0)
    {
        if (depth > 32) throw new InvalidOperationException("A cyclic graph passed preflight.");
        foreach (var effect in effects)
            switch (effect)
            {
                case InfluenceEffect influence:
                    var key = (owner, influence.Faction);
                    totals[key] = totals.GetValueOrDefault(key) + influence.Amount;
                    break;
                case SequenceEffect sequence:
                    AccumulateInfluence(sequence.Effects, owner, totals, depth + 1);
                    break;
                case PlayerOrderedEffect ordered:
                    AccumulateInfluence(ordered.Effects, owner, totals, depth + 1);
                    break;
                case OptionalPaymentEffect optional:
                    AccumulateInfluence(optional.Effects, optional.RecipientId ?? optional.DecisionOwnerId ?? owner, totals, depth + 1);
                    break;
                case ChoiceEffect choice:
                    var maxima = new Dictionary<(int Owner, Faction Faction), long>();
                    foreach (var option in choice.Options)
                    {
                        var branch = new Dictionary<(int Owner, Faction Faction), long>();
                        AccumulateInfluence(option.Effects, owner, branch, depth + 1);
                        foreach (var (target, amount) in branch)
                            maxima[target] = Math.Max(maxima.GetValueOrDefault(target), amount);
                    }
                    foreach (var (target, amount) in maxima) totals[target] = totals.GetValueOrDefault(target) + amount;
                    break;
                case ForPlayerEffect player:
                    AccumulateInfluence(player.Effects, player.RecipientId ?? player.PlayerId, totals, depth + 1);
                    break;
                case AcquireEffect acquire:
                    AccumulateInfluence(_cards[acquire.Card].AcquireEffects, owner, totals, depth + 1);
                    break;
                case SignetEffect:
                    AccumulateInfluence(_leaders[Player(owner).Leader].SignetEffects, owner, totals, depth + 1);
                    break;
            }
    }

    private TurnDiagnostic? ValidateContext(IEnumerable<Effect> effects, bool cardSource, bool boardSource, int owner) =>
        Support(effects.ToImmutableArray(), owner, cardSource, boardSource);

    private void BeginRound()
    {
        _state.Round = 1;
        _state.Conflict.Deck.TryDrawTo(_state.Conflict.Revealed);
        foreach (var player in _state.Players)
        {
            player.Temporary = new(0, 0, false);
            player.CombatMarker = 0;
            for (var i = 0; i < 5; i++) player.Deck.TryDrawTo(player.Hand, player.Discard, _state.Random);
        }
        _state.Phase = GamePhase.PlayerTurns;
        _state.CurrentPlayerId = _state.FirstPlayerId;
        _state.DecisionOwnerId = _state.FirstPlayerId;
    }

    private void Place(PlaceAgentAction action)
    {
        var player = Player(action.PlayerId);
        var card = Find(player.Hand, action.Card)!;
        var rule = _spaces[action.Space];
        var board = _state.Board.Spaces.Single(x => x.Definition.Id == action.Space);
        Pay(player, rule.Cost);
        if (!rule.PlacementOptions.IsEmpty) Pay(player, rule.PlacementOptions[action.PaymentOption].Cost);
        player.Hand.MoveTo(card.Id, player.InPlay);
        player.Units = player.Units with { AvailableAgents = player.Units.AvailableAgents - 1 };
        board.Occupants = board.Occupants.Add(new(player.Id, AgentKind.Agent));
        if (rule.OncePerGame) _used.Add(new(player.Id, action.Space));
        _oldGarrison = player.Units.TroopsInGarrison;
        _recruited = 0;
        _deploying = rule.Combat;
        var context = RootContext(player.Id, card.Id, card.Definition, action.Space, EffectProvenance.Card);
        PushFrame([], context, EffectGroupMode.PlayerOrdered, new(ExecutionContinuationKind.AgentTurn, player.Id));
        Enqueue(_cards[card.Definition].AgentEffects, context);
        var boardContext = RootContext(player.Id, null, null, action.Space, EffectProvenance.Board);
        Enqueue(rule.Effects, boardContext);
        if (!rule.PlacementOptions.IsEmpty) Enqueue(rule.PlacementOptions[action.PaymentOption].Effects, boardContext);
        if (rule.ControlReward is { } reward && board.ControllerId is { } controller)
            Enqueue([new GainEffect(reward, 1)], boardContext with { RecipientId = controller });
        ContinueTurn();
    }

    private void Reveal()
    {
        var player = Player(_state.CurrentPlayerId!.Value);
        _revealing = true;
        player.Temporary = player.Temporary with
        {
            HasRevealed = true,
            Persuasion = player.Temporary.Persuasion + (player.HasHighCouncilSeat ? 2 : 0)
        };
        PushFrame([], RootContext(player.Id, null, null, null, EffectProvenance.Card),
            EffectGroupMode.PlayerOrdered, new(ExecutionContinuationKind.RevealTurn, player.Id));
        foreach (var card in player.Hand.Cards.ToArray())
        {
            player.Hand.MoveTo(card.Id, player.InPlay);
            Enqueue(_cards[card.Definition].RevealEffects, RootContext(player.Id, card.Id, card.Definition, null, EffectProvenance.Card));
        }
        foreach (var space in _state.Board.Spaces.Where(x => x.Occupants.Any(a => a.PlayerId == player.Id)))
            if (_spaces.TryGetValue(space.Definition.Id, out var rule))
                Enqueue(rule.RevealEffects, RootContext(player.Id, null, null, space.Definition.Id, EffectProvenance.Board));
        ContinueTurn();
    }

    private void Execute(Effect effect, EffectContext context)
    {
        var player = Player(context.RecipientId);
        switch (effect)
        {
            case GainEffect gain:
                Gain(player, gain.Resource, gain.Amount);
                break;
            case DrawEffect draw:
                for (var i = 0; i < draw.Amount; i++) player.Deck.TryDrawTo(player.Hand, player.Discard, _state.Random);
                break;
            case RecruitEffect recruit:
                var amount = Math.Min(recruit.Amount, player.Units.TroopsInSupply);
                player.Units = player.Units with
                {
                    TroopsInSupply = player.Units.TroopsInSupply - amount,
                    TroopsInGarrison = player.Units.TroopsInGarrison + amount
                };
                if (player.Id == _state.CurrentPlayerId) _recruited += amount;
                break;
            case InfluenceEffect influence:
                var before = Influence(player, influence.Faction);
                var after = before + influence.Amount;
                player.Influence = [.. player.Influence.Select(x => x.Faction == influence.Faction ? x with { Amount = after } : x)];
                if (before < 2 && after >= 2) Gain(player, ResourceKind.VictoryPoints, 1);
                break;
            case OptionalPaymentEffect optional:
                _frames[^1] = _frames[^1] with
                {
                    Decision = new(optional, BindRoles(context, optional.DecisionOwnerId,
                    optional.PayerId, optional.RecipientId))
                };
                break;
            case ChoiceEffect choice:
                _frames[^1] = _frames[^1] with { Decision = new(choice, context) };
                break;
            case ForPlayerEffect target:
                PushChild(target.Effects, BindRoles(context, target.DecisionOwnerId ?? target.PlayerId,
                    target.PayerId ?? target.PlayerId, target.RecipientId ?? target.PlayerId), EffectGroupMode.PlayerOrdered);
                break;
            case SequenceEffect sequence:
                PushChild(sequence.Effects, context, EffectGroupMode.Sequence);
                break;
            case PlayerOrderedEffect ordered:
                PushChild(ordered.Effects, context, EffectGroupMode.PlayerOrdered);
                break;
            case TrashSelfEffect:
                if (context.SourceCard is { } id)
                {
                    var zone = _state.Zones.SingleOrDefault(x => x.Cards.Any(c => c.Id == id));
                    if (zone is null) throw new InvalidOperationException("Self-trash source card is missing from the game.");
                    if (zone != _state.Shared.Trashed) zone.MoveTo(id, _state.Shared.Trashed);
                }
                else throw new InvalidOperationException("Self-trash source context is missing.");
                break;
            case AcquireEffect acquire:
                var source = PurchaseZones().FirstOrDefault(x => x.Cards.Any(c => c.Definition == acquire.Card));
                if (source is not null) Acquire(source.Cards.First(x => x.Definition == acquire.Card), source, player,
                    context);
                break;
            case HighCouncilEffect:
                player.HasHighCouncilSeat = true;
                break;
            case SwordmasterEffect:
                player.Units = player.Units with
                {
                    AvailableAgents = player.Units.AvailableAgents + 1,
                    UnacquiredSwordmasters = player.Units.UnacquiredSwordmasters - 1
                };
                break;
            case HarvestSpiceEffect:
                var board = _state.Board.Spaces.Single(x => x.Definition.Id == context.Space);
                Gain(player, ResourceKind.Spice, board.BonusSpice);
                board.BonusSpice = 0;
                break;
            case SignetEffect:
                PushChild(_leaders[player.Leader].SignetEffects, context with { Provenance = EffectProvenance.Signet }, EffectGroupMode.PlayerOrdered);
                break;
            default:
                throw new InvalidOperationException("Unsupported effect passed preflight.");
        }
    }

    private void Choose(int index)
    {
        var frame = Decision!;
        _frames[^1] = _frames[^1] with { Decision = null };
        switch (frame.Effect)
        {
            case OptionalPaymentEffect optional when index == 1:
                Pay(Player(frame.Context.PayerId), optional.Cost);
                PushChild(optional.Effects, frame.Context, EffectGroupMode.PlayerOrdered);
                break;
            case ChoiceEffect choice:
                var option = choice.Options[index];
                Pay(Player(frame.Context.PayerId), option.Cost);
                PushChild(option.Effects, frame.Context, EffectGroupMode.PlayerOrdered);
                break;
        }
    }

    private void Acquire(CardInstance card, CardZone source, PlayerState player, EffectContext context)
    {
        source.MoveTo(card.Id, player.Discard);
        if (source == _state.Shared.ImperiumRow) _state.Shared.ImperiumDeck.TryDrawTo(source);
        PushChild(_cards[card.Definition].AcquireEffects, context with
        {
            SourceCard = card.Id,
            SourceDefinition = card.Definition,
            Space = null,
            Provenance = EffectProvenance.Acquisition,
        }, EffectGroupMode.PlayerOrdered);
    }

    private static EffectContext RootContext(int player, CardInstanceId? card, ContentId? definition,
        BoardSpaceId? space, EffectProvenance provenance) => new(player, player, player, player,
            card, definition, space, provenance);

    private static EffectContext BindRoles(EffectContext context, int? chooser, int? payer, int? recipient) =>
        context with
        {
            DecisionOwnerId = chooser ?? context.DecisionOwnerId,
            PayerId = payer ?? chooser ?? context.PayerId,
            RecipientId = recipient ?? chooser ?? context.RecipientId,
        };

    private bool IsRevealRoot => _frames.Count == 1 &&
        _frames[0].Continuation.Kind == ExecutionContinuationKind.RevealTurn;

    private void PushChild(IEnumerable<Effect> effects, EffectContext context, EffectGroupMode mode) =>
        PushFrame(effects, context, mode, new(ExecutionContinuationKind.ParentFrame));

    private void PushFrame(IEnumerable<Effect> effects, EffectContext context, EffectGroupMode mode,
        ExecutionContinuation continuation)
    {
        var parent = _frames.Count == 0 ? (int?)null : _frames[^1].Id;
        var id = ++_nextFrameId;
        _frames.Add(new(id, parent, mode, context.DecisionOwnerId, [], continuation));
        Enqueue(effects, context);
    }

    private void Enqueue(IEnumerable<Effect> effects, EffectContext context)
    {
        var frame = _frames[^1];
        _frames[^1] = frame with
        {
            Remaining = frame.Remaining.AddRange(effects.Select(effect =>
                new PendingEffect(++_nextEffectId, effect, context with { ParentFrameId = frame.Id }))),
        };
    }

    private PendingEffect Take(int id)
    {
        var frame = _frames[^1];
        var work = frame.Remaining.Single(x => x.Id == id);
        _frames[^1] = frame with { Remaining = frame.Remaining.Remove(work) };
        return work;
    }

    private EffectExecutionFrame PopFrame()
    {
        var frame = _frames[^1];
        _frames.RemoveAt(_frames.Count - 1);
        return frame;
    }

    private void Resume(ExecutionContinuation continuation)
    {
        switch (continuation.Kind)
        {
            case ExecutionContinuationKind.ParentFrame:
                if (_frames.Count == 0) throw new InvalidOperationException("A child lost its parent frame.");
                break;
            case ExecutionContinuationKind.AgentTurn:
                if (_state.CurrentPlayerId != continuation.PlayerId)
                    throw new InvalidOperationException("Agent continuation belongs to a different turn.");
                EndAgentTurn();
                break;
            default:
                throw new InvalidOperationException("This execution continuation is not implemented.");
        }
    }

    private void ContinueTurn()
    {
        while (Decision is null && _frames.Count > 0)
        {
            var frame = _frames[^1];
            if (frame.Remaining.IsEmpty)
            {
                if (frame.ParentId is null && (frame.Continuation.Kind == ExecutionContinuationKind.RevealTurn || _deploying))
                    break;
                Resume(PopFrame().Continuation);
                continue;
            }
            var available = frame.Remaining;
            if (frame.Mode == EffectGroupMode.Sequence)
            {
                var next = Take(available[0].Id);
                Execute(next.Effect, next.Context);
                continue;
            }
            if (!IsRevealRoot && available.Select(x => x.Context.RecipientId).Distinct().Count() == 1 &&
                available.All(x => x.Effect is GainEffect or RecruitEffect or InfluenceEffect or HarvestSpiceEffect))
            {
                // These same-recipient primitives commute; no ordering choice remains in this frame.
                foreach (var commutative in available)
                {
                    Take(commutative.Id);
                    Execute(commutative.Effect, commutative.Context);
                }
                continue;
            }
            if (available.Length != 1 || IsRevealRoot ||
                available[0].Effect is not (GainEffect or DrawEffect or RecruitEffect or InfluenceEffect or
                    HarvestSpiceEffect or HighCouncilEffect or SwordmasterEffect))
                break;
            var work = Take(available[0].Id);
            Execute(work.Effect, work.Context);
        }
        if (Decision is { } decision) _state.DecisionOwnerId = decision.Context.DecisionOwnerId;
        else if (_frames.Count > 0) _state.DecisionOwnerId = _frames[^1].ChooserId;
    }

    private void EndAgentTurn()
    {
        _recruited = 0;
        _oldGarrison = 0;
        NextPlayer();
    }

    private void EndReveal()
    {
        var continuation = PopFrame().Continuation;
        if (continuation.Kind != ExecutionContinuationKind.RevealTurn || continuation.PlayerId != _state.CurrentPlayerId)
            throw new InvalidOperationException("Reveal continuation belongs to a different turn.");
        var player = Player(_state.CurrentPlayerId!.Value);
        foreach (var zone in new[] { player.InPlay, player.Hand })
            foreach (var card in zone.Cards.ToArray()) zone.MoveTo(card.Id, player.Discard);
        player.CombatMarker = CombatStrength.Calculate(
            [new("troop", player.Units.TroopsInConflict, _rules.Strength.TroopStrength)],
            player.Temporary.Swords, _rules.Strength.SwordStrength, _rules.Strength.RequiresDeployedUnits);
        player.Temporary = player.Temporary with { Persuasion = 0 };
        _revealing = false;
        NextPlayer();
    }

    private void NextPlayer()
    {
        var current = _state.CurrentPlayerId!.Value;
        for (var offset = 1; offset <= _state.Players.Count; offset++)
        {
            var next = (current + offset) % _state.Players.Count;
            if (Player(next).Temporary.HasRevealed) continue;
            _state.CurrentPlayerId = next;
            _state.DecisionOwnerId = next;
            return;
        }
        _state.Phase = GamePhase.CombatBoundary;
        _state.CurrentPlayerId = null;
        _state.DecisionOwnerId = null;
    }

    private static void Gain(PlayerState player, ResourceKind resource, int amount)
    {
        player.Resources = resource switch
        {
            ResourceKind.Solari => player.Resources with { Solari = player.Resources.Solari + amount },
            ResourceKind.Spice => player.Resources with { Spice = player.Resources.Spice + amount },
            ResourceKind.Water => player.Resources with { Water = player.Resources.Water + amount },
            ResourceKind.VictoryPoints => player.Resources with { VictoryPoints = player.Resources.VictoryPoints + amount },
            _ => player.Resources,
        };
        player.Temporary = resource switch
        {
            ResourceKind.Persuasion => player.Temporary with { Persuasion = player.Temporary.Persuasion + amount },
            ResourceKind.Swords => player.Temporary with { Swords = player.Temporary.Swords + amount },
            _ => player.Temporary,
        };
    }

    private void AssertInvariants()
    {
        var cards = _state.Zones.SelectMany(x => x.Cards).ToArray();
        if (cards.Length != _cardCount || cards.Select(x => x.Id).Distinct().Count() != cards.Length)
            throw new InvalidOperationException("Physical card conservation invariant failed.");
        foreach (var player in _state.Players)
        {
            if (player.Resources.Solari < 0 || player.Resources.Spice < 0 || player.Resources.Water < 0 ||
                player.Resources.VictoryPoints < 0 || player.Units.AvailableAgents < 0 ||
                player.Units.UnacquiredSwordmasters < 0 || player.Units.TroopsInSupply < 0 ||
                player.Units.TroopsInGarrison < 0 || player.Units.TroopsInConflict < 0 ||
                player.Temporary.Persuasion < 0 || player.Temporary.Swords < 0)
                throw new InvalidOperationException("Negative resource or unit invariant failed.");
            if (player.Units.TroopsInSupply + player.Units.TroopsInGarrison + player.Units.TroopsInConflict != _troopTotals[player.Id])
                throw new InvalidOperationException("Troop conservation invariant failed.");
        }
    }

    private PlayerState Player(int id) => _state.Players.FirstOrDefault(x => x.Id == id)
        ?? throw new ArgumentOutOfRangeException(nameof(id));
    private TurnDiagnostic? ValidateOwner(int id) => _state.Players.Any(x => x.Id == id) ? null :
        Content("effect-owner", "Effect names an invalid decision owner.");
    private static int Influence(PlayerState player, Faction faction) => player.Influence.Single(x => x.Faction == faction).Amount;
    private static bool CanPay(PlayerState player, ResourceCost cost) => cost.Solari >= 0 && cost.Spice >= 0 && cost.Water >= 0 &&
        player.Resources.Solari >= cost.Solari && player.Resources.Spice >= cost.Spice && player.Resources.Water >= cost.Water;
    private static void Pay(PlayerState player, ResourceCost cost) => player.Resources = player.Resources with
    { Solari = player.Resources.Solari - cost.Solari, Spice = player.Resources.Spice - cost.Spice, Water = player.Resources.Water - cost.Water };
    private static TurnDiagnostic? ValidateCost(ResourceCost cost) => cost is null || cost.Solari < 0 || cost.Spice < 0 || cost.Water < 0
        ? Content("cost", "Costs must be nonnegative.") : null;
    private IEnumerable<CardZone> PurchaseZones() => new[] { _state.Shared.ImperiumRow }.Concat(_state.Shared.Reserves);
    private static CardInstance? Find(CardZone zone, CardSelector selector) => selector.Copy < 0 ? null :
        zone.Cards.Where(x => x.Definition == selector.Definition).Skip(selector.Copy).FirstOrDefault();
    private static IEnumerable<CardSelector> Selectors(CardZone zone)
    {
        foreach (var group in zone.Cards.GroupBy(x => x.Definition).OrderBy(x => x.Key.Value, StringComparer.Ordinal))
            for (var i = 0; i < group.Count(); i++) yield return new(group.Key, i);
    }
    private TurnDiagnostic? ValidateRoles(int? chooser, int? payer, int? recipient) =>
        (chooser is { } c ? ValidateOwner(c) : null) ??
        (payer is { } p ? ValidateOwner(p) : null) ??
        (recipient is { } r ? ValidateOwner(r) : null);

    private ImmutableArray<Effect> Normalize(ImmutableArray<Effect> effects, int depth = 0, TurnDiagnostic? definitionError = null)
    {
        // Shape diagnostics stay bound to this box. Invalid input becomes a deterministic nonexecutable
        // marker before canonical serialization; its identity binds normalized data, not discarded input.
        if (depth > 32) return [];
        var diagnostics = definitionError is null ? _graphValidator.Validate(effects) : [definitionError];
        if (diagnostics.Any(x => x.Status == StepStatus.ContentError || x.Code == "effect-type"))
        {
            ImmutableArray<Effect> invalid = [new UnsupportedEffect(string.Join(";", diagnostics.Select(x => x.Code + ":" + x.Message)))];
            _shapes[invalid] = diagnostics;
            return invalid;
        }
        var normalized = OrEmpty(effects).Where(x => x is not null).Select(effect => effect switch
        {
            SequenceEffect sequence => sequence with { Effects = Normalize(sequence.Effects, depth + 1) },
            PlayerOrderedEffect ordered => ordered with { Effects = Normalize(ordered.Effects, depth + 1) },
            OptionalPaymentEffect optional => optional with { Effects = Normalize(optional.Effects, depth + 1) },
            ForPlayerEffect target => target with { Effects = Normalize(target.Effects, depth + 1) },
            ChoiceEffect choice => choice with
            {
                Options = [.. OrEmpty(choice.Options)
                .Select(option => option with { Effects = Normalize(option.Effects, depth + 1) })]
            },
            _ => effect,
        }).ToImmutableArray();
        _shapes[normalized] = diagnostics;
        return normalized;
    }

    private static ImmutableArray<T> OrEmpty<T>(ImmutableArray<T> values) => values.IsDefault ? [] : values;
    private static TurnDiagnostic Illegal(string code, string message) => new(StepStatus.Illegal, code, message);
    private static TurnDiagnostic Unsupported(string code, string message) => new(StepStatus.Unsupported, code, message);
    private static TurnDiagnostic Content(string code, string message) => new(StepStatus.ContentError, code, message);
}
