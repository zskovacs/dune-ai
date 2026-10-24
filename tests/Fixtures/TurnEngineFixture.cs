using System.Collections.Immutable;
using Dune.Engine.Content;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;
using Dune.Engine.Turns;

namespace Dune.Engine.Fixtures;

/// <summary>
/// Explicitly invented card AND board effects for state-machine tests, not authentic named-space rules.
/// Synthetic physical cards use synthetic effects. Real starters are moved to unused
/// storage, never given fabricated effects. This fixture is not an authentic Dune inventory/game.
/// </summary>
internal static class TurnEngineFixture
{
    internal static GameState CreateState(int players = 3, int seed = 42)
    {
        var state = OriginalImperiumFixture.Create(players, seed);
        state.FirstPlayerId = 0;
        foreach (var player in state.Players)
        {
            foreach (var card in player.Deck.Cards.ToArray()) player.Deck.MoveTo(card.Id, state.Shared.UnusedStarters);
            for (var i = 0; i < 10; i++) state.Shared.ImperiumDeck.TryDrawTo(player.Deck);
        }
        return state;
    }

    internal static TurnRuleSet Rules(GameState state) => new("synthetic-turn-tests-v1",
        [.. state.Definition.Content.Where(x => x.Id.Value.StartsWith("fixture:imperiumcard:", StringComparison.Ordinal))
            .Select(x => new CardRule(x.Id, [.. Enum.GetValues<AgentIcon>()], [],
                [new GainEffect(ResourceKind.Persuasion, 1), new GainEffect(ResourceKind.Swords, 1)], 2, [])),
            .. OriginalImperiumTurnRules.Create().Cards.Where(x => x.Id.Value.Contains(":reserve:", StringComparison.Ordinal))],
        [new(Space("secure-contract"), AgentIcon.SpiceTrade, new(), [new GainEffect(ResourceKind.Solari, 3)]),
            new(Space("arrakeen"), AgentIcon.City, new(), [new RecruitEffect(2)], Combat: true),
            new(Space("wealth"), AgentIcon.Emperor, new(), [new InfluenceEffect(Faction.Emperor)]),
            new(Space("high-council"), AgentIcon.Landsraad, new(Solari: 5), [new HighCouncilEffect()], OncePerGame: true)],
        [.. state.Players.Select(x => new LeaderRule(x.Leader, true, [], Synthetic: true))]);

    internal static BoardSpaceId Space(string name) => new($"imperium:space:{name}:v1");
    internal static CardSelector Card(CardInstance card) => new(card.Definition);
    internal static TurnRuleSet CardEffects(TurnRuleSet rules, ContentId card,
        ImmutableArray<Effect>? agent = null, ImmutableArray<Effect>? reveal = null,
        ImmutableArray<Effect>? acquire = null) => rules with
        {
            Cards = [.. rules.Cards.Select(x => x.Id == card ? x with
        {
            AgentEffects = agent ?? x.AgentEffects,
            RevealEffects = reveal ?? x.RevealEffects,
            AcquireEffects = acquire ?? x.AcquireEffects,
        } : x)],
        };

    internal static void ResolveAll(TurnEngine engine)
    {
        while (engine.DecisionOwnerId is { } owner)
        {
            var legal = engine.LegalActions(owner);
            var resolve = legal.OfType<ResolveEffectAction>().FirstOrDefault();
            if (resolve is not null) { Apply(engine, resolve); continue; }
            var option = legal.OfType<ChooseOptionAction>().FirstOrDefault();
            if (option is not null) { Apply(engine, option); continue; }
            var deploy = legal.OfType<DeployAction>().FirstOrDefault();
            if (deploy is not null) Apply(engine, deploy);
            break;
        }
    }

    internal static void FinishPlayerTurns(TurnEngine engine)
    {
        var guard = 0;
        var round = engine.Observe(0).Game.Round;
        while (engine.Phase == GamePhase.PlayerTurns && engine.Observe(0).Game.Round == round)
        {
            if (++guard > 1000) throw new InvalidOperationException("Fixture did not reach Combat boundary.");
            ResolveAll(engine);
            if (engine.Phase != GamePhase.PlayerTurns) break;
            var owner = engine.DecisionOwnerId!.Value;
            var legal = engine.LegalActions(owner);
            GameAction? next = legal.OfType<EndRevealAction>().FirstOrDefault();
            next ??= legal.OfType<RevealAction>().FirstOrDefault();
            Apply(engine, next ?? throw new InvalidOperationException("Fixture has no continuation."));
        }
    }

    internal static void Apply(TurnEngine engine, GameAction action)
    {
        var result = engine.Step(action);
        if (!result.IsSuccess) throw new InvalidOperationException(string.Join("; ", result.Diagnostics));
    }
}
