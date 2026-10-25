using Dune.Engine.Content;
using Dune.Engine.Setup;

namespace Dune.Engine.Turns;

public sealed partial class TurnEngine
{
    private IEnumerable<SelectedTrashOption> TrashTargets(int playerId)
    {
        var player = Player(playerId);
        foreach (var zone in new[] { player.Hand, player.Discard, player.InPlay })
            foreach (var selector in Selectors(zone))
                yield return new(zone.Id, selector, Find(zone, selector)!.Id);
    }

    private TurnDiagnostic? ValidateSelectedTrash(GameAction action, DecisionFrame decision)
    {
        var effect = (SelectedTrashEffect)decision.Effect;
        var context = decision.Context;
        if (context.DecisionOwnerId != context.PayerId)
            return Unsupported("trash-roles", "Selected trash requires the chooser to pay their own card.");
        var selection = decision.SelectedTrash!;
        if (action is DeclineTrashAction decline)
            return !effect.Optional || decline.DecisionId != selection.Id
                ? Illegal("trash-decline", "This trash decision cannot be declined.") : ValidateRemaining();
        if (action is not SelectTrashCardAction select || select.DecisionId != selection.Id)
            return Illegal("trash-decision", "Select a card for the current trash decision.");
        var player = Player(context.DecisionOwnerId);
        var zone = new[] { player.Hand, player.Discard, player.InPlay }.FirstOrDefault(x => x.Id == select.Source);
        var binding = selection.Targets.FirstOrDefault(x => x.Source == select.Source && x.Card == select.Card);
        if (zone is null || binding is null || Find(zone, select.Card)?.Id != binding.Instance)
            return Illegal("trash-target", "The selected own card is unavailable for this decision.");
        // Stage B deliberately fails closed until the shared Reserve destination contract is installed.
        if (IsReserveTrash(select.Card.Definition))
            return Unsupported("trash-reserve", "Reserve return is not supported by this operation yet.");
        var supported = Support(effect.OnTrashed, context.RecipientId, context.SourceCard is not null,
            context.Space is not null, _state.DecisionWindow?.Kind == DecisionWindowKind.Combat);
        if (supported is not null) return supported;
        if (ValidateTrashRoles(effect.OnTrashed, context, cardSource: context.SourceCard is not null, boardSource: context.Space is not null) is { } roles) return roles;
        var target = new SelectedCardTarget(binding.Instance, binding.Card.Definition, binding.Source, context.DecisionOwnerId);
        return ValidateRemaining(effect.OnTrashed.Select(x => (x, context.RecipientId)), context.Space,
            selectedPayer: context.PayerId, selectedChooser: context.DecisionOwnerId, removedTrash: target);
    }

    private void BeginSelectedTrash(SelectedTrashEffect effect, EffectContext context)
    {
        _frames[^1] = _frames[^1] with
        {
            Decision = new(effect, context)
            {
                SelectedTrash = new(++_nextEffectId, [.. TrashTargets(context.DecisionOwnerId)]),
            },
        };
    }

    private void CommitSelectedTrash(SelectTrashCardAction action)
    {
        var decision = Decision!;
        var binding = decision.SelectedTrash!.Targets.Single(x => x.Source == action.Source && x.Card == action.Card);
        var source = _state.Zones.Single(x => x.Id == binding.Source);
        var target = new SelectedCardTarget(binding.Instance, binding.Card.Definition, binding.Source, decision.Context.DecisionOwnerId);
        source.MoveTo(binding.Instance, _state.Shared.Trashed);
        _frames[^1] = _frames[^1] with { Decision = null };
        PushChild(((SelectedTrashEffect)decision.Effect).OnTrashed, decision.Context with { SelectedTarget = target },
            EffectGroupMode.PlayerOrdered);
        ContinueTurn();
    }

    private void CommitDeclinedTrash(DeclineTrashAction action)
    {
        if (Decision!.SelectedTrash!.Id != action.DecisionId)
            throw new InvalidOperationException("A validated decline lost its selected trash decision.");
        _frames[^1] = _frames[^1] with { Decision = null };
        ContinueTurn();
    }

    private bool IsReserveTrash(ContentId definition) =>
        _state.Definition.Setup!.Reserves.Any(x => x.Definition == definition) ||
        _state.Definition.Content.Any(x => x.Id == definition && x.Category == ContentCategory.ReserveCard);

    private TurnDiagnostic? SelectedTrashSupport(SelectedTrashEffect effect, int owner, bool card, bool board,
        bool combatRetreat, int depth)
    {
        var error = Support(effect.OnTrashed, owner, card, board, combatRetreat, depth + 1);
        return effect.Optional && error?.Status == StepStatus.Unsupported ? null : error;
    }

