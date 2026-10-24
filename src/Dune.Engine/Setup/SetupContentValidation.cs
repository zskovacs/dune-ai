using System.Collections.Immutable;
using Dune.Engine.Content;
using Dune.Engine.Rulesets;

namespace Dune.Engine.Setup;

internal static class SetupContentValidation
{
    public static ImmutableArray<ConfigurationError> Validate(ResolvedGameDefinition definition, bool fixture = false)
    {
        var errors = ImmutableArray.CreateBuilder<ConfigurationError>();
        void Error(string code, string message) => errors.Add(new(code, "content", message));
        if (definition.Setup is not { } setup)
        {
            Error("unsupported-setup", "This resolved ruleset has no implemented initialization recipe.");
            return errors.ToImmutable();
        }
        if (!fixture && definition.CatalogCompleteness != CatalogCompleteness.Complete)
            Error("incomplete-catalog", "Authentic setup requires a complete verified catalog; the bundled catalog is representative.");
        var cards = definition.Content;
        if (!fixture)
        {
            foreach (var d in cards)
            {
                if (d.Verification != VerificationStatus.RulebookVerified)
                    Error("unverified-content", $"Setup metadata is not verified: {d.Id.Value}.");
                if (d.Product != Product.Imperium || !HasApplicableSource(d))
                    Error("unsupported-content-source", $"No applicable original Imperium setup source: {d.Id.Value}.");
            }
        }
        void Total(ContentCategory category, int required)
        {
            long actual = cards.Where(x => x.Category == category).Sum(x => (long)x.Quantity);
            if (actual != required)
                Error("content-quantity", $"{category}: expected {required} physical copies, found {actual}.");
        }
        Total(ContentCategory.StartingCard, 40);
        Total(ContentCategory.ImperiumCard, setup.SharedCards.ImperiumCards);
        Total(ContentCategory.IntrigueCard, setup.SharedCards.IntrigueCards);
        Total(ContentCategory.ReserveCard, 24);
        Total(ContentCategory.ConflictCard, setup.Conflicts.Sum(x => x.Available));
        Total(ContentCategory.Leader, setup.SharedCards.Leaders);
        void Required(ContentId id, ContentCategory category, int quantity)
        {
            var found = cards.FirstOrDefault(x => x.Id == id);
            if (found is null || found.Category != category || found.Quantity != quantity)
                Error("required-content", $"Required {id.Value}: {category}, {quantity} boxed copies.");
        }
        foreach (var card in setup.StartingDeck)
            Required(card.Definition, ContentCategory.StartingCard, card.CopiesPerPlayer * 4);
        foreach (var stack in setup.Reserves)
            Required(stack.Definition, ContentCategory.ReserveCard, stack.Quantity);
        Required(setup.Board.Id, ContentCategory.BoardLayout, 1);
        foreach (var tierSetup in setup.Conflicts)
        {
            long actual = cards.Where(x => x.Category == ContentCategory.ConflictCard && x.ConflictTier == tierSetup.Tier).Sum(x => (long)x.Quantity);
            if (actual != tierSetup.Available)
                Error("conflict-tier-quantity", $"Conflict {tierSetup.Tier}: expected {tierSetup.Available} available cards, found {actual}.");
        }
        if (cards.Any(x => x.Category == ContentCategory.ConflictCard && x.ConflictTier is null))
            Error("conflict-tier-required", "Each Conflict definition requires a verified tier.");
        if (cards.Any(x => x.Category == ContentCategory.Leader && x.Quantity != 1))
            Error("leader-quantity", "Each leader must have exactly one physical copy.");
        return errors.ToImmutable();
    }

    // These citations establish component/setup applicability. RulebookVerified is
    // an authoring assertion, never an automated proof of individual card accuracy.
    private static bool HasApplicableSource(ContentDefinition d) => d.Sources.Any(source =>
        source.Document == "DUNE_IMPERIUM_Rules_2020_10_26.pdf" && (d.Category switch
        {
            ContentCategory.StartingCard or ContentCategory.ReserveCard or
                ContentCategory.ImperiumCard or ContentCategory.IntrigueCard or
                ContentCategory.ConflictCard => source.Page == 3,
            ContentCategory.Leader => source.Page is 3 or 4,
            ContentCategory.BoardLayout or ContentCategory.BoardSpace => source.Page is 4 or 17 or 18,
            _ => false,
        }));
}
