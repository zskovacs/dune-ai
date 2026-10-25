using System.Collections.Immutable;
using Dune.Engine.Content;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;

namespace Dune.Engine.Turns;

public sealed partial class TurnEngine
{
    // Preflight-only stock marker. Never enters execution, snapshots or the serialized effect allowlist.
    private sealed record AcquisitionReservation(ContentId Card, bool AllowEmpty) : Effect;

    private FactionRule? FactionDefinition(Faction faction) =>
        _rules.Lifecycle?.Factions.FirstOrDefault(x => x.Faction == faction);

    private static int BoundedInfluence(int before, int amount, int maximum) =>
        (int)Math.Clamp((long)before + amount, 0, maximum);

    private void ApplyInfluence(InfluenceEffect effect, EffectContext context)
    {
        var player = Player(context.RecipientId);
        var before = Influence(player, effect.Faction);
        var after = BoundedInfluence(before, effect.Amount, FactionDefinition(effect.Faction)?.MaximumInfluence ?? 6);
        var token = _state.Board.Alliances.Single(x => x.Faction == effect.Faction);
        if (token.OwnerId == player.Id && after < before && (after < 4 || _state.Players.Any(p => p.Id != player.Id &&
            Influence(p, effect.Faction) >= Math.Max(4, after))))
            throw new InvalidOperationException("Unresolved Alliance holder loss passed preflight.");
        player.Influence = [.. player.Influence.Select(x => x.Faction == effect.Faction ? x with { Amount = after } : x)];
        Gain(player, ResourceKind.VictoryPoints, (after >= 2 ? 1 : 0) - (before >= 2 ? 1 : 0));
        int? nextOwner = token.OwnerId;
        if (after >= 4 && (token.OwnerId is null || token.OwnerId != player.Id &&
                 after > Influence(Player(token.OwnerId.Value), effect.Faction))) nextOwner = player.Id;
        if (nextOwner != token.OwnerId)
        {
            if (token.OwnerId is { } previous) Gain(Player(previous), ResourceKind.VictoryPoints, -1);
            if (nextOwner is { } next) Gain(Player(next), ResourceKind.VictoryPoints, 1);
            _state.Board.Alliances = [.. _state.Board.Alliances.Select(x => x.Faction == effect.Faction ? x with { OwnerId = nextOwner } : x)];
        }
        if (before < 4 && after >= 4)
            PushChild(FactionDefinition(effect.Faction)!.ThresholdFourReward, context, EffectGroupMode.PlayerOrdered);
    }

    private sealed class FactionProjection
    {
        internal Dictionary<(int Owner, Faction Faction), int> Influence { get; init; } = [];
        internal Dictionary<Faction, int?> Alliances { get; init; } = [];
        internal HashSet<(int Owner, Faction Faction)> Uncertain { get; init; } = [];
        internal HashSet<Faction> UncertainAlliances { get; init; } = [];
        internal Dictionary<int, (long Min, long Max)> VictoryPoints { get; init; } = [];
        internal FactionProjection Copy() => new()
        {
            Influence = new(Influence),
            Alliances = new(Alliances),
            Uncertain = new(Uncertain),
            UncertainAlliances = new(UncertainAlliances),
            VictoryPoints = new(VictoryPoints),
        };
    }

