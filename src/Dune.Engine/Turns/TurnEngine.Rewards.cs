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
}
