using System.Collections.Immutable;
using Dune.Engine.Content;

namespace Dune.Engine.Rulesets;

public readonly record struct ModuleId(string Value);
public readonly record struct DefinitionId(string Value);
public enum DefinitionKind
{
    BoardLayout, BoardSpace, StartingDeck, Resources, ConflictDeck, CardPool, LeaderPool,
    Mechanic, VictoryCondition, Setup,
}
public readonly record struct DefinitionKey(DefinitionKind Kind, string Slot);
public sealed record RuleDefinition(DefinitionId Id, DefinitionKey Key, string Value, ImmutableArray<SourceReference> Sources);
public sealed record RuleContribution(RuleDefinition Definition, DefinitionId? Replaces = null);
public sealed record RuleModule(ModuleId Id, int Revision, ImmutableArray<ModuleId> Dependencies,
    ImmutableArray<ModuleId> IncompatibleWith, ImmutableArray<RuleContribution> Contributions);
public sealed record RuleProvenance(ModuleId Module, RuleDefinition Definition);
public sealed record ResolvedRule(RuleDefinition Definition, ImmutableArray<RuleProvenance> History);