    /// <summary>Expand sourced threshold children for the existing bounded safety checks, without touching episode state.</summary>
    private (ImmutableArray<(Effect Effect, int Owner, int Payer, BoardSpaceId? Space)> Work, TurnDiagnostic? Error)
        ProjectFactionWork(IEnumerable<(Effect Effect, int Owner, int Payer, BoardSpaceId? Space)> work)
    {
        var materialized = work.ToImmutableArray();
        if (!materialized.Any(x => InfluenceFootprint(x.Effect, x.Owner, 0).Any())) return (materialized, null);
        var projection = new FactionProjection
        {
            Influence = _state.Players.SelectMany(p => p.Influence.Select(x => ((p.Id, x.Faction), x.Amount))).ToDictionary(),
            Alliances = _state.Board.Alliances.ToDictionary(x => x.Faction, x => x.OwnerId),
            VictoryPoints = _state.Players.ToDictionary(x => x.Id, x => ((long)x.Resources.VictoryPoints, (long)x.Resources.VictoryPoints)),
        };
        TurnDiagnostic? error = null;
        CheckUnordered(materialized.Select(x => ((Effect)new ForPlayerEffect(x.Owner, [x.Effect]) { PayerId = x.Payer }, x.Owner)));
        var result = materialized.Select(x => (Expand(x.Effect, x.Owner, projection, 0), x.Owner, x.Payer, x.Space)).ToImmutableArray();
        return (result, error);

        void ChangeVp(FactionProjection state, int owner, int amount, bool check = true)
        {
            var (min, max) = state.VictoryPoints[owner];
            min += amount;
            max += amount;
            if (check && (min < 0 || max > int.MaxValue)) error ??= Content("resource-bounds", "Influence crosses victory point bounds.");
            state.VictoryPoints[owner] = (min, max);
        }

        void Merge(FactionProjection destination, IEnumerable<FactionProjection> branches)
        {
            var states = branches.ToArray();
            foreach (var key in destination.Influence.Keys.ToArray())
                if (states.Any(x => x.Uncertain.Contains(key)) || states.Select(x => x.Influence[key]).Distinct().Count() > 1)
                    destination.Uncertain.Add(key);
                else destination.Influence[key] = states[0].Influence[key];
            foreach (var faction in destination.Alliances.Keys.ToArray())
                if (states.Any(x => x.UncertainAlliances.Contains(faction)) || states.Select(x => x.Alliances[faction]).Distinct().Count() > 1)
                    destination.UncertainAlliances.Add(faction);
                else destination.Alliances[faction] = states[0].Alliances[faction];
            foreach (var owner in destination.VictoryPoints.Keys.ToArray())
                destination.VictoryPoints[owner] = (states.Min(x => x.VictoryPoints[owner].Min), states.Max(x => x.VictoryPoints[owner].Max));
        }

        void CheckUnordered(IEnumerable<(Effect Effect, int Owner)> entries)
        {
            var siblings = entries.Select(x => InfluenceFootprint(x.Effect, x.Owner, 0).ToArray()).ToArray();
            foreach (var faction in siblings.SelectMany(x => x).Select(x => x.Faction).Distinct())
            {
                var involved = siblings.Where(x => x.Any(i => i.Faction == faction)).ToArray();
                if (involved.Length < 2) continue;
                var influences = involved.SelectMany(x => x).Where(x => x.Faction == faction).ToArray();
                if (influences.Any(x => x.Amount < 0) || influences.Select(x => x.Owner).Distinct().Count() > 1 ||
                    influences.Select(x => x.Payer).Distinct().Count() > 1)
                    error ??= Unsupported("influence-order", "Competing signed or differently bound Influence requires an explicit Sequence.");
            }
        }

        Effect Expand(Effect effect, int owner, FactionProjection state, int depth, bool mandatory = true)
        {
            if (depth > 32)
            {
                error ??= Content("effect-cycle", "Threshold reward graph is cyclic or too deep.");
                return effect;
            }
            ImmutableArray<Effect> Children(IEnumerable<Effect> children, int recipient) =>
                [.. children.Select(x => Expand(x, recipient, state, depth + 1, mandatory))];
            switch (effect)
            {
                case InfluenceEffect influence:
                    if (_lifecycleDefinitionError is { } invalid) { error ??= invalid; return new SequenceEffect([]); }
                    var key = (owner, influence.Faction);
                    if (state.Uncertain.Contains(key) || state.UncertainAlliances.Contains(influence.Faction))
                    {
                        error ??= Unsupported("influence-branch", "Influence following a conditional ownership or track change requires unsupported branch analysis.");
                        return new SequenceEffect([]);
                    }
                    var faction = FactionDefinition(influence.Faction);
                    var before = state.Influence[key];
                    if (before is < 0 or > 6)
                        error ??= Content("influence-bounds", "Influence state lies outside the original track.");
                    var after = BoundedInfluence(before, influence.Amount, faction?.MaximumInfluence ?? 6);
                    if (faction is null && Math.Max(before, after) >= 4)
                        error ??= Unsupported("alliance-bonus", "Faction threshold operations require a sourced faction definition.");
                    state.Influence[key] = after;
                    var output = ImmutableArray.CreateBuilder<Effect>();
                    void Vp(int recipient, int amount)
                    {
                        if (amount == 0) return;
                        ChangeVp(state, recipient, amount, check: mandatory);
                        Effect gain = new GainEffect(ResourceKind.VictoryPoints, amount);
                        output.Add(recipient == owner ? gain : new ForPlayerEffect(recipient, [gain]));
                    }
                    Vp(owner, (after >= 2 ? 1 : 0) - (before >= 2 ? 1 : 0));
                    var holder = state.Alliances[influence.Faction];
                    if (holder == owner && after < before && (after < 4 || _state.Players.Any(p => p.Id != owner &&
                        state.Influence[(p.Id, influence.Faction)] >= Math.Max(4, after))))
                        error ??= Unsupported("alliance-loss", "Alliance holder loss below four or involving a qualifying rival requires an unresolved ownership ruling.");
                    int? next = holder;
                    if (after >= 4 && (holder is null || holder != owner && after > state.Influence[(holder.Value, influence.Faction)])) next = owner;
                    if (next != holder)
                    {
                        if (holder is { } previous) Vp(previous, -1);
                        if (next is { } recipient) Vp(recipient, 1);
                        state.Alliances[influence.Faction] = next;
                    }
                    if (before < 4 && after >= 4 && faction is not null)
                    {
                        CheckUnordered(faction.ThresholdFourReward.Select(x => (x, owner)));
                        output.Add(new PlayerOrderedEffect(Children(faction.ThresholdFourReward, owner)));
                    }
                    return new SequenceEffect(output.ToImmutable());
                case GainEffect { Resource: ResourceKind.VictoryPoints } gain:
                    ChangeVp(state, owner, gain.Amount, check: mandatory);
                    return effect;
                case ConditionalEffect conditional:
                    var conditionalState = state.Copy();
                    var conditionalChild = conditional with
                    { Effects = [.. conditional.Effects.Select(x => Expand(x, owner, conditionalState, depth + 1, mandatory))] };
                    Merge(state, [state.Copy(), conditionalState]);
                    return conditionalChild;
                case SelectedTrashEffect { Optional: true }: return effect;
                case SelectedTrashEffect trash:
                    CheckUnordered(trash.OnTrashed.Select(x => (x, owner)));
                    return trash with { OnTrashed = Children(trash.OnTrashed, owner) };
                case SequenceEffect sequence: return sequence with { Effects = Children(sequence.Effects, owner) };
                case PlayerOrderedEffect ordered:
                    CheckUnordered(ordered.Effects.Select(x => (x, owner)));
                    return ordered with { Effects = Children(ordered.Effects, owner) };
                case ForPlayerEffect player:
                    CheckUnordered(player.Effects.Select(x => (x, player.RecipientId ?? player.PlayerId)));
                    return player with { Effects = Children(player.Effects, player.RecipientId ?? player.PlayerId) };
                case AcquireEffect acquire:
                    CheckUnordered(_cards[acquire.Card].AcquireEffects.Select(x => (x, owner)));
                    return new SequenceEffect([new AcquisitionReservation(acquire.Card, acquire.AllowEmpty),
                        new PlayerOrderedEffect(Children(_cards[acquire.Card].AcquireEffects, owner))]);
                case SignetEffect:
                    CheckUnordered(_leaders[Player(owner).Leader].SignetEffects.Select(x => (x, owner)));
                    return new PlayerOrderedEffect(Children(_leaders[Player(owner).Leader].SignetEffects, owner));
                case ChoiceEffect choice:
                    var branches = new List<FactionProjection>();
                    var options = choice.Options.Select(option =>
                    {
                        var branch = state.Copy();
                        branches.Add(branch);
                        CheckUnordered(option.Effects.Select(x => (x, owner)));
                        return option with { Effects = [.. option.Effects.Select(x => Expand(x, owner, branch, depth + 1, mandatory: false))] };
                    }).ToImmutableArray();
                    Merge(state, branches);
                    return choice with { Options = options };
                case OptionalPaymentEffect optional:
                    var optionalState = state.Copy();
                    var recipientId = optional.RecipientId ?? optional.DecisionOwnerId ?? owner;
                    CheckUnordered(optional.Effects.Select(x => (x, recipientId)));
                    var child = optional with { Effects = [.. optional.Effects.Select(x => Expand(x, recipientId, optionalState, depth + 1, mandatory: false))] };
                    Merge(state, [state.Copy(), optionalState]);
                    return child;
                default: return effect;
            }
        }
    }

