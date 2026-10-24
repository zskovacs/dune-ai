using System.Collections.Immutable;

namespace Dune.Engine.Turns;

/// <summary>State-independent shape/support checks, cached by immutable executable box.</summary>
public sealed class EffectGraphValidator
{
    private readonly Dictionary<ImmutableArray<Effect>, ImmutableArray<TurnDiagnostic>> _cache = [];

    public ImmutableArray<TurnDiagnostic> Validate(ImmutableArray<Effect> effects)
    {
        if (_cache.TryGetValue(effects, out var cached)) return cached;
        var diagnostics = ImmutableArray.CreateBuilder<TurnDiagnostic>();
        Visit(effects, 0, 0, new HashSet<Effect>(ReferenceEqualityComparer.Instance), diagnostics);
        var result = diagnostics.Distinct().ToImmutableArray();
        _cache[effects] = result;
        return result;
    }

    private static void Visit(ImmutableArray<Effect> effects, int depth, int jsonDepth, HashSet<Effect> path,
        ImmutableArray<TurnDiagnostic>.Builder diagnostics)
    {
        if (depth > 32 || jsonDepth > 48)
        {
            diagnostics.Add(new(StepStatus.ContentError, "effect-cycle", "Effect graph is cyclic or exceeds the supported nesting depth."));
            return;
        }
        if (effects.IsDefault) return;
        foreach (var effect in effects)
        {
            if (effect is null || !path.Add(effect))
            {
                diagnostics.Add(new(StepStatus.ContentError, "effect-cycle", "Effect graph contains a null or cyclic node."));
                continue;
            }
            switch (effect)
            {
                case GainEffect gain when gain.Amount < 0 || !Enum.IsDefined(gain.Resource):
                case DrawEffect { Amount: < 0 }:
                case RecruitEffect { Amount: < 0 }:
                case InfluenceEffect influence when influence.Amount < 0 || !Enum.IsDefined(influence.Faction):
                    diagnostics.Add(new(StepStatus.ContentError, "effect", "Effect amount or enum is invalid."));
                    break;
                case SequenceEffect sequence: Visit(sequence.Effects, depth + 1, jsonDepth + 2, path, diagnostics); break;
                case PlayerOrderedEffect ordered: Visit(ordered.Effects, depth + 1, jsonDepth + 2, path, diagnostics); break;
                case ForPlayerEffect target: Visit(target.Effects, depth + 1, jsonDepth + 2, path, diagnostics); break;
                case OptionalPaymentEffect optional:
                    Cost(optional.Cost, diagnostics);
                    Visit(optional.Effects, depth + 1, jsonDepth + 2, path, diagnostics);
                    break;
                case ChoiceEffect choice:
                    if (choice.Options.IsDefaultOrEmpty)
                        diagnostics.Add(new(StepStatus.ContentError, "choice", "An effect choice needs options."));
                    else foreach (var option in choice.Options)
                    {
                        if (option is null)
                        {
                            diagnostics.Add(new(StepStatus.ContentError, "choice", "A choice option is required."));
                            continue;
                        }
                        Cost(option.Cost, diagnostics);
                        Visit(option.Effects, depth + 1, jsonDepth + 4, path, diagnostics);
                    }
                    break;
                case UnsupportedEffect unsupported:
                    diagnostics.Add(new(StepStatus.Unsupported, "effect-support", unsupported.Reason));
                    break;
                case GainEffect or DrawEffect or RecruitEffect or InfluenceEffect or TrashSelfEffect or AcquireEffect or
                    HighCouncilEffect or SwordmasterEffect or HarvestSpiceEffect or SignetEffect:
                    break;
                default: diagnostics.Add(new(StepStatus.Unsupported, "effect-type", "This effect primitive is unsupported.")); break;
            }
            path.Remove(effect);
        }
    }

    private static void Cost(ResourceCost cost, ImmutableArray<TurnDiagnostic>.Builder diagnostics)
    {
        if (cost is null || cost.Solari < 0 || cost.Spice < 0 || cost.Water < 0)
            diagnostics.Add(new(StepStatus.ContentError, "cost", "Costs must be nonnegative."));
    }
}