    // Validate all authored child contexts independently of unsupported success support. In particular,
    // an Unsupported sibling must not hide malformed roles or source requirements in an optional box.
    private TurnDiagnostic? ValidateTrashRoles(IEnumerable<Effect> effects, EffectContext context, int depth = 0,
        bool? cardSource = null, bool? boardSource = null)
    {
        if (depth > 32) return Content("effect-cycle", "Selected trash graph is cyclic or too deep.");
        TurnDiagnostic? result = null;
        foreach (var effect in effects)
        {
            TurnDiagnostic? error = effect switch
            {
                TrashSelfEffect when cardSource == false => Content("effect-context", "Self-trash requires a source card."),
                HarvestSpiceEffect when boardSource == false => Content("effect-context", "Spice harvest requires a board space."),
                ControlSpaceEffect control when !_state.Board.Spaces.Any(x => x.Definition.Id == control.Space && x.Definition.Controllable) =>
                    Content("control-space", "Control requires a controllable board space."),
                SelectedTrashEffect trash => SuccessRoles(trash) ?? (context.DecisionOwnerId != context.PayerId
                    ? Unsupported("trash-roles", "Selected trash requires the chooser to pay their own card.") : null),
                ConditionalEffect conditional => ValidateTrashRoles(conditional.Effects, context, depth + 1, cardSource, boardSource),
                SequenceEffect sequence => ValidateTrashRoles(sequence.Effects, context, depth + 1, cardSource, boardSource),
                PlayerOrderedEffect ordered => ValidateTrashRoles(ordered.Effects, context, depth + 1, cardSource, boardSource),
                ForPlayerEffect player => ValidateOwner(player.PlayerId) ?? ValidateRoles(player.DecisionOwnerId, player.PayerId, player.RecipientId) ??
                    ValidateTrashRoles(player.Effects, BindRoles(context, player.DecisionOwnerId ?? player.PlayerId,
                        player.PayerId ?? player.PlayerId, player.RecipientId ?? player.PlayerId), depth + 1, cardSource, boardSource),
                OptionalPaymentEffect optional => ValidateRoles(optional.DecisionOwnerId, optional.PayerId, optional.RecipientId) ??
                    ValidateTrashRoles(optional.Effects, BindRoles(context, optional.DecisionOwnerId, optional.PayerId, optional.RecipientId),
                        depth + 1, cardSource, boardSource),
                ChoiceEffect choice => choice.Options.Select(x => ValidateTrashRoles(x.Effects, context, depth + 1, cardSource, boardSource))
                    .OrderBy(x => x?.Status == StepStatus.ContentError ? 0 : 1).FirstOrDefault(x => x is not null),
                AcquireEffect acquire when _cards.TryGetValue(acquire.Card, out var rule) =>
                    _cardErrors.GetValueOrDefault(rule.Id) ?? ValidateTrashRoles(rule.AcquireEffects, context, depth + 1, true, false),
                SignetEffect => ValidateTrashRoles(_leaders[Player(context.RecipientId).Leader].SignetEffects, context, depth + 1, cardSource, boardSource),
                InfluenceEffect influence when FactionDefinition(influence.Faction) is { } faction =>
                    ValidateTrashRoles(faction.ThresholdFourReward, context, depth + 1, false, false),
                _ => null,
            };
            if (error?.Status == StepStatus.ContentError) return error;
            result ??= error;
        }
        return result;

        TurnDiagnostic? SuccessRoles(SelectedTrashEffect trash)
        {
            var error = ValidateTrashRoles(trash.OnTrashed, context, depth + 1, cardSource, boardSource);
            return trash.Optional && error?.Status == StepStatus.Unsupported ? null : error;
        }
    }

    // Current eligible cards are a conservative bound: future draws/acquisitions never fund a card cost.
    // Choices and source-sensitive competing removals require joint analysis outside this bounded proof.
    private TurnDiagnostic? ValidateTrashAvailability(
        IEnumerable<(Effect Effect, int Owner, int Payer, BoardSpaceId? Space)> work,
        SelectedCardTarget? removed = null, int? chooser = null)
    {
        var projection = ProjectFactionWork(work);
        if (projection.Error is { } error) return error;
        var demand = new Dictionary<int, long>();
        var selfTrash = false;
        TurnDiagnostic? issue = null;
        foreach (var (effect, owner, payer, space) in projection.Work)
            Visit(effect, RootContext(owner, null, null, space, EffectProvenance.Card) with
            { DecisionOwnerId = chooser ?? owner, PayerId = payer }, 0);
        if (issue is not null) return issue;
        if (selfTrash && demand.Count > 0)
            return Unsupported("trash-source-order", "Competing source and target removals require unsupported joint analysis.");
        foreach (var (owner, count) in demand)
        {
            var available = TrashTargets(owner).Count(x => x.Instance != removed?.Instance && !IsReserveTrash(x.Card.Definition));
            if (count > available)
                return Unsupported("trash-targets", "Mandatory selected trash exceeds currently available own cards.");
        }
        return null;

        void Visit(Effect effect, EffectContext context, int depth, bool choice = false)
        {
            if (depth > 32) { issue ??= Content("effect-cycle", "Selected trash graph is cyclic or too deep."); return; }
            IEnumerable<Effect> children = [];
            switch (effect)
            {
                case SelectedTrashEffect when context.DecisionOwnerId != context.PayerId:
                    issue ??= Unsupported("trash-roles", "Selected trash requires the chooser to pay their own card.");
                    return;
                case SelectedTrashEffect { Optional: true }: return;
                case SelectedTrashEffect trash:
                    if (choice) issue ??= Unsupported("trash-choice-order", "Selected trash inside a mandatory choice requires unsupported joint analysis.");
                    demand[context.DecisionOwnerId] = demand.GetValueOrDefault(context.DecisionOwnerId) + 1;
                    children = trash.OnTrashed;
                    break;
                case TrashSelfEffect: selfTrash = true; return;
                case SequenceEffect sequence: children = sequence.Effects; break;
                case PlayerOrderedEffect ordered: children = ordered.Effects; break;
                case ForPlayerEffect player:
                    context = BindRoles(context, player.DecisionOwnerId ?? player.PlayerId,
                        player.PayerId ?? player.PlayerId, player.RecipientId ?? player.PlayerId);
                    children = player.Effects;
                    break;
                case ChoiceEffect options:
                    foreach (var option in options.Options)
                        foreach (var child in option.Effects) Visit(child, context, depth + 1, choice: true);
                    return;
                case OptionalPaymentEffect: return;
                case AcquireEffect acquire: children = _cards[acquire.Card].AcquireEffects; break;
                case SignetEffect: children = _leaders[Player(context.RecipientId).Leader].SignetEffects; break;
            }
            foreach (var child in children) Visit(child, context, depth + 1, choice);
        }
    }
}