    private IEnumerable<(int Owner, Faction Faction, int Amount, int Payer)> InfluenceFootprint(Effect effect, int owner, int depth, int? payer = null)
    {
        if (depth > 32) yield break; // Support owns cyclic definition rejection.
        if (effect is InfluenceEffect influence)
        {
            yield return (owner, influence.Faction, influence.Amount, payer ?? owner);
            if (influence.Amount > 0 && FactionDefinition(influence.Faction) is { } faction)
                foreach (var reward in faction.ThresholdFourReward)
                    foreach (var item in InfluenceFootprint(reward, owner, depth + 1, payer)) yield return item;
            yield break;
        }
        var children = effect switch
        {
            SelectedTrashEffect { Optional: false } trash => trash.OnTrashed,
            SequenceEffect sequence => sequence.Effects,
            PlayerOrderedEffect ordered => ordered.Effects,
            ForPlayerEffect player => player.Effects,
            OptionalPaymentEffect optional => optional.Effects,
            ChoiceEffect choice => choice.Options.SelectMany(x => x.Effects),
            AcquireEffect acquire => _cards[acquire.Card].AcquireEffects,
            SignetEffect => _leaders[Player(owner).Leader].SignetEffects,
            _ => Enumerable.Empty<Effect>(),
        };
        var recipient = effect switch
        {
            ForPlayerEffect player => player.RecipientId ?? player.PlayerId,
            OptionalPaymentEffect optional => optional.RecipientId ?? optional.DecisionOwnerId ?? owner,
            _ => owner,
        };
        var childPayer = effect switch
        {
            ForPlayerEffect player => player.PayerId ?? player.PlayerId,
            OptionalPaymentEffect optional => optional.PayerId ?? optional.DecisionOwnerId ?? payer ?? owner,
            _ => payer ?? owner,
        };
        foreach (var child in children)
            foreach (var item in InfluenceFootprint(child, recipient, depth + 1, childPayer)) yield return item;
    }

