using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Fixtures;
using Dune.Engine.Rulesets;
using Dune.Engine.Runtime;
using Dune.Engine.Setup;

namespace Dune.Engine.Tests;

public sealed class GameInitializerTests
{
    [Theory]
    [InlineData(3, 0, 10)]
    [InlineData(4, 1, 0)]
    public void SetupCreatesExactBoxedPoolsAndResourcesBeforeRoundStart(int count, int vp, int unused)
    {
        var c = OriginalImperiumFixture.Configuration(count);
        var d = OriginalImperiumFixture.Resolve(c);
        var result = OriginalImperiumFixture.Initializer.InitializeFixture(c, d);
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(x => x.Message)));
        var state = result.State!;
        Assert.Equal(0, state.Round);
        Assert.Equal(GamePhase.BeforeRoundStart, state.Phase);
        Assert.Null(state.DecisionOwnerId);
        Assert.Null(state.CurrentPlayerId);
        Assert.False(state.IsCompleted);
        Assert.InRange(state.FirstPlayerId, 0, count - 1);
        Assert.Equal(ContentAuthenticity.SyntheticFixture, state.Authenticity);
        Assert.Same(d, state.Definition);
        Assert.Equal(count, state.Players.Select(x => x.Leader).Distinct().Count());
        foreach (var player in state.Players)
        {
            Assert.Equal(new PlayerResources(0, 0, 1, vp), player.Resources);
            Assert.Equal(new PlayerUnits(2, 1, 9, 3, 0, 3) { OrdinaryAgents = 2 }, player.Units);
            Assert.Equal(4, player.Influence.Length);
            Assert.All(player.Influence, x => Assert.Equal(0, x.Amount));
            Assert.False(player.HasHighCouncilSeat);
            Assert.Equal(0, player.CombatMarker);
            Assert.Equal(new TemporaryPlayerState(0, 0, false), player.Temporary);
            Assert.Empty(player.ActiveEffects);
            Assert.Equal(10, player.Deck.Count);
            Assert.Equal(player.Id, player.Deck.OwnerId);
            Assert.Equal(ZoneVisibility.Hidden, player.Deck.Visibility);
            Assert.Empty(player.Hand.Cards);
            Assert.Empty(player.Discard.Cards);
            Assert.Empty(player.InPlay.Cards);
            Assert.Empty(player.Intrigue.Cards);
            Assert.All(new[] { player.Hand, player.Discard, player.InPlay, player.Intrigue },
                x => Assert.Equal(player.Id, x.OwnerId));
            foreach (var recipe in d.Setup!.StartingDeck)
                Assert.Equal(recipe.CopiesPerPlayer, player.Deck.Cards.Count(x => x.Definition == recipe.Definition));
        }

        Assert.Equal(new[] { 8, 10, 6 }, state.Shared.Reserves.Select(x => x.Count));
        Assert.All(state.Shared.Reserves, x => Assert.Equal(ZoneVisibility.Public, x.Visibility));
        Assert.Equal(5, state.Shared.ImperiumRow.Count);
        Assert.Equal(62, state.Shared.ImperiumDeck.Count);
        Assert.Equal(40, state.Shared.IntrigueDeck.Count);
        Assert.Equal(unused, state.Shared.UnusedStarters.Count);
        Assert.Null(state.Shared.UnusedStarters.OwnerId);
        Assert.Equal(ZoneVisibility.Hidden, state.Shared.UnusedStarters.Visibility);
        Assert.Equal(8, state.Conflict.Unused.Count);
        Assert.Same(state.Conflict, state.Board.Conflict);
        Assert.Equal(ZoneVisibility.Hidden, state.Conflict.Unused.Visibility);
        Assert.Empty(state.Conflict.Revealed.Cards);
        var tiers = state.Conflict.Deck.Cards.Select(x => d.Content.Single(y => y.Id == x.Definition).ConflictTier);
        Assert.Equal(new ConflictTier?[]
        {
            ConflictTier.I, ConflictTier.II, ConflictTier.II, ConflictTier.II,
            ConflictTier.II, ConflictTier.II, ConflictTier.III, ConflictTier.III, ConflictTier.III, ConflictTier.III
        }, tiers);
        var all = state.Zones.SelectMany(x => x.Cards).ToArray();
        Assert.Equal(189, all.Length);
        Assert.Equal(189, all.Select(x => x.Id).Distinct().Count());
        Assert.Contains(all.GroupBy(x => x.Definition),
            x => x.Count() > 1 && x.Select(y => y.Id).Distinct().Count() == x.Count());
        Assert.Equal(state.Zones.Count, state.Zones.Select(x => x.Id).Distinct().Count());
        Assert.Equal(22, state.Board.Spaces.Count);
        Assert.All(state.Board.Spaces, x =>
        {
            Assert.Empty(x.Occupants);
            Assert.Equal(0, x.BonusSpice);
            Assert.Null(x.ControllerId);
        });
        Assert.Equal(3, state.Board.Spaces.Count(x => x.Definition.Controllable));
        Assert.Equal(new MentatLocation(new BoardSpaceId("imperium:space:mentat:v1"), null), state.Board.Mentat);
        Assert.Equal(4, state.Board.Alliances.Length);
        Assert.All(state.Board.Alliances, x => Assert.Null(x.OwnerId));
        Assert.True(state.Board.Bank.IsUnlimited);
    }

    [Fact]
    public void SameInputsHaveCanonicalSnapshotsAndSeedsChangeOnlyTheirOwnRandomStreams()
    {
        var c = OriginalImperiumFixture.Configuration();
        var d = OriginalImperiumFixture.Resolve(c);
        var first = OriginalImperiumFixture.Create(c, d);
        var again = OriginalImperiumFixture.Create(c, d);
        Assert.Equal(first.CaptureTrustedSnapshot().CanonicalJson, again.CaptureTrustedSnapshot().CanonicalJson);
        Assert.Equal(first.CaptureTrustedSnapshot().Hash, again.CaptureTrustedSnapshot().Hash);
        var main = OriginalImperiumFixture.Create(c with { RandomSeed = 331 }, d);
        Assert.NotEqual(first.CaptureTrustedSnapshot().Hash, main.CaptureTrustedSnapshot().Hash);
        Assert.NotEqual(first.Shared.ImperiumDeck.Cards, main.Shared.ImperiumDeck.Cards);
        Assert.Equal(first.Players.Select(x => x.Leader), main.Players.Select(x => x.Leader));
        var leader = OriginalImperiumFixture.Create(c with { Leaders = c.Leaders with { Seed = 99 } }, d);
        Assert.Equal(first.Zones.SelectMany(x => x.Cards), leader.Zones.SelectMany(x => x.Cards));
        Assert.Equal(first.FirstPlayerId, leader.FirstPlayerId);
        Assert.Equal(first.Random.Checkpoint, leader.Random.Checkpoint);
        Assert.NotEqual(first.Players.Select(x => x.Leader), leader.Players.Select(x => x.Leader));
        Assert.NotEqual(first.ConfigurationIdentity, leader.ConfigurationIdentity);
        Assert.NotEqual(first.CaptureTrustedSnapshot().Hash, leader.CaptureTrustedSnapshot().Hash);
    }

    [Fact]
    public void RandomLeaderPoolIsCanonicalAndAssignmentUsesPlayerOrder()
    {
        var c = OriginalImperiumFixture.Configuration() with
        {
            Leaders = OriginalImperiumFixture.Configuration().Leaders with { PlayerOrder = [3, 2, 1, 0] },
        };
        var d = OriginalImperiumFixture.Resolve(c);
        var leaders = d.EligibleLeaders.OrderBy(x => x.Value, StringComparer.Ordinal).ToList();
        new SeededRandom(c.Leaders.Seed).Shuffle(leaders);
        var state = OriginalImperiumFixture.Create(c, d);
        for (var i = 0; i < c.Players.Count; i++)
            Assert.Equal(leaders[i], state.Players[c.Leaders.PlayerOrder[i]].Leader);
        var fixedConfig = c with
        {
            Leaders = c.Leaders with
            {
                Strategy = LeaderSelectionStrategy.Fixed,
                FixedAssignments = [.. state.Players.Reverse().Select(x => new LeaderAssignment(x.Id, x.Leader))]
            }
        };
        var fixedState = OriginalImperiumFixture.Create(fixedConfig, OriginalImperiumFixture.Resolve(fixedConfig));
        Assert.Equal(state.Players.Select(x => x.Leader), fixedState.Players.Select(x => x.Leader));
    }

    [Fact]
    public async Task ConcurrentInitializationSharesOnlyStaticDefinitions()
    {
        var c = OriginalImperiumFixture.Configuration();
        var d = OriginalImperiumFixture.Resolve(c);
        var states = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => Task.Run(() => OriginalImperiumFixture.Create(c, d))));
        Assert.All(states, x => Assert.Same(d, x.Definition));
        Assert.Single(states.Select(x => x.CaptureTrustedSnapshot().Hash).Distinct());
        var first = states[0];
        var second = states[1];
        Assert.NotSame(first.Players[0], second.Players[0]);
        Assert.NotSame(first.Players[0].Deck, second.Players[0].Deck);
        Assert.NotSame(first.Board.Spaces[0], second.Board.Spaces[0]);
        Assert.NotSame(first.Players[0].Deck.Cards[0], second.Players[0].Deck.Cards[0]);
        first.Players[0].Deck.TryDrawTo(first.Players[0].Hand);
        first.Board.Spaces[0].BonusSpice = 3;
        Assert.Equal(10, second.Players[0].Deck.Count);
        Assert.Empty(second.Players[0].Hand.Cards);
        Assert.Equal(0, second.Board.Spaces[0].BonusSpice);
    }

    [Fact]
    public void PublicInitializationNeverFallsBackToSyntheticOrIncompleteContent()
    {
        var c = OriginalImperiumFixture.Configuration();
        var normal = new GameInitializer().Create(c);
        Assert.Null(normal.State);
        Assert.Contains(normal.Errors, x => x.Code == "incomplete-catalog");
        var fixture = OriginalImperiumFixture.Initializer.Create(c);
        Assert.Null(fixture.State);
        Assert.Contains(fixture.Errors, x => x.Code == "unverified-content");
        Assert.Contains(fixture.Errors, x => x.Code == "incomplete-catalog");
        var mislabeled = new GameInitializer(new RulesetComposer(new ContentCatalog(
            OriginalImperiumFixture.Catalog.Definitions,
            CatalogCompleteness.Complete))).Create(c);
        Assert.Null(mislabeled.State);
        Assert.Contains(mislabeled.Errors, x => x.Code == "unverified-content");
        Assert.Contains(mislabeled.Errors, x => x.Code == "unsupported-content-source");
    }

    [Fact]
    public void FixtureValidationStillRejectsWrongQuantitiesIdentitiesAndConflictTiers()
    {
        var c = OriginalImperiumFixture.Configuration();
        foreach (var catalog in new[]
                 {
                     new ContentCatalog(OriginalImperiumFixture.Catalog.Definitions.Where(x =>
                         x.Category != ContentCategory.ImperiumCard || !x.Id.Value.Contains(":0:v1"))),
                     new ContentCatalog(OriginalImperiumFixture.Catalog.Definitions.Select(x =>
                         x.Category == ContentCategory.ReserveCard
                             ? x with { Id = new("fixture:wrong-reserve:" + x.Quantity) }
                             : x)),
                     new ContentCatalog(OriginalImperiumFixture.Catalog.Definitions.Select(x =>
                         x.Category == ContentCategory.ConflictCard && x.ConflictTier == ConflictTier.I
                             ? x with { ConflictTier = ConflictTier.II }
                             : x)),
                 })
        {
            var d = new RulesetComposer(catalog).Compose(c).Definition!;
            var result = new GameInitializer().InitializeFixture(c, d);
            Assert.Null(result.State);
            Assert.NotEmpty(result.Errors);
        }
    }

    [Fact]
    public void InitializeRejectsMismatchedOrModifiedResolvedDefinitions()
    {
        var c = OriginalImperiumFixture.Configuration();
        var d = OriginalImperiumFixture.Resolve(c);
        var initializer = OriginalImperiumFixture.Initializer;
        Assert.Contains(initializer.InitializeFixture(OriginalImperiumFixture.Configuration(3), d).Errors,
            x => x.Code == "configuration-mismatch");
        Assert.Contains(initializer.InitializeFixture(c, d with { EligibleLeaders = [] }).Errors,
            x => x.Code == "ineligible-leader");
        Assert.Contains(initializer.InitializeFixture(c, d with { Setup = d.Setup! with { Id = "modified" } }).Errors,
            x => x.Code == "unsupported-setup");
        Assert.Contains(initializer.InitializeFixture(c, d with { Content = d.Content.RemoveAt(0) }).Errors,
            x => x.Code == "definition-mismatch");
        Assert.Contains(initializer.InitializeFixture(c, d with { Content = default }).Errors,
            x => x.Code == "invalid-definition");
        Assert.Contains(initializer.InitializeFixture(c, d with { Modules = [null!] }).Errors,
            x => x.Code == "unsupported-setup");
        Assert.Contains(initializer.InitializeFixture(c, d with { Definitions = [null!] }).Errors,
            x => x.Code == "unsupported-setup");
        var custom = new RulesetComposer(OriginalImperiumFixture.Catalog, d.Modules).Compose(c).Definition!;
        Assert.Contains(initializer.InitializeFixture(c, custom).Errors, x => x.Code == "unsupported-setup");
    }

    [Fact]
    public void FixedAndRandomLeaderFailurePathsReturnNoPartialState()
    {
        var c = OriginalImperiumFixture.Configuration();
        var pool = OriginalImperiumFixture.Resolve(c).EligibleLeaders;
        var fixedConfig = c with
        {
            Leaders = c.Leaders with
            {
                Strategy = LeaderSelectionStrategy.Fixed,
                FixedAssignments = [.. Enumerable.Range(0, 4).Select(i => new LeaderAssignment(i, pool[i]))]
            }
        };
        var d = OriginalImperiumFixture.Resolve(fixedConfig);
        var duplicate = fixedConfig with
        {
            Leaders = fixedConfig.Leaders with
            {
                FixedAssignments = [.. Enumerable.Range(0, 4).Select(i => new LeaderAssignment(i, pool[0]))]
            }
        };
        Assert.Null(OriginalImperiumFixture.Initializer.Create(duplicate).State);
        Assert.Contains(OriginalImperiumFixture.Initializer.InitializeFixture(duplicate, d).Errors,
            x => x.Code == "leader-assignment");
        var unavailable = fixedConfig with
        {
            Leaders = fixedConfig.Leaders with
            {
                FixedAssignments =
                fixedConfig.Leaders.FixedAssignments.SetItem(0, new(0, new("fixture:unavailable-leader:v1")))
            }
        };
        Assert.Contains(OriginalImperiumFixture.Initializer.Create(unavailable).Errors,
            x => x.Code == "ineligible-leader");
        var candidate = c with
        {
            Leaders = c.Leaders with { CandidateLeaders = [new("fixture:unavailable-leader:v1")] }
        };
        Assert.Contains(OriginalImperiumFixture.Initializer.Create(candidate).Errors,
            x => x.Code == "ineligible-leader");
        var excluded = fixedConfig with { Content = fixedConfig.Content with { Excluded = [pool[0]] } };
        Assert.Contains(OriginalImperiumFixture.Initializer.Create(excluded).Errors,
            x => x.Code == "ineligible-leader");
        // Matching config identity alone cannot bypass the eligibility check on Initialize.
        var badResolved = d with { ConfigurationIdentity = ConfigurationIdentity.Compute(unavailable) };
        Assert.Contains(OriginalImperiumFixture.Initializer.InitializeFixture(unavailable, badResolved).Errors,
            x => x.Code == "ineligible-leader");
    }

    [Theory]
    [InlineData("uprising")]
    [InlineData("ix")]
    [InlineData("epic")]
    [InlineData("variant")]
    [InlineData("solo")]
    [InlineData("draft")]
    [InlineData("snake")]
    public void UnsupportedConfigurationsReturnExplicitDiagnostics(string mode)
    {
        var c = OriginalImperiumFixture.Configuration();
        c = mode switch
        {
            "uprising" => c with { BaseGame = BaseGame.Uprising },
            "ix" => c with { Expansions = [Expansion.RiseOfIx] },
            "epic" => c with { Mode = GameMode.Epic },
            "variant" => c with { Variants = [RuleVariant.GoTo11] },
            "solo" => c with { Players = new(PlayerFormat.SoloWithRivals, 1) },
            "draft" => c with { Leaders = c.Leaders with { Strategy = LeaderSelectionStrategy.SequentialDraft } },
            _ => c with { Leaders = c.Leaders with { Strategy = LeaderSelectionStrategy.SnakeDraft } },
        };
        var result = OriginalImperiumFixture.Initializer.Create(c);
        Assert.Null(result.State);
        Assert.Contains(result.Errors, x => x.Code == "unsupported-setup");
    }

    [Fact]
    public void ObservationsRedactNonemptyOpponentPrivateZonesAndRemainDetached()
    {
        var state = OriginalImperiumFixture.Create();
        foreach (var player in state.Players)
        {
            player.Deck.TryDrawTo(player.Hand);
            state.Shared.IntrigueDeck.TryDrawTo(player.Intrigue);
            player.Deck.TryDrawTo(player.Discard);
            player.Deck.TryDrawTo(player.InPlay);
        }

        state.Players[1].ActiveEffects = [new(state.Players[1].Intrigue.Cards[0].Definition, "private-fixture-effect")];
        state.ActiveEffects = [new(state.Players[1].Intrigue.Cards[0].Definition, "private-fixture-game-effect")];
        var observation = state.Observe(0);
        Assert.Single(observation.Hand);
        Assert.Single(observation.Intrigue);
        Assert.Equal(state.Players[0].Hand.Cards[0].Definition, observation.Hand[0].Definition);
        Assert.Equal(state.Players[0].Intrigue.Cards[0].Definition, observation.Intrigue[0].Definition);
        Assert.All(observation.Players, x =>
        {
            Assert.Equal(1, x.HandCount);
            Assert.Equal(1, x.IntrigueCount);
            Assert.Equal(7, x.DeckCount);
        });
        var serialized = JsonSerializer.Serialize(observation);
        foreach (var player in state.Players.Skip(1))
            Assert.DoesNotContain(player.Intrigue.Cards[0].Definition.Value, serialized);
        Assert.DoesNotContain("Seed", serialized);
        Assert.DoesNotContain("Checkpoint", serialized);
        Assert.DoesNotContain("Configuration", serialized);
        Assert.DoesNotContain("Fingerprint", serialized);
        Assert.DoesNotContain("CardInstance", serialized);
        Assert.DoesNotContain("RawDrawCount", serialized);
        Assert.DoesNotContain("private-fixture", serialized);
        Assert.DoesNotContain("outside:unused", serialized);
        Assert.DoesNotContain(state.Shared.ImperiumDeck.Cards[0].Definition.Value, serialized);
        Assert.DoesNotContain(state.Conflict.Deck.Cards[0].Definition.Value, serialized);
        Assert.Equal(10, observation.StartingComposition.Sum(x => x.CopiesPerPlayer));
        Assert.Equal(JsonSerializer.Serialize(observation.PublicZones),
            JsonSerializer.Serialize(state.Observe(1).PublicZones));
        Assert.Equal(JsonSerializer.Serialize(observation.Players), JsonSerializer.Serialize(state.Observe(1).Players));
        Assert.Equal(state.Players[1].Discard.Cards[0].Definition, observation.Players[1].Discard[0].Definition);
        var opponent = state.Players[1];
        var oldHand = opponent.Hand.Cards[0];
        opponent.Hand.MoveTo(oldHand.Id, opponent.Deck);
        opponent.Deck.MoveTo(opponent.Deck.Cards.First(x => x.Definition != oldHand.Definition).Id, opponent.Hand);
        opponent.Intrigue.MoveTo(opponent.Intrigue.Cards[0].Id, state.Shared.IntrigueDeck);
        state.Shared.IntrigueDeck.TryDrawTo(opponent.Intrigue);
        Assert.Equal(serialized, JsonSerializer.Serialize(state.Observe(0)));
        var changedCopy = observation with { Hand = [], Players = [] };
        Assert.Single(state.Players[0].Hand.Cards);
        Assert.Empty(changedCopy.Hand);
        Assert.Throws<NotSupportedException>(() => ((IList<ObservedCard>)observation.Hand).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<CardInstance>)state.Players[0].Deck.Cards).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<PlayerState>)state.Players).Clear());
        var snapshot = state.CaptureTrustedSnapshot();
        state.Players[0].Deck.TryDrawTo(state.Players[0].Hand);
        state.Players[0].Resources = new(7, 8, 9, 10);
        state.Board.Spaces[0].Occupants = [new(0, AgentKind.Agent)];
        Assert.Single(observation.Hand);
        Assert.Equal(0, observation.Players[0].Resources.Solari);
        Assert.Empty(observation.Board.Spaces[0].Occupants);
        Assert.Equal(serialized, JsonSerializer.Serialize(observation));
        Assert.Single(snapshot.Data.Zones.Single(x => x.Id == state.Players[0].Hand.Id).Cards);
        Assert.Empty(snapshot.Data.Board.Spaces[0].Occupants);
        Assert.NotEqual(snapshot.Hash, state.CaptureTrustedSnapshot().Hash);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Observe(99));
    }

    [Fact]
    public void TrustedSnapshotContainsAllPhysicalCardsSeedsAndTemporaryState()
    {
        var state = OriginalImperiumFixture.Create(3, 121, 77);
        state.Players[0].Temporary = new(3, 4, true);
        state.Players[0].ActiveEffects = [new(state.Players[0].Leader, "fixture-effect")];
        state.ActiveEffects = [new(state.Players[1].Leader, "fixture-game-effect")];
        var snapshot = state.CaptureTrustedSnapshot();
        Assert.Contains("HIDDEN DATA", snapshot.DebugLabel);
        Assert.Equal(189, snapshot.Data.Zones.Sum(x => x.Cards.Length));
        Assert.Equal(121, snapshot.Data.MainRandom.Seed);
        Assert.Equal(77, snapshot.Data.LeaderRandom.Seed);
        Assert.Equal(state.FirstPlayerId, snapshot.Data.FirstPlayerId);
        Assert.Equal(state.Random.Checkpoint, snapshot.Data.MainRandom);
        Assert.Equal(state.LeadersRandom.Checkpoint, snapshot.Data.LeaderRandom);
        Assert.Equal(state.Definition.Fingerprint, snapshot.Data.RulesetIdentity);
        Assert.Equal(state.ConfigurationIdentity, snapshot.Data.ConfigurationIdentity);
        Assert.Equal(ContentAuthenticity.SyntheticFixture, snapshot.Data.Authenticity);
        Assert.Equal(new TemporaryPlayerState(3, 4, true), snapshot.Data.Players[0].Temporary);
        Assert.Single(snapshot.Data.Players[0].ActiveEffects);
        Assert.Single(snapshot.Data.ActiveEffects);
        Assert.Equal(state.Conflict.Deck.Cards, snapshot.Data.Zones.Single(x => x.Id == state.Conflict.Deck.Id).Cards);
        state.Extensions = [new UnsupportedFixtureExtension()];
        Assert.Throws<NotSupportedException>(() => state.CaptureTrustedSnapshot());
    }

    private sealed class UnsupportedFixtureExtension : ModuleExtensionState;

    [Fact]
    public void MissingInputsReturnDiagnosticsWithoutState()
    {
        var initializer = new GameInitializer();
        Assert.Contains(initializer.Create(null).Errors, x => x.Code == "required");
        Assert.Null(initializer.Create(null).State);
        Assert.Contains(initializer.Initialize(OriginalImperiumFixture.Configuration(), null).Errors,
            x => x.Code == "required");
        Assert.Contains(
            initializer.Initialize(null, OriginalImperiumFixture.Resolve(OriginalImperiumFixture.Configuration()))
                .Errors,
            x => x.Code == "required");
    }
}
