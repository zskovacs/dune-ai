using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;

namespace Dune.Engine.Turns;

/// <summary>Incremental turns and decision windows over trusted setup state; unsupported lifecycle work stops explicitly.</summary>
public sealed partial class TurnEngine
{
    private const string InvalidLifecycleVersion = "invalid-lifecycle:";
    private readonly GameState _state;
    private readonly TurnRuleSet _rules;
    private readonly Dictionary<ContentId, TurnDiagnostic> _cardErrors = [];
    private readonly Dictionary<BoardSpaceId, TurnDiagnostic> _spaceErrors = [];
    private readonly Dictionary<ContentId, CardRule> _cards;
    private readonly Dictionary<BoardSpaceId, BoardRule> _spaces;
    private readonly Dictionary<ContentId, LeaderRule> _leaders;
    private readonly Dictionary<ContentId, IntrigueRule> _intrigues;
    private readonly TurnDiagnostic? _lifecycleDefinitionError;
    private readonly TurnDiagnostic? _lifecycleStartupDiagnostic;
    private readonly List<EffectExecutionFrame> _frames = [];
    private IEnumerable<PendingEffect> Pending => _frames.SelectMany(x => x.Remaining);
    private ImmutableArray<PendingEffect> CurrentPending => _frames.Count == 0 ? [] : _frames[^1].Remaining;
    private DecisionFrame? Decision => _frames.Count == 0 ? null : _frames[^1].Decision;
    private readonly EffectGraphValidator _graphValidator = new();
    private readonly Dictionary<ImmutableArray<Effect>, ImmutableArray<TurnDiagnostic>> _shapes = [];
    private readonly Dictionary<(ImmutableArray<Effect> Effects, int Owner, bool Card, bool Board, bool CombatRetreat, int Depth), TurnDiagnostic?> _support = [];
    private readonly HashSet<UsedSpace> _used = [];
    private readonly List<GameAction> _history = [];
    private readonly string _initialHash;
    private readonly int _cardCount;
    private readonly Dictionary<int, int> _troopTotals;
    private readonly string _rulesIdentity;
    private int _nextEffectId;
    private int _nextFrameId;
    private int _nextWindowId;
    private bool _revealing;
    private bool _deploying;
    private int _recruited;
    private int _oldGarrison;
    private CombatOutcome? _combatOutcome;
    private ConflictRewardProgress? _conflictRewards;

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
        _lifecycleDefinitionError = ValidateLifecycleDefinitions(rules.Lifecycle);
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
            Lifecycle = rules.Lifecycle is { } lifecycle ? lifecycle with
            {
                // Bind rejected shape data to a reserved, nonexecutable identity before serializing safe enum values.
                Version = _lifecycleDefinitionError is { } invalid
                    ? InvalidLifecycleVersion + invalid.Code + ":" + invalid.Message + ":" + lifecycle.Version : lifecycle.Version,
                Intrigues = [.. OrEmpty(lifecycle.Intrigues).Where(x => x is not null).DistinctBy(x => x.Id).OrderBy(x => x.Id.Value, StringComparer.Ordinal)
                    .Select(x => x with { Timing = Enum.IsDefined(x.Timing) ? x.Timing : IntrigueTiming.Plot, Effects = Normalize(x.Effects), Sources = OrEmpty(x.Sources) })],
                Conflicts = [.. OrEmpty(lifecycle.Conflicts).Where(x => x is not null).DistinctBy(x => x.Id).OrderBy(x => x.Id.Value, StringComparer.Ordinal)
                    .Select(x => x with { Tier = Enum.IsDefined(x.Tier) ? x.Tier : ConflictTier.I, Rewards = [.. OrEmpty(x.Rewards).Where(r => r is not null).OrderBy(r => r.Tier)
                        .Select(r => r with { Effects = Normalize(r.Effects) })], Sources = OrEmpty(x.Sources) })],
                Factions = [.. OrEmpty(lifecycle.Factions).Where(x => x is not null).DistinctBy(x => x.Faction).OrderBy(x => x.Faction)
                    .Select(x => x with { Faction = Enum.IsDefined(x.Faction) ? x.Faction : Faction.Emperor, ThresholdFourReward = Normalize(x.ThresholdFourReward), Sources = OrEmpty(x.Sources) })],
                Participation = Enum.IsDefined(lifecycle.Participation) ? lifecycle.Participation : CombatParticipationPolicy.OriginalTroops,
                Cleanup = Enum.IsDefined(lifecycle.Cleanup) ? lifecycle.Cleanup : CombatCleanupPolicy.ReturnTroopsToSupply,
            } : null,
        };
        _cards = _rules.Cards.ToDictionary(x => x.Id);
        _spaces = _rules.Spaces.ToDictionary(x => x.Id);
        _leaders = _rules.Leaders.ToDictionary(x => x.Id);
        _intrigues = _rules.Lifecycle?.Intrigues.ToDictionary(x => x.Id) ?? [];
        _lifecycleStartupDiagnostic = LifecycleStartupSupport();
        _rulesIdentity = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(_rules, ConfigurationJson.Options))));
        _initialHash = state.CaptureTrustedSnapshot().Hash;
        _cardCount = state.Zones.Sum(x => x.Count);
        _troopTotals = state.Players.ToDictionary(x => x.Id,
            x => x.Units.TroopsInSupply + x.Units.TroopsInGarrison + x.Units.TroopsInConflict);
        // Replay's factory owns this synthetic initial-phase seam; its initial hash precedes automatic window opening.
        if (state.Phase is GamePhase.Combat or GamePhase.Endgame)
        {
            if (state.Authenticity != ContentAuthenticity.SyntheticFixture)
                throw new NotSupportedException("Initial lifecycle phases require an explicit synthetic fixture.");
            OpenPhaseWindow();
        }
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
        var effects = CurrentPending.Where(_ => _frames[^1].ChooserId == playerId && Decision is null)
            .Select(x => new ObservedPendingEffect(x.Id, playerId, x.Effect.GetType().Name)).ToImmutableArray();
        PendingDecisionSummary? decision = null;
        if (Decision is { } frame && frame.Context.OwnerId == playerId)
            decision = new(playerId, frame.Effect.GetType().Name, frame.Effect switch
            {
                OptionalPaymentEffect => ["Decline", "Pay"],
                ChoiceEffect choice => [.. choice.Options.Select(x => x.Label)],
                RetreatEffect retreat => [.. Enumerable.Range(0, RetreatMaximum(retreat, frame.Context) + 1)
                    .Select(x => $"Retreat {x} troops")],
                _ => throw new InvalidOperationException("Unknown pending decision."),
            });
        return new(game, effects, decision, LegalActions(playerId));
    }

    public ImmutableArray<GameAction> LegalActions(int playerId)
    {
        Player(playerId); // Validate viewer even when inactive.
        AssertInvariants();
        if (_state.DecisionOwnerId != playerId)
            return [];
        return Candidates(playerId).Where(x => Validate(x) is null).ToImmutableArray();
    }

    public ImmutableArray<TurnDiagnostic> SupportDiagnostics(int playerId)
    {
        Player(playerId);
        AssertInvariants();
        if (_state.Phase == GamePhase.BeforeRoundStart && _lifecycleStartupDiagnostic is { } startup)
            return [startup];
        if (_state.Phase != GamePhase.PlayerTurns && _state.DecisionWindow is null &&
            !(_state.Phase == GamePhase.Combat && _frames.Count > 0 &&
                _frames[0].Continuation.Kind == ExecutionContinuationKind.ConflictRewards))
            return _state.Phase is GamePhase.Combat or GamePhase.Endgame
                ? [Unsupported("lifecycle-support", "The lifecycle continuation is not implemented for this ruleset.")]
                : [];
        if (_state.DecisionOwnerId != playerId)
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
            case PlayIntrigueAction intrigue:
                PlayIntrigue(intrigue);
                break;
            case PassPriorityAction:
                PassPriority();
                break;
            default:
                throw new InvalidOperationException("Validated unknown action.");
        }
        RefreshCombatStrength();
        _history.Add(action);
        AssertInvariants();
        return new(StepStatus.Applied, []);
    }

    public TrustedTurnSnapshot CaptureTrustedSnapshot() => new(new("dune-turn-snapshot-v5", _rulesIdentity, _initialHash,
        _state.CaptureTrustedSnapshot().Data, [.. _frames], _nextFrameId, _revealing, _deploying,
        _recruited, _oldGarrison, _nextEffectId,
        [.. _used.OrderBy(x => x.PlayerId).ThenBy(x => x.Space.Value, StringComparer.Ordinal)], [.. _history])
    { Window = _state.DecisionWindow, NextWindowId = _nextWindowId, CombatOutcome = _combatOutcome, ConflictRewards = _conflictRewards is { } progress ? progress with { RemainingRewardWork = [.. Pending] } : null });

    public TurnReplay CaptureReplay() => new("dune-turn-replay-v5",
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
            var count = frame.Effect switch
            {
                ChoiceEffect choice => choice.Options.Length,
                RetreatEffect retreat => RetreatMaximum(retreat, frame.Context) + 1,
                _ => 2,
            };
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
            if (IsPostWinRewardRoot)
            {
                foreach (var selector in Selectors(Player(id).Intrigue)) yield return new PlayIntrigueAction(id, selector);
                yield return new PassPriorityAction(id);
            }
            yield break;
        }
        if (_state.DecisionWindow is not null)
        {
            foreach (var selector in Selectors(Player(id).Intrigue)) yield return new PlayIntrigueAction(id, selector);
            yield return new PassPriorityAction(id);
            yield break;
        }
        if (_state.Phase != GamePhase.PlayerTurns) yield break;
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
            if (_lifecycleStartupDiagnostic is not null) return _lifecycleStartupDiagnostic;
            if (leaderError is not null) return leaderError;
            return _state.Conflict.Deck.Count == 0 ? Content("conflict-empty", "No Conflict card is available.") : null;
        }
        if (_state.DecisionOwnerId != action.PlayerId)
            return Illegal("decision-owner", "This player does not own the current decision.");
        if (leaderError is not null) return leaderError;
        if (Decision is { } frame)
        {
            if (action is not ChooseOptionAction choose)
                return Illegal("pending-decision", "Resolve the pending decision first.");
            if (frame.Effect is RetreatEffect retreat)
                return choose.Option < 0 || choose.Option > RetreatMaximum(retreat, frame.Context)
                    ? Illegal("retreat-count", "Retreat exceeds the permitted deployed troop count.") :
                    ValidateRetreatClosure(frame.Context, choose.Option) ?? ValidateRemaining();
            if (frame.Effect is OptionalPaymentEffect && choose.Option == 0) return ValidateRemaining();
            var selected = frame.Effect switch
            {
                OptionalPaymentEffect optional when choose.Option == 1 => new EffectOption("Pay", optional.Cost, optional.Effects),
                ChoiceEffect choice when choose.Option >= 0 && choose.Option < choice.Options.Length => choice.Options[choose.Option],
                _ => null,
            };
            if (selected is null || !CanPay(Player(frame.Context.PayerId), selected.Cost))
                return Illegal("option", "This option is unavailable or unaffordable.");
            return ValidateRemaining(selected.Effects.Select(x => (x, frame.Context.RecipientId)), frame.Context.Space,
                CostPayments(frame.Context.PayerId, selected.Cost), frame.Context.PayerId);
        }
        if (_frames.Count > 0 && action is ResolveEffectAction resolveWork)
        {
            var work = CurrentPending.FirstOrDefault(x => x.Id == resolveWork.EffectId);
            return work is null ? Illegal("effect", "This pending effect is unavailable.") :
                Preflight([work.Effect], work.Context.RecipientId, work.Context.Space, payer: work.Context.PayerId) ?? ValidateRemaining();
        }
        if (_frames.Count > 0 && !IsRevealRoot && !IsPostWinRewardRoot && !_deploying)
            return Illegal("child-effects", "Complete the current frame before continuing suspended work.");
        if (_frames.Count > 1 && (action is not ResolveEffectAction immediate ||
            !CurrentPending.Any(x => x.Id == immediate.EffectId)))
            return Illegal("child-effects", "Complete the current child before continuing suspended work.");
        if (_state.DecisionWindow is { } window)
            return ValidateWindowAction(action, window);
        if (_state.Phase != GamePhase.PlayerTurns)
            return Illegal("phase", "Player Turns are not active.");
        var active = Player(_state.CurrentPlayerId!.Value);
        if (action is PlayIntrigueAction plot && Find(active.Intrigue, plot.Card) is { } plotCard &&
            _intrigues.TryGetValue(plotCard.Definition, out var plotRule) && plotRule.Timing == IntrigueTiming.Plot)
            return Unsupported("intrigue-plot-support", "Plot Intrigue play is not implemented.");
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
                var optionCost = spaceRule.PlacementOptions.IsEmpty ? new ResourceCost() : spaceRule.PlacementOptions[place.PaymentOption].Cost;
                var placementPayments = CostPayments(active.Id, new(spaceRule.Cost.Solari + optionCost.Solari,
                    spaceRule.Cost.Spice + optionCost.Spice, spaceRule.Cost.Water + optionCost.Water));
                var issue = Preflight(selectedEffects, active.Id, place.Space, placementPayments);
                if (issue is null && spaceRule.ControlReward is { } controlReward && board.ControllerId is { } controller)
                    issue = ValidateResourceBounds(selectedEffects.Select(x => (x, active.Id))
                        .Append((new GainEffect(controlReward, 1), controller)), place.Space, placementPayments);
                if (issue is not null) return issue;
                if (spaceRule.ControlReward is { } reward && !Enum.IsDefined(reward))
                    return Content("control-reward", "Invalid control reward.");
                return null;
            case RevealAction:
                if (_frames.Count > 0 || _deploying || _revealing || active.Temporary.HasRevealed)
                    return Illegal("reveal", "Reveal is unavailable while another turn is resolving.");
                var revealEffects = new List<Effect>();
                var revealResources = new List<(Effect Effect, int Owner, int Payer, BoardSpaceId? Space)>();
                foreach (var card in active.Hand.Cards)
                {
                    if (!_cards.TryGetValue(card.Definition, out var rule))
                        return Unsupported("reveal-support", "A remaining hand card has no executable Reveal definition.");
                    if (_cardErrors.TryGetValue(rule.Id, out var revealCardError)) return revealCardError;
                    if (ValidateContext(rule.RevealEffects, cardSource: true, boardSource: false, active.Id) is { } error) return error;
                    revealEffects.AddRange(rule.RevealEffects);
                    revealResources.AddRange(rule.RevealEffects.Select(x => (x, active.Id, active.Id, (BoardSpaceId?)null)));
                }
                foreach (var space in _state.Board.Spaces.Where(x => x.Occupants.Any(a => a.PlayerId == active.Id)))
                    if (_spaces.TryGetValue(space.Definition.Id, out var rule))
                    {
                        if (_spaceErrors.TryGetValue(rule.Id, out var revealSpaceError)) return revealSpaceError;
                        if (ValidateContext(rule.RevealEffects, cardSource: false, boardSource: true, active.Id) is { } error) return error;
                        revealEffects.AddRange(rule.RevealEffects);
                        revealResources.AddRange(rule.RevealEffects.Select(x => (x, active.Id, active.Id, (BoardSpaceId?)space.Definition.Id)));
                    }
                return Preflight(revealEffects, active.Id) ??
                    ValidateResourceBounds(revealResources.Concat(active.HasHighCouncilSeat
                        ? [(new GainEffect(ResourceKind.Persuasion, 2), active.Id, active.Id, (BoardSpaceId?)null)] : []));
            case DeployAction deploy:
                if (!_deploying || _frames.Count != 1 || !CurrentPending.IsEmpty ||
                    _frames[0].Continuation.Kind != ExecutionContinuationKind.AgentTurn || _revealing || deploy.Troops < 0 ||
                    deploy.Troops > Math.Min(active.Units.TroopsInGarrison, _recruited + Math.Min(2, _oldGarrison)))
                    return Illegal("deployment", "Deployment exceeds recruits plus at most two old garrison troops.");
                if (!StrengthFits(active.Temporary.Swords, active.Units.TroopsInConflict + deploy.Troops))
                    return Content("resource-bounds", "Deployment exceeds Combat strength bounds.");
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
                (int Payer, ResourceKind Resource, int Amount)[] purchasePayments = [(active.Id, ResourceKind.Persuasion, purchase.Cost)];
                return ValidateContext(purchase.AcquireEffects, cardSource: true, boardSource: false, active.Id) ??
                    ValidateSpecialOperations(Pending.Select(x => (x.Effect, x.Context.RecipientId))
                        .Concat(purchase.AcquireEffects.Select(x => (x, active.Id)))) ??
                    Preflight(purchase.AcquireEffects, active.Id, payments: purchasePayments) ??
                    ValidateResourceBounds(Pending.Select(x => (x.Effect, x.Context.RecipientId, x.Context.PayerId, x.Context.Space))
                        .Concat(purchase.AcquireEffects.Select(x => (x, active.Id, active.Id, (BoardSpaceId?)null))), purchasePayments, selected) ??
                    ValidatePermanentOperations(Pending.Select(x => (x.Effect, x.Context.RecipientId))
                        .Concat(purchase.AcquireEffects.Select(x => (x, active.Id)))) ??
                    ValidateInfluenceBounds(Pending.Select(x => (x.Effect, x.Context.RecipientId))
                        .Concat(purchase.AcquireEffects.Select(x => (x, active.Id)))) ??
                    ValidateAcquisitionStock(Pending.Select(x => (x.Effect, x.Context.RecipientId))
                        .Concat(purchase.AcquireEffects.Select(x => (x, active.Id))), selected);
            case EndRevealAction:
                if (!IsRevealRoot || Pending.Any()) return Illegal("pending-effects", "Resolve all Reveal effects before ending Reveal.");
                return _rules.Lifecycle is not null && _state.Players.All(x => x.Id == active.Id || x.Temporary.HasRevealed) &&
                    _state.Players.All(x => !CombatParticipation.IsEligible(_rules.Lifecycle.Participation, CombatUnits(x)))
                    ? PreflightConflictRewards(CalculateCombatOutcome()) : null;
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

    private TurnDiagnostic? Preflight(IEnumerable<Effect> effects, int owner, BoardSpaceId? space = null,
        IEnumerable<(int Payer, ResourceKind Resource, int Amount)>? payments = null, int? payer = null)
    {
        var materialized = effects.ToImmutableArray();
        // Executable boxes were checked with their actual source/role context before activation.
        return ValidateSpecialOperations(materialized.Select(x => (x, owner))) ??
            ValidateAcquisitionStock(materialized.Select(x => (x, owner))) ??
            ValidateResourceBounds(materialized.Select(x => (x, owner)), space, payments, payer) ??
            ValidateInfluenceBounds(materialized.Select(x => (x, owner))) ??
            ValidatePermanentOperations(materialized.Select(x => (x, owner)));
    }

    private TurnDiagnostic? Support(ImmutableArray<Effect> effects, int owner, bool card, bool board, bool combatRetreat = false, int depth = 0)
    {
        if (depth > 32) return Content("effect-cycle", "Effect or acquisition/Signet graph is cyclic or too deep.");
        var key = (effects, owner, card, board, combatRetreat, depth);
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
                RetreatEffect when !combatRetreat || _rules.Lifecycle?.Participation != CombatParticipationPolicy.OriginalTroops =>
                    Unsupported("retreat-context", "Retreat is supported only in original troop Combat Intrigues."),
                DeployFromSupplyEffect => Unsupported("supply-deployment-context", "Supply deployment is reserved for the matching Conflict defense decision."),
                ControlSpaceEffect control when !_state.Board.Spaces.Any(x => x.Definition.Id == control.Space && x.Definition.Controllable) =>
                    Content("control-space", "Control requires a controllable board space."),
                ConditionalEffect conditional => conditional.Effects.Any(x => x is not GainEffect)
                    ? Unsupported("conditional-children", "Conditional children are limited to nonnegative resource awards.")
                    : Support(conditional.Effects, owner, card, board, combatRetreat, depth + 1),
                SequenceEffect sequence => Support(sequence.Effects, owner, card, board, combatRetreat, depth + 1),
                PlayerOrderedEffect ordered => Support(ordered.Effects, owner, card, board, combatRetreat, depth + 1),
                OptionalPaymentEffect optional => ValidateRoles(optional.DecisionOwnerId, optional.PayerId, optional.RecipientId) ??
                    Support(optional.Effects, optional.RecipientId ?? optional.DecisionOwnerId ?? owner, card, board, combatRetreat, depth + 1),
                ChoiceEffect choice => choice.Options.Select(x => Support(x.Effects, owner, card, board, combatRetreat, depth + 1)).FirstOrDefault(x => x is not null),
                ForPlayerEffect target => ValidateOwner(target.PlayerId) ?? ValidateRoles(target.DecisionOwnerId, target.PayerId, target.RecipientId) ??
                    Support(target.Effects, target.RecipientId ?? target.PlayerId, card, board, combatRetreat, depth + 1),
                AcquireEffect acquire => !_cards.TryGetValue(acquire.Card, out var rule)
                    ? Unsupported("acquire-effect", "The granted card is unsupported.")
                    : _cardErrors.GetValueOrDefault(rule.Id) ?? Support(rule.AcquireEffects, owner, true, false, combatRetreat, depth + 1),
                SignetEffect => Support(_leaders[Player(owner).Leader].SignetEffects, owner, card, board, combatRetreat, depth + 1),
                InfluenceEffect influence when _rules.Lifecycle?.Factions.FirstOrDefault(x => x.Faction == influence.Faction) is { } faction =>
                    _lifecycleDefinitionError ?? Support(faction.ThresholdFourReward, owner, false, false, combatRetreat, depth + 1),
                _ => null,
            };
        }
        _support[key] = issue;
        return issue;
    }

    private TurnDiagnostic? ValidateRemaining(IEnumerable<(Effect Effect, int Owner)>? selected = null, BoardSpaceId? sourceSpace = null,
        IEnumerable<(int Payer, ResourceKind Resource, int Amount)>? payments = null, int? selectedPayer = null)
    {
        // A selected decision child finishes first, then the active frame, then its suspended parents.
        // Keep explicit Sequence order when projecting signed Influence across a suspended decision.
        var children = new List<Effect>();
        if (selected is not null)
            children.Add(new PlayerOrderedEffect([.. selected.Select(x => (Effect)new ForPlayerEffect(x.Owner, [x.Effect])
                { PayerId = selectedPayer ?? x.Owner })]));
        foreach (var pendingFrame in _frames.AsEnumerable().Reverse())
        {
            ImmutableArray<Effect> effects = [.. pendingFrame.Remaining.Select(x => (Effect)new ForPlayerEffect(x.Context.DecisionOwnerId, [x.Effect])
                { RecipientId = x.Context.RecipientId, PayerId = x.Context.PayerId })];
            children.Add(pendingFrame.Mode == EffectGroupMode.Sequence ? new SequenceEffect(effects) : new PlayerOrderedEffect(effects));
        }
        children.AddRange(FutureRewardWork().Select(x => (Effect)new ForPlayerEffect(x.Owner, [x.Effect])));
        (Effect Effect, int Owner)[] work = [(new SequenceEffect([.. children]), 0)];
        var resources = work.Select(x => (x.Effect, x.Owner, 0, sourceSpace ?? Pending.Select(x => x.Context.Space).FirstOrDefault(x => x is not null)));
        return ValidateSpecialOperations(work) ?? ValidateAcquisitionStock(work) ?? ValidateResourceBounds(resources, payments) ?? ValidateInfluenceBounds(work) ?? ValidatePermanentOperations(work);
    }

    private TurnDiagnostic? ValidatePermanentOperations(IEnumerable<(Effect Effect, int Owner)> work)
    {
        var projection = ProjectFactionWork(work.Select(x => (x.Effect, x.Owner, x.Owner, (BoardSpaceId?)null)));
        if (projection.Error is { } error) return error;
        var totals = new Dictionary<(int Owner, bool Swordmaster), int>();
        foreach (var (effect, owner, _, _) in projection.Work) AccumulatePermanentOperations([effect], owner, totals);
        foreach (var (key, count) in totals)
            if (count > (key.Swordmaster ? Player(key.Owner).Units.UnacquiredSwordmasters : Player(key.Owner).HasHighCouncilSeat ? 0 : 1))
                return Illegal("permanent-upgrade", "A selected permanent upgrade is already acquired or granted more than once.");
        return null;
    }

    private void AccumulatePermanentOperations(IEnumerable<Effect> effects, int owner, Dictionary<(int Owner, bool Swordmaster), int> target, bool includeChoices = false)
    {
        foreach (var effect in effects)
            switch (effect)
            {
                case SwordmasterEffect or HighCouncilEffect:
                    var key = (owner, effect is SwordmasterEffect);
                    target[key] = target.GetValueOrDefault(key) + 1;
                    break;
                case SequenceEffect sequence: AccumulatePermanentOperations(sequence.Effects, owner, target, includeChoices); break;
                case PlayerOrderedEffect ordered: AccumulatePermanentOperations(ordered.Effects, owner, target, includeChoices); break;
                case ForPlayerEffect player: AccumulatePermanentOperations(player.Effects, player.RecipientId ?? player.PlayerId, target, includeChoices); break;
                case AcquireEffect acquire: AccumulatePermanentOperations(_cards[acquire.Card].AcquireEffects, owner, target, includeChoices); break;
                case SignetEffect: AccumulatePermanentOperations(_leaders[Player(owner).Leader].SignetEffects, owner, target, includeChoices); break;
                case ChoiceEffect choice when includeChoices && !HasFreeEmptyOption(choice):
                    var maxima = new Dictionary<(int Owner, bool Swordmaster), int>();
                    foreach (var option in choice.Options)
                    {
                        var branch = new Dictionary<(int Owner, bool Swordmaster), int>();
                        AccumulatePermanentOperations(option.Effects, owner, branch, true);
                        foreach (var (upgrade, amount) in branch) maxima[upgrade] = Math.Max(maxima.GetValueOrDefault(upgrade), amount);
                    }
                    foreach (var (upgrade, amount) in maxima) target[upgrade] = target.GetValueOrDefault(upgrade) + amount;
                    break;
                    // Optional and choice branches are checked when selected, before payment or frame mutation.
            }
    }

    private static bool HasFreeEmptyOption(ChoiceEffect choice) =>
        choice.Options.Any(x => x.Cost == new ResourceCost() && x.Effects.IsEmpty);

    private static IEnumerable<(int Payer, ResourceKind Resource, int Amount)> CostPayments(int payer, ResourceCost cost) =>
        [(payer, ResourceKind.Solari, cost.Solari), (payer, ResourceKind.Spice, cost.Spice), (payer, ResourceKind.Water, cost.Water)];

    private TurnDiagnostic? ValidateResourceBounds(IEnumerable<(Effect Effect, int Owner)> work, BoardSpaceId? space = null,
        IEnumerable<(int Payer, ResourceKind Resource, int Amount)>? payments = null, int? payer = null) =>
        ValidateResourceBounds(work.Select(x => (x.Effect, x.Owner, payer ?? x.Owner, space)), payments);

    private TurnDiagnostic? ValidateResourceBounds(IEnumerable<(Effect Effect, int Owner, int Payer, BoardSpaceId? Space)> work,
        IEnumerable<(int Payer, ResourceKind Resource, int Amount)>? payments = null, CardInstance? consumed = null)
    {
        var projection = ProjectFactionWork(work);
        if (projection.Error is { } error) return error;
        var materialized = projection.Work;
        var totals = new Dictionary<(int Owner, ResourceKind Resource), long>();
        // These affordable, selected payments happen before all selected and suspended work.
        // Capacity is freed only on the bound payer, independently of each reward recipient.
        AddPayments(totals, payments ?? []);
        var funding = new Dictionary<(int Owner, ResourceKind Resource), long>(totals);
        foreach (var (effect, owner, payer, space) in materialized) Accumulate([effect], owner, payer, space, totals);
        if (!HasCapacity(totals)) return Content("resource-bounds", "Mandatory gains exceed resource bounds.");
        if (AmbiguousJointChoices(materialized, totals, materialized.Select(x => (x.Effect, x.Owner)), funding))
            return Unsupported("choice-joint-ambiguous", "Overlapping mandatory choices require unsupported joint funding, capacity or permanent-upgrade analysis.");
        return materialized.All(x => ChoicesHaveCapacity([x.Effect], x.Owner, x.Payer, x.Space, totals, [],
            new HashSet<Effect>(ReferenceEqualityComparer.Instance), funding)) ? null :
            Unsupported("choice-unavailable", "A mandatory choice has no provably affordable option preserving stock, capacity and permanent upgrades.");

        static void AddPayments(Dictionary<(int Owner, ResourceKind Resource), long> target,
            IEnumerable<(int Payer, ResourceKind Resource, int Amount)> costs)
        {
            foreach (var (payer, resource, amount) in costs)
                target[(payer, resource)] = target.GetValueOrDefault((payer, resource)) - amount;
        }

        bool HasCapacity(Dictionary<(int Owner, ResourceKind Resource), long> amounts)
        {
            foreach (var (key, amount) in amounts)
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
                if (current + amount < 0 || current + amount > int.MaxValue || key.Resource == ResourceKind.Swords &&
                    !StrengthFits(current + amount, player.Units.TroopsInConflict)) return false;
            }
            return true;
        }

        bool AmbiguousJointChoices(IEnumerable<(Effect Effect, int Owner, int Payer, BoardSpaceId? Space)> entries,
            Dictionary<(int Owner, ResourceKind Resource), long> amounts, IEnumerable<(Effect Effect, int Owner)> mandatoryWork,
            Dictionary<(int Owner, ResourceKind Resource), long> funds)
        {
            var worst = new Dictionary<(int Owner, ResourceKind Resource), long>(amounts);
            var capacityContributors = new Dictionary<(int Owner, ResourceKind Resource), int>();
            var paymentWorst = new Dictionary<(int Owner, ResourceKind Resource), long>();
            var paymentContributors = new Dictionary<(int Owner, ResourceKind Resource), int>();
            var permanentWorst = new Dictionary<(int Owner, bool Swordmaster), int>();
            foreach (var (effect, owner) in mandatoryWork) AccumulatePermanentOperations([effect], owner, permanentWorst);
            var permanentContributors = new Dictionary<(int Owner, bool Swordmaster), int>();
            foreach (var (choice, owner, payer, space) in entries.SelectMany(x => CapacityChoices([x.Effect], x.Owner, x.Payer, x.Space)))
            {
                // A free empty alternative proves this choice can always avoid additional demand.
                if (HasFreeEmptyOption(choice)) continue;
                var maxima = new Dictionary<(int Owner, ResourceKind Resource), long>();
                var permanentMaxima = new Dictionary<(int Owner, bool Swordmaster), int>();
                var paymentMaxima = new Dictionary<(int Owner, ResourceKind Resource), long>();
                foreach (var option in choice.Options)
                {
                    var branch = new Dictionary<(int Owner, ResourceKind Resource), long>();
                    AddPayments(branch, CostPayments(payer, option.Cost));
                    Accumulate(option.Effects, owner, payer, space, branch, true);
                    var optionPayments = new Dictionary<(int Owner, ResourceKind Resource), long>();
                    foreach (var (boundPayer, resource, amount) in CostPayments(payer, option.Cost)) optionPayments[(boundPayer, resource)] = amount;
                    MaximumPayments(option.Effects, owner, payer, optionPayments, mandatoryOnly: true);
                    foreach (var (key, amount) in optionPayments) paymentMaxima[key] = Math.Max(paymentMaxima.GetValueOrDefault(key), amount);
                    foreach (var (key, amount) in branch) maxima[key] = Math.Max(maxima.GetValueOrDefault(key), amount);
                    var permanentBranch = new Dictionary<(int Owner, bool Swordmaster), int>();
                    AccumulatePermanentOperations(option.Effects, owner, permanentBranch, true);
                    foreach (var (key, amount) in permanentBranch) permanentMaxima[key] = Math.Max(permanentMaxima.GetValueOrDefault(key), amount);
                }
                foreach (var (key, amount) in maxima.Where(x => x.Value > 0))
                {
                    capacityContributors[key] = capacityContributors.GetValueOrDefault(key) + 1;
                    worst[key] = worst.GetValueOrDefault(key) + amount;
                }
                foreach (var (key, amount) in paymentMaxima.Where(x => x.Value > 0))
                {
                    paymentContributors[key] = paymentContributors.GetValueOrDefault(key) + 1;
                    paymentWorst[key] = paymentWorst.GetValueOrDefault(key) + amount;
                }
                foreach (var (key, amount) in permanentMaxima)
                {
                    permanentContributors[key] = permanentContributors.GetValueOrDefault(key) + 1;
                    permanentWorst[key] = permanentWorst.GetValueOrDefault(key) + amount;
                }
            }
            if (paymentContributors.Any(x => x.Value > 1 && CashAvailable(x.Key, funds) < paymentWorst[x.Key])) return true;
            if (capacityContributors.Any(x => x.Value > 1 && !HasCapacity(new() { [x.Key] = worst[x.Key] }))) return true;
            return permanentContributors.Any(x => x.Value > 1 && permanentWorst[x.Key] >
                (x.Key.Swordmaster ? Player(x.Key.Owner).Units.UnacquiredSwordmasters : Player(x.Key.Owner).HasHighCouncilSeat ? 0 : 1));
        }

        IEnumerable<(ChoiceEffect Choice, int Owner, int Payer, BoardSpaceId? Space)> CapacityChoices(
            IEnumerable<Effect> effects, int owner, int payer, BoardSpaceId? space)
        {
            foreach (var effect in effects)
            {
                if (effect is ChoiceEffect choice) { yield return (choice, owner, payer, space); continue; }
                var children = effect switch
                {
                    SequenceEffect sequence => sequence.Effects,
                    PlayerOrderedEffect ordered => ordered.Effects,
                    ForPlayerEffect player => player.Effects,
                    AcquireEffect acquire => _cards[acquire.Card].AcquireEffects,
                    SignetEffect => _leaders[Player(owner).Leader].SignetEffects,
                    _ => ImmutableArray<Effect>.Empty,
                };
                var recipient = effect is ForPlayerEffect target ? target.RecipientId ?? target.PlayerId : owner;
                var childPayer = effect is ForPlayerEffect bound ? bound.PayerId ?? bound.PlayerId : payer;
                foreach (var nested in CapacityChoices(children, recipient, childPayer, effect is AcquireEffect ? null : space)) yield return nested;
            }
        }

        bool ChoicesHaveCapacity(IEnumerable<Effect> effects, int owner, int payer, BoardSpaceId? space,
            Dictionary<(int Owner, ResourceKind Resource), long> amounts, ImmutableArray<(Effect Effect, int Owner)> selectedWork,
            HashSet<Effect> selectedChoices, Dictionary<(int Owner, ResourceKind Resource), long> funds)
        {
            foreach (var effect in effects)
            {
                var available = effect switch
                {
                    SequenceEffect sequence => SequenceHasCapacity(sequence.Effects, owner, payer, space, amounts, selectedWork, selectedChoices, funds),
                    PlayerOrderedEffect ordered => ChoicesHaveCapacity(ordered.Effects, owner, payer, space, amounts, selectedWork, selectedChoices, funds),
                    ForPlayerEffect player => ChoicesHaveCapacity(player.Effects, player.RecipientId ?? player.PlayerId,
                        player.PayerId ?? player.PlayerId, space, amounts, selectedWork, selectedChoices, funds),
                    AcquireEffect acquire => ChoicesHaveCapacity(_cards[acquire.Card].AcquireEffects, owner, payer, null, amounts, selectedWork, selectedChoices, funds),
                    SignetEffect => ChoicesHaveCapacity(_leaders[Player(owner).Leader].SignetEffects, owner, payer, space, amounts, selectedWork, selectedChoices, funds),
                    ChoiceEffect choice => choice.Options.Any(option =>
                    {
                        if (!CanFund(payer, option.Cost, funds)) return false;
                        var branchFunding = new Dictionary<(int Owner, ResourceKind Resource), long>(funds);
                        AddPayments(branchFunding, CostPayments(payer, option.Cost));
                        var branch = new Dictionary<(int Owner, ResourceKind Resource), long>(amounts);
                        AddPayments(branch, CostPayments(payer, option.Cost));
                        Accumulate(option.Effects, owner, payer, space, branch);
                        var branchWork = selectedWork.AddRange(option.Effects.Select(x => (x, owner)));
                        var branchChoices = new HashSet<Effect>(selectedChoices, ReferenceEqualityComparer.Instance) { choice };
                        var mandatoryWork = materialized.Select(x => (x.Effect, x.Owner)).Concat(branchWork).ToArray();
                        return HasCapacity(branch) && ValidateAcquisitionStock(mandatoryWork, consumed, branchChoices) is null &&
                            ValidateIntrigueReservations(mandatoryWork, branchChoices) is null &&
                            ValidatePermanentOperations(mandatoryWork) is null &&
                            !AmbiguousJointChoices(option.Effects.Select(x => (x, owner, payer, space)), branch, mandatoryWork, branchFunding) &&
                            ChoicesHaveCapacity(option.Effects, owner, payer, space, branch, branchWork, branchChoices, branchFunding);
                    }),
                    _ => true,
                };
                if (!available) return false;
            }
            return true;
        }

        long CurrentCash((int Owner, ResourceKind Resource) key) => key.Resource switch
        {
            ResourceKind.Solari => Player(key.Owner).Resources.Solari,
            ResourceKind.Spice => Player(key.Owner).Resources.Spice,
            ResourceKind.Water => Player(key.Owner).Resources.Water,
            _ => 0,
        };

        long CashAvailable((int Owner, ResourceKind Resource) key, Dictionary<(int Owner, ResourceKind Resource), long> funds) =>
            CurrentCash(key) + funds.GetValueOrDefault(key);

        bool CanFund(int payer, ResourceCost cost, Dictionary<(int Owner, ResourceKind Resource), long> funds) =>
            CashAvailable((payer, ResourceKind.Solari), funds) >= cost.Solari &&
            CashAvailable((payer, ResourceKind.Spice), funds) >= cost.Spice &&
            CashAvailable((payer, ResourceKind.Water), funds) >= cost.Water;

        bool SequenceHasCapacity(ImmutableArray<Effect> effects, int owner, int payer, BoardSpaceId? space,
            Dictionary<(int Owner, ResourceKind Resource), long> amounts, ImmutableArray<(Effect Effect, int Owner)> selectedWork,
            HashSet<Effect> selectedChoices, Dictionary<(int Owner, ResourceKind Resource), long> funds)
        {
            var prefix = new Dictionary<(int Owner, ResourceKind Resource), long>(funds);
            var creditIncome = true;
            foreach (var effect in effects)
            {
                if (!ChoicesHaveCapacity([effect], owner, payer, space, amounts, selectedWork, selectedChoices, prefix)) return false;
                var suspends = MaySuspend(effect);
                ProjectCompleted(effect, owner, payer, prefix, creditIncome && !suspends);
                if (suspends) creditIncome = false;
            }
            return true;
        }

        bool MaySuspend(Effect effect) => effect switch
        {
            SequenceEffect sequence => sequence.Effects.Any(MaySuspend),
            ChoiceEffect or OptionalPaymentEffect or ForPlayerEffect or PlayerOrderedEffect or AcquireEffect or SignetEffect => true,
            _ => false,
        };

        void ProjectCompleted(Effect effect, int owner, int payer,
            Dictionary<(int Owner, ResourceKind Resource), long> funds, bool creditIncome)
        {
            if (creditIncome && effect is SequenceEffect sequence)
            {
                foreach (var child in sequence.Effects) ProjectCompleted(child, owner, payer, funds, true);
                return;
            }
            if (creditIncome && effect is GainEffect { Resource: ResourceKind.Solari or ResourceKind.Spice or ResourceKind.Water } gain)
            {
                var key = (owner, gain.Resource);
                funds[key] = funds.GetValueOrDefault(key) + gain.Amount;
                return;
            }
            // Only uninterrupted automatic Sequence prefixes supply guaranteed income. Following a
            // decision/child boundary, bound possible spending and gate funding that requires more ordering.
            var expenses = new Dictionary<(int Owner, ResourceKind Resource), long>();
            MaximumPayments([effect], owner, payer, expenses);
            foreach (var (key, amount) in expenses)
                funds[key] = Math.Max(0, CashAvailable(key, funds) - amount) - CurrentCash(key);
        }

        void MaximumPayments(IEnumerable<Effect> effects, int owner, int payer,
            Dictionary<(int Owner, ResourceKind Resource), long> target, bool mandatoryOnly = false)
        {
            foreach (var effect in effects)
            {
                switch (effect)
                {
                    case ChoiceEffect choice when !mandatoryOnly || !HasFreeEmptyOption(choice):
                        var maxima = new Dictionary<(int Owner, ResourceKind Resource), long>();
                        foreach (var option in choice.Options)
                        {
                            var branch = new Dictionary<(int Owner, ResourceKind Resource), long>();
                            foreach (var (boundPayer, resource, amount) in CostPayments(payer, option.Cost)) branch[(boundPayer, resource)] = amount;
                            MaximumPayments(option.Effects, owner, payer, branch, mandatoryOnly);
                            foreach (var (key, amount) in branch) maxima[key] = Math.Max(maxima.GetValueOrDefault(key), amount);
                        }
                        foreach (var (key, amount) in maxima) target[key] = target.GetValueOrDefault(key) + amount;
                        break;
                    case OptionalPaymentEffect optional when !mandatoryOnly:
                        var optionalPayer = optional.PayerId ?? optional.DecisionOwnerId ?? payer;
                        foreach (var (boundPayer, resource, amount) in CostPayments(optionalPayer, optional.Cost))
                            target[(boundPayer, resource)] = target.GetValueOrDefault((boundPayer, resource)) + amount;
                        MaximumPayments(optional.Effects, optional.RecipientId ?? optional.DecisionOwnerId ?? owner, optionalPayer, target);
                        break;
                    case SequenceEffect sequence: MaximumPayments(sequence.Effects, owner, payer, target, mandatoryOnly); break;
                    case PlayerOrderedEffect ordered: MaximumPayments(ordered.Effects, owner, payer, target, mandatoryOnly); break;
                    case ForPlayerEffect player: MaximumPayments(player.Effects, player.RecipientId ?? player.PlayerId, player.PayerId ?? player.PlayerId, target, mandatoryOnly); break;
                    case AcquireEffect acquire: MaximumPayments(_cards[acquire.Card].AcquireEffects, owner, payer, target, mandatoryOnly); break;
                    case SignetEffect: MaximumPayments(_leaders[Player(owner).Leader].SignetEffects, owner, payer, target, mandatoryOnly); break;
                }
            }
        }

        void Accumulate(IEnumerable<Effect> effects, int owner, int payer, BoardSpaceId? space, Dictionary<(int Owner, ResourceKind Resource), long> target, bool includeChoices = false)
        {
            foreach (var effect in effects)
                switch (effect)
                {
                    case HarvestSpiceEffect:
                        var spice = (owner, ResourceKind.Spice);
                        target[spice] = target.GetValueOrDefault(spice) + (space is { } board ? _state.Board.Spaces.Single(x => x.Definition.Id == board).BonusSpice : 0);
                        break;
                    case ConditionalEffect conditional: Accumulate(conditional.Effects, owner, payer, space, target, includeChoices); break;
                    case GainEffect gain:
                        var key = (owner, gain.Resource);
                        target[key] = target.GetValueOrDefault(key) + gain.Amount;
                        break;
                    case SequenceEffect sequence: Accumulate(sequence.Effects, owner, payer, space, target, includeChoices); break;
                    case PlayerOrderedEffect ordered: Accumulate(ordered.Effects, owner, payer, space, target, includeChoices); break;
                    case ForPlayerEffect player: Accumulate(player.Effects, player.RecipientId ?? player.PlayerId, player.PayerId ?? player.PlayerId, space, target, includeChoices); break;
                    case AcquireEffect acquire: Accumulate(_cards[acquire.Card].AcquireEffects, owner, payer, null, target, includeChoices); break;
                    case SignetEffect: Accumulate(_leaders[Player(owner).Leader].SignetEffects, owner, payer, space, target, includeChoices); break;
                    case ChoiceEffect choice when includeChoices && !HasFreeEmptyOption(choice):
                        var maxima = new Dictionary<(int Owner, ResourceKind Resource), long>();
                        foreach (var option in choice.Options)
                        {
                            var branch = new Dictionary<(int Owner, ResourceKind Resource), long>();
                            AddPayments(branch, CostPayments(payer, option.Cost));
                            Accumulate(option.Effects, owner, payer, space, branch, true);
                            foreach (var (resource, amount) in branch) maxima[resource] = Math.Max(maxima.GetValueOrDefault(resource), amount);
                        }
                        foreach (var (resource, amount) in maxima) target[resource] = target.GetValueOrDefault(resource) + amount;
                        break;
                        // Unselected optional/choice branches do not consume capacity. Once selected,
                        // their mandatory effects are passed here with the payment that precedes them.

                }
        }
    }

    private TurnDiagnostic? ValidateInfluenceBounds(IEnumerable<(Effect Effect, int Owner)> work)
    {
        return ProjectFactionWork(work.Select(x => (x.Effect, x.Owner, x.Owner, (BoardSpaceId?)null))).Error;
    }

    private TurnDiagnostic? ValidateAcquisitionStock(IEnumerable<(Effect Effect, int Owner)> work,
        CardInstance? consumed = null, IReadOnlySet<Effect>? selectedChoices = null)
    {
        var projection = ProjectFactionWork(work.Select(x => (x.Effect, x.Owner, x.Owner, (BoardSpaceId?)null)));
        if (projection.Error is { } error) return error;
        var materialized = projection.Work.Select(x => (x.Effect, x.Owner)).ToArray();
        if (ValidateIntrigueReservations(materialized, selectedChoices) is { } intrigueError) return intrigueError;
        var demand = new Dictionary<ContentId, long>();
        var mandatory = new HashSet<ContentId>();
        foreach (var (effect, owner) in materialized)
            AccumulateAcquisitionDemand([effect], owner, demand, mandatory);
        if (!HasStock(demand, mandatory))
            return Unsupported("acquire-stock", "The complete effect graph could exhaust public stock required by a mandatory acquisition.");
        if (AmbiguousJointStock(materialized, demand))
            return Unsupported("choice-stock-ambiguous", "Competing mandatory choices require unsupported joint stock feasibility analysis.");
        return materialized.All(x => ChoicesHaveStock([x.Effect], x.Owner, demand, mandatory)) ? null :
            Unsupported("choice-stock", "No option can preserve visible stock required by mandatory acquisitions.");

        bool HasStock(Dictionary<ContentId, long> counts, HashSet<ContentId> required)
        {
            foreach (var target in required)
            {
                // Only visible stock is safe to reserve; a refill cannot assume hidden deck content.
                var stock = PurchaseZones().Sum(zone => zone.Cards.Count(card => card.Definition == target &&
                    card.Id != consumed?.Id));
                if (counts.GetValueOrDefault(target) > stock) return false;
            }
            return true;
        }

        bool AmbiguousJointStock(IEnumerable<(Effect Effect, int Owner)> entries, Dictionary<ContentId, long> counts)
        {
            var worst = new Dictionary<ContentId, long>(counts);
            var competitors = new Dictionary<ContentId, int>();
            foreach (var (choice, owner) in entries.SelectMany(x => MandatoryChoices([x.Effect], x.Owner)))
            {
                if (HasFreeEmptyOption(choice)) continue;
                var maxima = new Dictionary<ContentId, long>();
                var targets = new HashSet<ContentId>();
                foreach (var option in choice.Options)
                {
                    var branch = new Dictionary<ContentId, long>();
                    AccumulateAcquisitionDemand(option.Effects, owner, branch, targets, includeChoices: true);
                    foreach (var (card, amount) in branch) maxima[card] = Math.Max(maxima.GetValueOrDefault(card), amount);
                }
                foreach (var card in targets)
                {
                    competitors[card] = competitors.GetValueOrDefault(card) + 1;
                    worst[card] = worst.GetValueOrDefault(card) + maxima.GetValueOrDefault(card);
                }
            }
            return competitors.Any(x => x.Value > 1 && !HasStock(worst, [x.Key]));
        }

        IEnumerable<(ChoiceEffect Choice, int Owner)> MandatoryChoices(IEnumerable<Effect> effects, int owner)
        {
            foreach (var effect in effects)
            {
                if (effect is ChoiceEffect choice)
                {
                    if (selectedChoices?.Contains(choice) != true) yield return (choice, owner);
                    continue;
                }
                var children = effect switch
                {
                    SequenceEffect sequence => sequence.Effects,
                    PlayerOrderedEffect ordered => ordered.Effects,
                    ForPlayerEffect player => player.Effects,
                    AcquireEffect acquire => _cards[acquire.Card].AcquireEffects,
                    SignetEffect => _leaders[Player(owner).Leader].SignetEffects,
                    _ => ImmutableArray<Effect>.Empty,
                };
                var recipient = effect is ForPlayerEffect target ? target.RecipientId ?? target.PlayerId : owner;
                foreach (var nested in MandatoryChoices(children, recipient)) yield return nested;
            }
        }

        bool ChoicesHaveStock(IEnumerable<Effect> effects, int owner, Dictionary<ContentId, long> counts,
            HashSet<ContentId> required)
        {
            foreach (var effect in effects)
            {
                var available = effect switch
                {
                    SequenceEffect sequence => ChoicesHaveStock(sequence.Effects, owner, counts, required),
                    PlayerOrderedEffect ordered => ChoicesHaveStock(ordered.Effects, owner, counts, required),
                    ForPlayerEffect player => ChoicesHaveStock(player.Effects, player.RecipientId ?? player.PlayerId, counts, required),
                    AcquireEffect acquire => ChoicesHaveStock(_cards[acquire.Card].AcquireEffects, owner, counts, required),
                    SignetEffect => ChoicesHaveStock(_leaders[Player(owner).Leader].SignetEffects, owner, counts, required),
                    ChoiceEffect choice when selectedChoices?.Contains(choice) == true => true,
                    ChoiceEffect choice => choice.Options.Any(option =>
                    {
                        var branchCounts = new Dictionary<ContentId, long>(counts);
                        var branchRequired = new HashSet<ContentId>(required);
                        AccumulateAcquisitionDemand(option.Effects, owner, branchCounts, branchRequired);
                        return HasStock(branchCounts, branchRequired) &&
                            !AmbiguousJointStock(option.Effects.Select(x => (x, owner)), branchCounts) &&
                            ChoicesHaveStock(option.Effects, owner, branchCounts, branchRequired);
                    }),
                    // An optional child may be declined; its mandatory choices are checked on selection.
                    _ => true,
                };
                if (!available) return false;
            }
            return true;
        }
    }

    private void AccumulateAcquisitionDemand(IEnumerable<Effect> effects, int owner,
        Dictionary<ContentId, long> totals, HashSet<ContentId> mandatory, int depth = 0, bool includeChoices = false)
    {
        if (depth > 32) throw new InvalidOperationException("A cyclic acquisition graph passed preflight.");
        foreach (var effect in effects)
            switch (effect)
            {
                case AcquireEffect acquire:
                    totals[acquire.Card] = totals.GetValueOrDefault(acquire.Card) + 1;
                    if (!acquire.AllowEmpty) mandatory.Add(acquire.Card);
                    AccumulateAcquisitionDemand(_cards[acquire.Card].AcquireEffects, owner, totals, mandatory, depth + 1, includeChoices);
                    break;
                case AcquisitionReservation reservation:
                    totals[reservation.Card] = totals.GetValueOrDefault(reservation.Card) + 1;
                    if (!reservation.AllowEmpty) mandatory.Add(reservation.Card);
                    break;
                case SequenceEffect sequence:
                    AccumulateAcquisitionDemand(sequence.Effects, owner, totals, mandatory, depth + 1, includeChoices);
                    break;
                case PlayerOrderedEffect ordered:
                    AccumulateAcquisitionDemand(ordered.Effects, owner, totals, mandatory, depth + 1, includeChoices);
                    break;
                case ForPlayerEffect player:
                    AccumulateAcquisitionDemand(player.Effects, player.RecipientId ?? player.PlayerId, totals, mandatory, depth + 1, includeChoices);
                    break;
                case SignetEffect:
                    AccumulateAcquisitionDemand(_leaders[Player(owner).Leader].SignetEffects, owner, totals, mandatory, depth + 1, includeChoices);
                    break;
                case ChoiceEffect choice when includeChoices && !HasFreeEmptyOption(choice):
                    var maxima = new Dictionary<ContentId, long>();
                    foreach (var option in choice.Options)
                    {
                        var branch = new Dictionary<ContentId, long>();
                        AccumulateAcquisitionDemand(option.Effects, owner, branch, mandatory, depth + 1, true);
                        foreach (var (card, amount) in branch) maxima[card] = Math.Max(maxima.GetValueOrDefault(card), amount);
                    }
                    foreach (var (card, amount) in maxima) totals[card] = totals.GetValueOrDefault(card) + amount;
                    break;
                    // Optional and choice branches reserve stock only after selection. Support for every
                    // branch was checked before activation; selected work is combined with suspended frames.

            }
    }

    private TurnDiagnostic? ValidateContext(IEnumerable<Effect> effects, bool cardSource, bool boardSource, int owner,
        bool combatRetreat = false) => Support(effects.ToImmutableArray(), owner, cardSource, boardSource, combatRetreat);

    private TurnDiagnostic? ValidateWindowAction(GameAction action, DecisionWindowState window)
    {
        if (window.SuspendedFrameId is not null || _frames.Count > 0 && !IsPostWinRewardRoot)
            return Illegal("window-suspended", "Complete the current Intrigue before using priority.");
        if (action is PassPriorityAction)
        {
            if (IsPostWinRewardRoot && !CurrentPending.IsEmpty)
                return Illegal("mandatory-rewards", "Complete all mandatory rewards before closing this opportunity.");
            return window.Kind == DecisionWindowKind.Combat && window.OpportunityPlays == 0 &&
                window.ConsecutivePasses.Length + 1 == window.EligibleSeats.Length
                ? PreflightConflictRewards(CalculateCombatOutcome()) : null;
        }
        if (action is not PlayIntrigueAction play)
            return Illegal("window-action", "This window accepts only Intrigue plays and priority passes.");
        var card = Find(Player(play.PlayerId).Intrigue, play.Card);
        if (card is null) return Illegal("intrigue-card", "The selected own Intrigue is unavailable.");
        if (!_intrigues.TryGetValue(card.Definition, out var rule))
            return Unsupported("intrigue-support", "The selected Intrigue has no executable definition.");
        if (_lifecycleDefinitionError is not null) return _lifecycleDefinitionError;
        var timing = window.Kind switch
        {
            DecisionWindowKind.Combat => IntrigueTiming.Combat,
            DecisionWindowKind.PostWinRewards => IntrigueTiming.PostWin,
            DecisionWindowKind.Endgame => IntrigueTiming.Endgame,
            _ => throw new InvalidOperationException("Unknown decision window kind."),
        };
        if (rule.Timing != timing) return Illegal("intrigue-timing", "This Intrigue cannot be played in the current window.");
        if (ValidateCost(rule.Cost) is { } costError) return costError;
        if (!CanPay(Player(play.PlayerId), rule.Cost)) return Illegal("intrigue-cost", "This Intrigue is unaffordable.");
        return ValidateContext(rule.Effects, cardSource: true, boardSource: false, play.PlayerId,
            combatRetreat: window.Kind == DecisionWindowKind.Combat) ??
            Preflight(rule.Effects, play.PlayerId, payments: CostPayments(play.PlayerId, rule.Cost)) ??
            ValidateRemaining(rule.Effects.Select(x => (x, play.PlayerId)), payments: CostPayments(play.PlayerId, rule.Cost));
    }

    private void OpenPhaseWindow()
    {
        if (_rules.Lifecycle is null) return;
        if (_lifecycleDefinitionError is not null || _rules.Lifecycle.Participation != CombatParticipationPolicy.OriginalTroops ||
            _rules.Lifecycle.Cleanup != CombatCleanupPolicy.ReturnTroopsToSupply)
            return; // Unsupported lifecycle policy leaves an explicit nonterminal stop.
        RefreshCombatStrength();
        var kind = _state.Phase == GamePhase.Endgame ? DecisionWindowKind.Endgame : DecisionWindowKind.Combat;
        var eligible = _state.Players.Where(x => kind == DecisionWindowKind.Endgame ||
                CombatParticipation.IsEligible(_rules.Lifecycle.Participation, CombatUnits(x)))
            .OrderBy(x => (x.Id - _state.FirstPlayerId + _state.Players.Count) % _state.Players.Count)
            .Select(x => x.Id).ToImmutableArray();
        OpenWindow(kind, eligible, new(kind == DecisionWindowKind.Endgame
            ? ExecutionContinuationKind.Endgame : ExecutionContinuationKind.ConflictRewards));
    }

    private void OpenWindow(DecisionWindowKind kind, ImmutableArray<int> eligible, ExecutionContinuation continuation)
    {
        if (_state.DecisionWindow is not null || _frames.Count > 0 || _state.CurrentPlayerId is not null)
            throw new InvalidOperationException("A lifecycle window cannot replace existing decisions or a turn.");
        var id = ++_nextWindowId;
        _state.DecisionOwnerId = null;
        if (eligible.IsEmpty)
        {
            if (kind == DecisionWindowKind.Combat)
            {
                FreezeCombatOutcome();
                BeginConflictRewards();
            }
            return;
        }
        _state.DecisionWindow = new(id, kind, eligible, 0, eligible[0], 0, [], continuation);
        _state.DecisionOwnerId = eligible[0];
    }

    private void PlayIntrigue(PlayIntrigueAction action)
    {
        var window = _state.DecisionWindow!;
        var player = Player(action.PlayerId);
        var card = Find(player.Intrigue, action.Card)!;
        var rule = _intrigues[card.Definition];
        Pay(player, rule.Cost);
        player.Intrigue.MoveTo(card.Id, _state.Shared.IntrigueResolving);
        var continuation = new ExecutionContinuation(window.Kind == DecisionWindowKind.Endgame
            ? ExecutionContinuationKind.Endgame : ExecutionContinuationKind.CombatOpportunity, player.Id, window.Id);
        PushFrame(rule.Effects, RootContext(player.Id, card.Id, card.Definition, null, EffectProvenance.Card),
            EffectGroupMode.PlayerOrdered, continuation);
        _state.DecisionWindow = window with
        {
            OpportunityPlays = checked(window.OpportunityPlays + 1),
            ConsecutivePasses = [],
            SuspendedFrameId = _frames[^1].Id,
            ResolvingCardId = card.Id,
        };
        ContinueTurn();
    }

    private void ResumeWindow(ExecutionContinuation continuation)
    {
        var window = _state.DecisionWindow;
        if (window is null || continuation.WindowId != window.Id || continuation.PlayerId != window.PriorityOwnerId ||
            window.SuspendedFrameId is null ||
            (window.RewardFrameId is { } rewardRoot ? _frames.Count != 1 || _frames[0].Id != rewardRoot : _frames.Count > 0))
            throw new InvalidOperationException("Intrigue continuation lost its suspended priority window.");
        if (window.ResolvingCardId is { } card && _state.Shared.IntrigueResolving.Cards.Any(x => x.Id == card))
            _state.Shared.IntrigueResolving.MoveTo(card, _state.Shared.IntrigueDiscard);
        _state.DecisionWindow = window with { SuspendedFrameId = null, ResolvingCardId = null };
        _state.DecisionOwnerId = window.PriorityOwnerId;
        if (window.Kind == DecisionWindowKind.Combat)
        {
            var eligible = window.EligibleSeats.Where(id =>
                CombatParticipation.IsEligible(_rules.Lifecycle!.Participation, CombatUnits(Player(id)))).ToImmutableArray();
            if (eligible.IsEmpty)
            {
                CloseWindow(_state.DecisionWindow);
                return;
            }
            var owner = eligible.Contains(window.PriorityOwnerId) ? window.PriorityOwnerId : eligible
                .OrderBy(id => (id - window.PriorityOwnerId + _state.Players.Count) % _state.Players.Count).First();
            _state.DecisionWindow = _state.DecisionWindow with
            {
                EligibleSeats = eligible,
                ClockwiseCursor = eligible.IndexOf(owner),
                PriorityOwnerId = owner,
                OpportunityPlays = owner == window.PriorityOwnerId ? window.OpportunityPlays : 0,
            };
            _state.DecisionOwnerId = owner;
        }
    }

    private void PassPriority()
    {
        var window = _state.DecisionWindow!;
        if (window.Kind == DecisionWindowKind.PostWinRewards)
        {
            if (IsPostWinRewardRoot) PopFrame();
            _state.DecisionWindow = null;
            _conflictRewards = _conflictRewards! with
            { ClosedPostWinOpportunities = _conflictRewards.ClosedPostWinOpportunities.Add(window.PriorityOwnerId) };
            AdvanceConflictRecipient();
            ContinueTurn();
            return;
        }
        // Finishing a played opportunity differs from passing an opportunity without playing.
        var passes = window.OpportunityPlays > 0 ? ImmutableArray<int>.Empty :
            window.ConsecutivePasses.Add(window.PriorityOwnerId);
        if (passes.Length == window.EligibleSeats.Length)
        {
            CloseWindow(window);
            return;
        }
        var cursor = (window.ClockwiseCursor + 1) % window.EligibleSeats.Length;
        _state.DecisionWindow = window with
        {
            ClockwiseCursor = cursor,
            PriorityOwnerId = window.EligibleSeats[cursor],
            OpportunityPlays = 0,
            ConsecutivePasses = passes,
        };
        _state.DecisionOwnerId = window.EligibleSeats[cursor];
    }

    private void CloseWindow(DecisionWindowState window)
    {
        if (window.SuspendedFrameId is not null || _frames.Count > 0)
            throw new InvalidOperationException("A window cannot close while Intrigue work is suspended.");
        if (window.Kind == DecisionWindowKind.Combat) FreezeCombatOutcome();
        _state.DecisionWindow = null;
        _state.DecisionOwnerId = null;
        if (window.Kind == DecisionWindowKind.Combat)
        {
            BeginConflictRewards();
            ContinueTurn();
        }
    }

    private void FreezeCombatOutcome()
    {
        if (_combatOutcome is not null)
            throw new InvalidOperationException("Combat outcome is already frozen.");
        _combatOutcome = CalculateCombatOutcome();
    }

    private CombatOutcome CalculateCombatOutcome() => CombatOutcome.Calculate([.. _state.Players.Select(player =>
            new CombatParticipant(player.Id, CombatStrength.Calculate(CombatUnits(player), player.Temporary.Swords,
                _rules.Strength.SwordStrength, _rules.Strength.RequiresDeployedUnits)))], _state.Players.Count);

    private void BeginRound()
    {
        _state.Round = 1;
        _state.Conflict.Deck.TryDrawTo(_state.Conflict.Revealed);
        _state.Phase = GamePhase.RoundStart;
        if (MatchingDefenseOwner() is { } controller)
        {
            PushFrame([new OptionalPaymentEffect(new(), [new DeployFromSupplyEffect(1)])],
                RootContext(controller, null, _state.Conflict.Revealed.Cards.Single().Definition, null, EffectProvenance.Board),
                EffectGroupMode.Sequence, new(ExecutionContinuationKind.RoundStart));
            ContinueTurn();
            return;
        }
        FinishRoundStart();
    }

    private void FinishRoundStart()
    {
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
                ApplyInfluence(influence, context);
                break;
            case DrawIntrigueEffect draw:
                for (var i = 0; i < draw.Amount; i++)
                    if (_state.Shared.IntrigueDeck.TryDrawTo(player.Intrigue) is null)
                        throw new InvalidOperationException("Unavailable Intrigue draw passed preflight.");
                break;
            case ControlSpaceEffect control:
                ApplyControl(control.Space, player);
                break;
            case TakeMentatEffect mentat:
                _state.Board.Mentat = new(null, player.Id)
                { ProtectedThroughRound = mentat.ForNextRound ? checked(_state.Round + 1) : null };
                player.Units = player.Units with { AvailableAgents = checked(player.Units.AvailableAgents + 1) };
                break;
            case DeployFromSupplyEffect deploy:
                player.Units = player.Units with
                { TroopsInSupply = player.Units.TroopsInSupply - deploy.Amount, TroopsInConflict = player.Units.TroopsInConflict + deploy.Amount };
                break;
            case ConditionalEffect conditional:
                if (ConditionHolds(conditional.Condition, player.Id))
                    PushChild(conditional.Effects, context, EffectGroupMode.PlayerOrdered);
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
            case RetreatEffect retreat:
                _frames[^1] = _frames[^1] with { Decision = new(retreat, context) };
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
            case RetreatEffect:
                var player = Player(frame.Context.RecipientId);
                player.Units = player.Units with
                {
                    TroopsInConflict = player.Units.TroopsInConflict - index,
                    TroopsInGarrison = player.Units.TroopsInGarrison + index,
                };
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

    private bool IsPostWinRewardRoot => _frames.Count == 1 &&
        _state.DecisionWindow is { Kind: DecisionWindowKind.PostWinRewards, SuspendedFrameId: null } window &&
        window.RewardFrameId == _frames[0].Id;

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
            case ExecutionContinuationKind.ConflictRewards:
                AdvanceConflictRecipient();
                break;
            case ExecutionContinuationKind.RoundStart:
                FinishRoundStart();
                break;
            case ExecutionContinuationKind.CombatOpportunity:
            case ExecutionContinuationKind.Endgame:
                ResumeWindow(continuation);
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
            if (IsPostWinRewardRoot) break; // Explicit remaining awards can fund cards, and cards can fund awards.
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
                    if (_frames[^1].Id != frame.Id) break;
                }
                continue;
            }
            if (available.Length != 1 || IsRevealRoot ||
                available[0].Effect is not (GainEffect or DrawEffect or DrawIntrigueEffect or RecruitEffect or InfluenceEffect or
                    HarvestSpiceEffect or HighCouncilEffect or SwordmasterEffect))
                break;
            var work = Take(available[0].Id);
            Execute(work.Effect, work.Context);
        }
        if (Decision is { } decision) _state.DecisionOwnerId = decision.Context.DecisionOwnerId;
        else if (_frames.Count > 0) _state.DecisionOwnerId = _frames[^1].ChooserId;
        else if (_state.DecisionWindow is { } window) _state.DecisionOwnerId = window.PriorityOwnerId;
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
            CombatUnits(player),
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
        _state.Phase = GamePhase.Combat;
        _state.CurrentPlayerId = null;
        _state.DecisionOwnerId = null;
        OpenPhaseWindow();
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
        if ((_state.Phase == GamePhase.PlayerTurns) != (_state.CurrentPlayerId is not null))
            throw new InvalidOperationException("Player Turns require a current turn player; other phases cannot have one.");
        if (_frames.Count > 0 && _state.Phase != GamePhase.PlayerTurns && _state.DecisionWindow is null &&
            _frames[0].Continuation.Kind is not (ExecutionContinuationKind.ConflictRewards or ExecutionContinuationKind.RoundStart))
            throw new InvalidOperationException("Lifecycle effect frames require their suspended window.");
        var expectedOwner = Decision?.Context.DecisionOwnerId ??
            (_frames.Count > 0 ? _frames[^1].ChooserId : _state.DecisionWindow?.PriorityOwnerId ??
                (_state.Phase == GamePhase.PlayerTurns ? _state.CurrentPlayerId : null));
        if (_state.DecisionOwnerId != expectedOwner)
            throw new InvalidOperationException("Phase, frame and decision owner are out of sync.");
        if (_state.DecisionWindow is { } window &&
            (_state.Phase != (window.Kind == DecisionWindowKind.Endgame ? GamePhase.Endgame : GamePhase.Combat) ||
             window.EligibleSeats.IsEmpty || window.EligibleSeats.Distinct().Count() != window.EligibleSeats.Length ||
             window.EligibleSeats.Any(x => !_state.Players.Any(p => p.Id == x)) || window.ClockwiseCursor < 0 ||
             window.ClockwiseCursor >= window.EligibleSeats.Length ||
             window.EligibleSeats[window.ClockwiseCursor] != window.PriorityOwnerId ||
             window.OpportunityPlays < 0 || window.ConsecutivePasses.Length >= window.EligibleSeats.Length ||
             window.ConsecutivePasses.Distinct().Count() != window.ConsecutivePasses.Length ||
             window.ConsecutivePasses.Any(x => !window.EligibleSeats.Contains(x)) ||
             (window.SuspendedFrameId is { } suspended
                 ? window.ResolvingCardId is null || !_frames.Any(x => x.Id == suspended && x.ParentId == window.RewardFrameId &&
                     x.Continuation.WindowId == window.Id && x.Continuation.PlayerId == window.PriorityOwnerId)
                 : _frames.Count > 0 && !(window.RewardFrameId is { } rewardRoot && _frames[0].Id == rewardRoot &&
                     _frames[0].Continuation.Kind == ExecutionContinuationKind.ConflictRewards) || window.ResolvingCardId is not null)))
            throw new InvalidOperationException("Decision window state is inconsistent with its phase or suspended work.");
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

    private IEnumerable<CombatUnitContribution> CombatUnits(PlayerState player) =>
        [new("troop", player.Units.TroopsInConflict, _rules.Strength.TroopStrength)];

    private int RetreatMaximum(RetreatEffect retreat, EffectContext context) =>
        Math.Min(retreat.Maximum, Player(context.RecipientId).Units.TroopsInConflict);

    private bool StrengthFits(long swords, int troops) =>
        _rules.Strength.RequiresDeployedUnits && troops == 0 ||
        (long)troops * _rules.Strength.TroopStrength + swords * _rules.Strength.SwordStrength <= int.MaxValue;

    private void RefreshCombatStrength()
    {
        foreach (var player in _state.Players)
            player.CombatMarker = CombatStrength.Calculate(CombatUnits(player), player.Temporary.Swords,
                _rules.Strength.SwordStrength, _rules.Strength.RequiresDeployedUnits);
    }

    private TurnDiagnostic? ValidateLifecycleDefinitions(LifecycleRuleSet? lifecycle)
    {
        if (lifecycle is null) return null;
        var intrigues = OrEmpty(lifecycle.Intrigues);
        var conflicts = OrEmpty(lifecycle.Conflicts);
        var factions = OrEmpty(lifecycle.Factions);
        if (string.IsNullOrWhiteSpace(lifecycle.Version) || lifecycle.Version.StartsWith(InvalidLifecycleVersion, StringComparison.Ordinal) ||
            !Enum.IsDefined(lifecycle.Participation) || !Enum.IsDefined(lifecycle.Cleanup) ||
            intrigues.Any(x => x is null) || conflicts.Any(x => x is null) || factions.Any(x => x is null))
            return Content("lifecycle-shape", "Lifecycle version, policy or definition is invalid.");
        if (intrigues.Select(x => x.Id).Concat(conflicts.Select(x => x.Id)).Distinct().Count() != intrigues.Length + conflicts.Length ||
            factions.Select(x => x.Faction).Distinct().Count() != factions.Length)
            return Content("lifecycle-shape", "Lifecycle definitions must be unique.");
        foreach (var rule in intrigues)
            if (!ContentCatalog.ValidId(rule.Id.Value) || !Enum.IsDefined(rule.Timing) || ValidateCost(rule.Cost) is not null ||
                !ValidLifecycleSources(rule.Id, rule.Sources) ||
                !rule.Id.Value.StartsWith("fixture:", StringComparison.Ordinal) &&
                    (!rule.Id.Value.StartsWith("imperium:intrigue:", StringComparison.Ordinal) ||
                     !rule.Sources.Any(x => x.Document == "DUNE_IMPERIUM_Rules_2020_10_26.pdf" && (rule.Timing switch
                     { IntrigueTiming.Combat => x.Page is 7 or 12, IntrigueTiming.PostWin => x.Page == 13, _ => x.Page == 7 }))))
                return Content("lifecycle-shape", "Intrigue identity, timing, cost or source provenance is invalid.");
        foreach (var rule in conflicts)
            if (!ContentCatalog.ValidId(rule.Id.Value) || !Enum.IsDefined(rule.Tier) || !ValidLifecycleSources(rule.Id, rule.Sources) ||
                !rule.Id.Value.StartsWith("fixture:", StringComparison.Ordinal) &&
                    (!rule.Id.Value.StartsWith("imperium:conflict:", StringComparison.Ordinal) ||
                     !rule.Sources.Any(x => x.Document == "DUNE_IMPERIUM_Rules_2020_10_26.pdf" && x.Page == 12)) ||
                OrEmpty(rule.Rewards).Length != 3 || OrEmpty(rule.Rewards).Any(x => x is null) ||
                !OrEmpty(rule.Rewards).Select(x => x.Tier).Order().SequenceEqual(new[] { 1, 2, 3 }) ||
                rule.ControlSpace is { } control && !_state.Board.Definition.Spaces.Any(x => x.Id == control && x.Controllable))
                return Content("lifecycle-shape", "Conflict identity, deck tier, reward positions, control space or source provenance is invalid.");
        foreach (var rule in factions)
            if (!Enum.IsDefined(rule.Faction) || rule.MaximumInfluence != 6 ||
                !ContentCatalog.ValidSources(rule.Sources) ||
                !(rule.Sources.All(OfficialSource) || _state.Authenticity == ContentAuthenticity.SyntheticFixture &&
                    rule.Sources.All(x => x.Document.StartsWith("Synthetic", StringComparison.Ordinal))))
                return Content("lifecycle-shape", "Faction, influence maximum or sources are invalid.");
        return null;
    }

    private bool ValidLifecycleSources(ContentId id, ImmutableArray<SourceReference> sources)
    {
        if (!ContentCatalog.ValidSources(sources)) return false;
        return id.Value.StartsWith("fixture:", StringComparison.Ordinal)
            ? _state.Authenticity == ContentAuthenticity.SyntheticFixture &&
                sources.All(x => x.Document.StartsWith("Synthetic", StringComparison.Ordinal))
            : id.Value.StartsWith("imperium:", StringComparison.Ordinal) && sources.All(OfficialSource);
    }

    private static bool OfficialSource(SourceReference source) => source.Document switch
    {
        "DUNE_IMPERIUM_Rules_2020_10_26.pdf" => source.Page is >= 1 and <= 16,
        "DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf" => source.Page is >= 1 and <= 2,
        _ => false,
    };

    private TurnDiagnostic? LifecycleStartupSupport()
    {
        if (_rules.Lifecycle is not { } lifecycle) return null;
        if (_lifecycleDefinitionError is not null) return _lifecycleDefinitionError;
        if (_state.Players.Select(LeaderSupport).FirstOrDefault(x => x is not null) is { } leaderError)
            return leaderError;
        var conflicts = lifecycle.Conflicts.ToDictionary(x => x.Id);
        // Use the configured inventory, never a hidden card's position or the current player's hand.
        // Diagnostics deliberately disclose neither the definition, its category, nor the number of failures.
        var enabled = _state.Definition.Content.Where(x => x.Quantity > 0 &&
            x.Category is ContentCategory.IntrigueCard or ContentCategory.ConflictCard);
        foreach (var definition in enabled)
        {
            if (definition.Category == ContentCategory.IntrigueCard)
            {
                if (!_intrigues.TryGetValue(definition.Id, out var rule) || rule.Timing == IntrigueTiming.Plot)
                    return Unsupported("lifecycle-deck-support", "An enabled deck definition is unsupported.");
                foreach (var player in _state.Players)
                    if (ValidateContext(rule.Effects, true, false, player.Id,
                        combatRetreat: rule.Timing == IntrigueTiming.Combat) is { } error) return DeckDiagnostic(error);
            }
            else
            {
                if (!conflicts.TryGetValue(definition.Id, out var rule))
                    return Unsupported("lifecycle-deck-support", "An enabled deck definition is unsupported.");
                if (definition.ConflictTier != rule.Tier)
                    return Content("lifecycle-deck-content", "An enabled deck definition is invalid.");
                foreach (var reward in rule.Rewards)
                    foreach (var player in _state.Players)
                        if (ValidateContext(reward.Effects, false, false, player.Id) is { } error) return DeckDiagnostic(error);
            }
        }
        // Fixtures explicitly move unused original starters outside the enabled decks. Validate all remaining
        // physical card definitions, including hidden stock, without pruning or inspecting a top-card identity.
        var cardIds = _state.Zones.Where(x => x != _state.Shared.UnusedStarters && x != _state.Conflict.Unused)
            .SelectMany(x => x.Cards).Select(x => x.Definition).ToHashSet();
        if (cardIds.Except(_state.Definition.Content.Select(x => x.Id)).Any())
            return Unsupported("lifecycle-deck-support", "An enabled deck definition is unsupported.");
        foreach (var definition in _state.Definition.Content.Where(x => cardIds.Contains(x.Id) &&
            x.Category is ContentCategory.StartingCard or ContentCategory.ImperiumCard or ContentCategory.ReserveCard))
        {
            if (!_cards.TryGetValue(definition.Id, out var rule))
                return Unsupported("lifecycle-deck-support", "An enabled deck definition is unsupported.");
            if (_cardErrors.TryGetValue(definition.Id, out var error)) return DeckDiagnostic(error);
            foreach (var player in _state.Players)
                foreach (var box in new[] { rule.AgentEffects, rule.RevealEffects, rule.AcquireEffects })
                    if (ValidateContext(box, true, false, player.Id) is { } issue) return DeckDiagnostic(issue);
        }
        foreach (var faction in lifecycle.Factions)
        {
            foreach (var player in _state.Players)
                if (ValidateContext(faction.ThresholdFourReward, false, false, player.Id) is { } issue) return DeckDiagnostic(issue);
        }
        return null;
    }

    private static TurnDiagnostic DeckDiagnostic(TurnDiagnostic error) => error.Status == StepStatus.ContentError
        ? Content("lifecycle-deck-content", "An enabled deck definition is invalid.")
        : Unsupported("lifecycle-deck-support", "An enabled deck definition is unsupported.");

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
            ConditionalEffect conditional => conditional with { Effects = Normalize(conditional.Effects, depth + 1) },
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