    private TurnDiagnostic? ValidateIntrigueReservations(IEnumerable<(Effect Effect, int Owner)> work,
        IReadOnlySet<Effect>? selectedChoices = null)
    {
        var entries = work.ToArray();
        var mandatory = entries.Sum(x => Demand(x.Effect, x.Owner));
        if (mandatory > _state.Shared.IntrigueDeck.Count)
            return Unsupported("intrigue-empty", "Mandatory Intrigue draws exceed available cards; shared-deck recycling is unverified.");
        return null;

        long Demand(Effect effect, int owner) => effect switch
        {
            DrawIntrigueEffect draw => draw.Amount,
            SelectedTrashEffect { Optional: false } trash => trash.OnTrashed.Sum(x => Demand(x, owner)),
            SequenceEffect sequence => sequence.Effects.Sum(x => Demand(x, owner)),
            PlayerOrderedEffect ordered => ordered.Effects.Sum(x => Demand(x, owner)),
            ForPlayerEffect player => player.Effects.Sum(x => Demand(x, player.RecipientId ?? player.PlayerId)),
            AcquireEffect acquire => _cards[acquire.Card].AcquireEffects.Sum(x => Demand(x, owner)),
            SignetEffect => _leaders[Player(owner).Leader].SignetEffects.Sum(x => Demand(x, owner)),
            // Every choice must reserve at least its cheapest Intrigue alternative. The existing
            // resource/stock choice check tests a single common viable option with this reservation.
            ChoiceEffect choice when selectedChoices?.Contains(choice) == true => 0,
            ChoiceEffect choice => choice.Options.Min(option => option.Effects.Sum(x => Demand(x, owner))),
            _ => 0,
        };
    }

    private TurnDiagnostic? ValidateRetreatClosure(EffectContext context, int count)
    {
        if (_state.DecisionWindow?.Kind != DecisionWindowKind.Combat ||
            _state.Players.Any(x => x.Units.TroopsInConflict - (x.Id == context.RecipientId ? count : 0) > 0)) return null;
        // Original troop eligibility has no positive strength once all troops leave. Project before the
        // chosen count changes units, frames or priority; preserve the shared outcome calculator.
        var projected = CombatOutcome.Calculate([.. _state.Players.Select(x => new CombatParticipant(x.Id,
            CombatStrength.Calculate([new("troop", 0, _rules.Strength.TroopStrength)], x.Temporary.Swords,
                _rules.Strength.SwordStrength, _rules.Strength.RequiresDeployedUnits)))], _state.Players.Count);
        if (!_rules.Strength.RequiresDeployedUnits && Pending.Any())
            return Unsupported("combat-closure-order", "Nonstandard strength after retreat requires unsupported pending-work projection.");
        return PreflightConflictRewards(projected);
    }

    private ConflictRule? RevealedConflict => _state.Conflict.Revealed.Cards.Count == 1
        ? _rules.Lifecycle?.Conflicts.FirstOrDefault(x => x.Id == _state.Conflict.Revealed.Cards[0].Definition) : null;

    private ImmutableArray<ConflictRewardRecipient> RewardRecipients(CombatOutcome outcome)
    {
        if (RevealedConflict is not { } conflict) return [];
        return [.. outcome.Placements.Where(x => x.RewardTier is not null).SelectMany(placement =>
            placement.PlayerIds.Select(id => new ConflictRewardRecipient(id, placement.RewardTier!.Value,
                conflict.Rewards.Single(x => x.Tier == placement.RewardTier).Effects)))];
    }

    private TurnDiagnostic? PreflightConflictRewards(CombatOutcome outcome)
    {
        if (_lifecycleDefinitionError is { } invalid) return invalid;
        if (PreflightLifecycleContinuation() is { } transition) return transition;
        if (outcome.Placements.IsEmpty) return null; // No positive strength means no mandatory awards.
        if (RevealedConflict is null)
            return Unsupported("conflict-reward-definition", "The revealed Conflict has no executable reward definition.");
        var recipients = RewardRecipients(outcome);
        foreach (var recipient in recipients)
            if (ValidateContext(recipient.Effects, false, false, recipient.PlayerId) is { } error) return error;
        foreach (var tied in outcome.Placements.Where(x => x.IsTie && x.RewardTier is not null))
        {
            var entries = recipients.Where(x => tied.PlayerIds.Contains(x.PlayerId)).ToArray();
            // Canonical seat order is only an engineering schedule for independent awards. Never resolve
            // a scarce target, competing Alliance, or foreign-role funding by inventing a priority rule.
            if (entries.Any(x => RewardOrderingSensitive(x.Effects, x.PlayerId)))
                return Unsupported("conflict-reward-order", "Tied shared-deck, cross-player or scarce awards require a sourced ordering ruling.");
            var footprints = entries.SelectMany(x => x.Effects.SelectMany(effect => InfluenceFootprint(effect, x.PlayerId, 0))).ToArray();
            foreach (var sharedFaction in footprints.GroupBy(x => x.Faction).Where(x => x.Select(i => i.Owner).Distinct().Count() > 1))
            {
                var independent = _state.Board.Alliances.Single(x => x.Faction == sharedFaction.Key).OwnerId is null &&
                    entries.All(x => x.Effects.All(effect => SimplePositiveInfluenceWork(effect, x.PlayerId))) &&
                    sharedFaction.All(x => x.Amount >= 0) &&
                    sharedFaction.GroupBy(x => x.Owner).All(seat =>
                        (long)Influence(Player(seat.Key), sharedFaction.Key) + seat.Sum(x => (long)x.Amount) < 4);
                if (!independent)
                    return Unsupported("conflict-reward-order", "Shared tied faction awards require independent positive tracks below four with no incumbent.");
            }
        }
        (Effect Effect, int Owner)[] work = [(new SequenceEffect([.. recipients.Select(x => (Effect)new ForPlayerEffect(x.PlayerId, x.Effects))]), 0)];
        return ValidateTrashAvailability(work.Select(x => (x.Effect, x.Owner, x.Owner, (BoardSpaceId?)null))) ??
            ValidateSpecialOperations(work) ?? ValidateAcquisitionStock(work) ??
            ValidateResourceBounds(work) ?? ValidateInfluenceBounds(work) ?? ValidatePermanentOperations(work);
    }

    private bool SimplePositiveInfluenceWork(Effect effect, int owner) => effect switch
    {
        InfluenceEffect influence => influence.Amount >= 0,
        SequenceEffect sequence => sequence.Effects.All(x => SimplePositiveInfluenceWork(x, owner)),
        PlayerOrderedEffect ordered => ordered.Effects.All(x => SimplePositiveInfluenceWork(x, owner)),
        // Choice/payment/acquisition/Signet children with Influence need branch or causal analysis.
        _ => !InfluenceFootprint(effect, owner, 0).Any(),
    };

    private bool RewardOrderingSensitive(IEnumerable<Effect> effects, int owner)
    {
        // Expand actual threshold children through the existing faction projector, preserving causal
        // order. An untriggered Intrigue bonus below four must not exclude independent tied tracks.
        var projection = ProjectFactionWork([((Effect)new PlayerOrderedEffect([.. effects]), owner, owner, (BoardSpaceId?)null)]);
        if (projection.Error is not null) return true;
        return Sensitive(((PlayerOrderedEffect)projection.Work[0].Effect).Effects, owner, 0);

        bool Sensitive(IEnumerable<Effect> children, int recipient, int depth) => depth > 32 || children.Any(effect => effect switch
        {
            DrawIntrigueEffect { Amount: > 0 } => true,
            TakeMentatEffect or ControlSpaceEffect or ForPlayerEffect => true,
            SelectedTrashEffect { Optional: false } trash => Sensitive(trash.OnTrashed, recipient, depth + 1),
            SequenceEffect sequence => Sensitive(sequence.Effects, recipient, depth + 1),
            PlayerOrderedEffect ordered => Sensitive(ordered.Effects, recipient, depth + 1),
            OptionalPaymentEffect optional => optional.DecisionOwnerId is { } chooser && chooser != recipient ||
                optional.PayerId is { } payer && payer != recipient || optional.RecipientId is { } target && target != recipient ||
                Sensitive(optional.Effects, recipient, depth + 1),
            ChoiceEffect choice => choice.Options.Any(x => Sensitive(x.Effects, recipient, depth + 1)),
            AcquireEffect acquire => Sensitive(_cards[acquire.Card].AcquireEffects, recipient, depth + 1),
            SignetEffect => Sensitive(_leaders[Player(recipient).Leader].SignetEffects, recipient, depth + 1),
            _ => false,
        });
    }

    private void BeginConflictRewards()
    {
        var recipients = RewardRecipients(_combatOutcome!);
        _conflictRewards = new(_combatOutcome!, recipients, 0, [], [], null);
        StartConflictRecipient();
    }

    private IEnumerable<(Effect Effect, int Owner)> FutureRewardWork() => _conflictRewards is { } progress
        ? progress.Recipients.Skip(progress.CurrentRecipient + 1).Select(x => ((Effect)new PlayerOrderedEffect(x.Effects), x.PlayerId))
        : [];

    private void AdvanceConflictRecipient()
    {
        _conflictRewards = _conflictRewards! with { CurrentRecipient = _conflictRewards.CurrentRecipient + 1, RootFrameId = null };
        StartConflictRecipient();
    }

    private void StartConflictRecipient()
    {
        var progress = _conflictRewards!;
        if (progress.CurrentRecipient >= progress.Recipients.Length)
        {
            CompleteCombat();
            return;
        }
        var recipient = progress.Recipients[progress.CurrentRecipient];
        var winner = progress.Outcome.WinnerId == recipient.PlayerId;
        if (winner)
            OpenWindow(DecisionWindowKind.PostWinRewards, [recipient.PlayerId], new(ExecutionContinuationKind.ConflictRewards));
        PushFrame(recipient.Effects, RootContext(recipient.PlayerId, null, RevealedConflict!.Id, null, EffectProvenance.Board),
            EffectGroupMode.PlayerOrdered, new(ExecutionContinuationKind.ConflictRewards, recipient.PlayerId));
        _conflictRewards = progress with { RootFrameId = _frames[^1].Id };
        if (winner) _state.DecisionWindow = _state.DecisionWindow! with { RewardFrameId = _frames[^1].Id };
        _state.DecisionOwnerId = recipient.PlayerId;
    }

    private void ApplyControl(BoardSpaceId space, PlayerState player)
    {
        var board = _state.Board.Spaces.Single(x => x.Definition.Id == space);
        if (board.ControllerId == player.Id) return;
        if (board.ControllerId is { } old)
        {
            var previous = Player(old);
            previous.Units = previous.Units with { ControlMarkers = checked(previous.Units.ControlMarkers + 1) };
        }
        player.Units = player.Units with { ControlMarkers = player.Units.ControlMarkers - 1 };
        board.ControllerId = player.Id;
    }

    private int? MatchingDefenseOwner() => RevealedConflict?.ControlSpace is { } space
        ? _state.Board.Spaces.Single(x => x.Definition.Id == space).ControllerId : null;

    private bool ConditionHolds(EndgameCondition condition, int owner)
    {
        int Count(int seat) => new[] { Player(seat).Deck, Player(seat).Hand, Player(seat).Discard, Player(seat).InPlay }
            .Sum(zone => zone.Cards.Count(x => x.Definition.Value == "imperium:reserve:spice-must-flow:v1"));
        var count = Count(owner);
        return condition switch
        {
            EndgameCondition.OwnAtLeastTwoSpiceMustFlow => count >= 2,
            EndgameCondition.OwnMoreSpiceMustFlowThanEachOpponent => _state.Players.Where(x => x.Id != owner).All(x => count > Count(x.Id)),
            _ => throw new InvalidOperationException("Unsupported condition passed preflight."),
        };
    }

    private TurnDiagnostic? ValidateSpecialOperations(IEnumerable<(Effect Effect, int Owner)> work)
    {
        // Generated threshold children use the same causal projection as stock/resource/permanent
        // checks, including selected work, suspended parents and later Conflict recipients.
        var projection = ProjectFactionWork(work.Select(x => (x.Effect, x.Owner, x.Owner, (BoardSpaceId?)null)));
        if (projection.Error is { } projectionError) return projectionError;
        var controls = new List<(BoardSpaceId Space, int Owner)>();
        var mentats = new List<int>();
        var agents = new Dictionary<int, long>();
        TurnDiagnostic? error = null;
        foreach (var (effect, owner, _, _) in projection.Work) Visit(effect, owner, 0);
        if (error is not null) return error;
        if (mentats.Count > 1 || mentats.Count > 0 && (_state.Board.Mentat.OwnerId is not null ||
            _state.Board.Mentat.Space is null || _state.Board.Mentat.Space != _state.Board.Definition.InitialMentatSpace))
            return Unsupported("mentat-unavailable", "Competing or already owned Mentat awards require an unresolved ruling.");
        if (agents.Any(x => (long)Player(x.Key).Units.AvailableAgents + x.Value > int.MaxValue) ||
            mentats.Count > 0 && _state.Round == int.MaxValue)
            return Content("resource-bounds", "Mentat award exceeds Agent or lifetime bounds.");
        if (controls.GroupBy(x => x.Space).Any(x => x.Select(i => i.Owner).Distinct().Count() > 1))
            return Unsupported("control-order", "Competing control awards require an explicit sourced ordering.");
        foreach (var (space, owner) in controls)
            if (_state.Board.Spaces.Single(x => x.Definition.Id == space).ControllerId is { } old && old != owner &&
                Player(old).Units.ControlMarkers == int.MaxValue)
                return Content("resource-bounds", "Returning a control marker exceeds supply bounds.");
        foreach (var owner in controls.Select(x => x.Owner).Distinct())
        {
            var demand = controls.Where(x => x.Owner == owner).Select(x => x.Space).Distinct()
                .Count(space => _state.Board.Spaces.Single(x => x.Definition.Id == space).ControllerId != owner);
            if (demand > Player(owner).Units.ControlMarkers)
                return Unsupported("control-markers", "A mandatory control award has no available marker.");
        }
        return null;

        void Visit(Effect effect, int owner, int depth)
        {
            if (depth > 32) { error ??= Content("effect-cycle", "Special reward graph is cyclic or too deep."); return; }
            IEnumerable<Effect> children = [];
            switch (effect)
            {
                case ControlSpaceEffect control:
                    if (!_state.Board.Spaces.Any(x => x.Definition.Id == control.Space && x.Definition.Controllable))
                        error ??= Content("control-space", "Control requires a controllable board space.");
                    else controls.Add((control.Space, owner));
                    return;
                case TakeMentatEffect { ForNextRound: false } when !MentatAtStartingSpace:
                    return; // The paid board effect still draws when the token is absent.
                case TakeMentatEffect:
                    mentats.Add(owner);
                    agents[owner] = agents.GetValueOrDefault(owner) + 1;
                    return;
                case SwordmasterEffect:
                    agents[owner] = agents.GetValueOrDefault(owner) + 1;
                    return;
                case DeployFromSupplyEffect deploy:
                    if (_state.Phase != GamePhase.RoundStart || _frames.Count == 0 ||
                        _frames[0].Continuation.Kind != ExecutionContinuationKind.RoundStart)
                        error ??= Unsupported("supply-deployment-context", "Supply deployment requires the matching Conflict defense decision.");
                    else if (deploy.Amount != 1 || Player(owner).Units.TroopsInSupply < deploy.Amount)
                        error ??= Illegal("defense-supply", "The defense requires an available supply troop.");
                    else if (!StrengthFits(Player(owner).Temporary.Swords, Player(owner).Units.TroopsInConflict + deploy.Amount))
                        error ??= Content("resource-bounds", "Defense exceeds Combat strength bounds.");
                    return;
                case SelectedTrashEffect { Optional: true }: return;
                case SelectedTrashEffect trash: children = trash.OnTrashed; break;
                case SequenceEffect sequence: children = sequence.Effects; break;
                case PlayerOrderedEffect ordered: children = ordered.Effects; break;
                case ForPlayerEffect player: owner = player.RecipientId ?? player.PlayerId; children = player.Effects; break;
                // Optional special operations are checked at selection; a mandatory Choice involving a scarce
                // operation is gated until branch-aware reservation can prove all continuations safe.
                case OptionalPaymentEffect: return;
                case ChoiceEffect choice:
                    if (choice.Options.Any(x => x.Effects.Any(effect => Special(effect, owner))))
                        error ??= Unsupported("special-choice", "Scarce awards inside mandatory choices require branch-aware reservation.");
                    return;
                case AcquireEffect acquire: children = _cards[acquire.Card].AcquireEffects; break;
                case SignetEffect: children = _leaders[Player(owner).Leader].SignetEffects; break;
            }
            foreach (var child in children) Visit(child, owner, depth + 1);
        }
        bool Special(Effect effect, int owner, int depth = 0) => depth > 32 || (effect switch
        {
            ControlSpaceEffect or TakeMentatEffect or DeployFromSupplyEffect => true,
            SelectedTrashEffect { Optional: false } trash => trash.OnTrashed.Any(x => Special(x, owner, depth + 1)),
            SequenceEffect sequence => sequence.Effects.Any(x => Special(x, owner, depth + 1)),
            PlayerOrderedEffect ordered => ordered.Effects.Any(x => Special(x, owner, depth + 1)),
            ForPlayerEffect player => player.Effects.Any(x => Special(x, player.RecipientId ?? player.PlayerId, depth + 1)),
            OptionalPaymentEffect optional => optional.Effects.Any(x => Special(x, optional.RecipientId ?? optional.DecisionOwnerId ?? owner, depth + 1)),
            ChoiceEffect choice => choice.Options.Any(x => x.Effects.Any(child => Special(child, owner, depth + 1))),
            AcquireEffect acquire => _cards[acquire.Card].AcquireEffects.Any(x => Special(x, owner, depth + 1)),
            SignetEffect => _leaders[Player(owner).Leader].SignetEffects.Any(x => Special(x, owner, depth + 1)),
            _ => false,
        });
    }
}
