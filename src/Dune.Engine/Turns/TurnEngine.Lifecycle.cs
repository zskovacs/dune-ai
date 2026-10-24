using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Dune.Engine.Configuration;
using Dune.Engine.Content;
using Dune.Engine.Runtime;

namespace Dune.Engine.Turns;

public sealed partial class TurnEngine
{
    private DuneGameResult CreateResult()
    {
        var replay = CreateReplay("");
        var replayIdentity = Convert.ToHexStringLower(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(TurnReplayIdentityData.From(replay), ConfigurationJson.Options)));
        var standings = _state.Players.Select(player => new DuneStanding(player.Id,
            player.Resources.VictoryPoints, player.Resources.Spice, player.Resources.Solari,
            player.Resources.Water, player.Units.TroopsInGarrison, 0, [])).ToArray();
        var ranked = standings.OrderByDescending(x => x.VictoryPoints).ThenByDescending(x => x.Spice)
            .ThenByDescending(x => x.Solari).ThenByDescending(x => x.Water).ThenByDescending(x => x.GarrisonTroops).ToArray();
        var criteria = standings.ToDictionary(x => x.PlayerId, _ => ImmutableArray<DuneTiebreaker>.Empty);
        foreach (var tied in standings.GroupBy(x => x.VictoryPoints)) RecordSplits([.. tied], 0);
        var rank = 1;
        for (var i = 0; i < ranked.Length; i++)
        {
            if (i > 0 && !SameScore(ranked[i - 1], ranked[i])) rank = i + 1;
            var standing = ranked[i];
            standings[Array.FindIndex(standings, x => x.PlayerId == standing.PlayerId)] = standing with
            { Rank = rank, AppliedTiebreakers = criteria[standing.PlayerId] };
        }
        return new("dune-game-result-v1", replay.EngineVersion, replay.RuntimeVersion,
            _state.Definition.Setup!.Id, _state.ConfigurationIdentity, "dune-configuration-sha256-v1",
            _state.Definition.Fingerprint, _state.ContentIdentity, _rules.Version, _rulesIdentity,
            _rules.SupportVersion, _rules.Lifecycle!.Version, _state.Authenticity,
            replay.EnvironmentSeed, replay.LeaderSeed, _state.Round, _state.TerminalReason,
            [.. standings.OrderBy(x => x.PlayerId)], [.. standings.Where(x => x.Rank == 1).OrderBy(x => x.PlayerId).Select(x => x.PlayerId)],
            replayIdentity);

        void RecordSplits(DuneStanding[] group, int index)
        {
            if (group.Length < 2 || index == 4) return;
            var criterion = (DuneTiebreaker)index;
            var subgroups = group.GroupBy(x => criterion switch
            {
                DuneTiebreaker.Spice => x.Spice,
                DuneTiebreaker.Solari => x.Solari,
                DuneTiebreaker.Water => x.Water,
                _ => x.GarrisonTroops,
            }).ToArray();
            if (subgroups.Length > 1)
                foreach (var standing in group) criteria[standing.PlayerId] = criteria[standing.PlayerId].Add(criterion);
            foreach (var subgroup in subgroups) RecordSplits([.. subgroup], index + 1);
        }
    }

    private static bool SameScore(DuneStanding left, DuneStanding right) =>
        left.VictoryPoints == right.VictoryPoints && left.Spice == right.Spice && left.Solari == right.Solari &&
        left.Water == right.Water && left.GarrisonTroops == right.GarrisonTroops;

    private bool MentatAtStartingSpace => _state.Board.Mentat.OwnerId is null &&
        _state.Board.Mentat.Space == _state.Board.Definition.InitialMentatSpace;

    // Closing actions must establish that their automatic continuation is executable before mutation.
    private TurnDiagnostic? PreflightLifecycleContinuation()
    {
        if (_rules.Lifecycle is not { Cleanup: CombatCleanupPolicy.ReturnTroopsToSupply })
            return Unsupported("combat-cleanup", "This Combat cleanup policy is unsupported.");
        if (_state.Board.Spaces.Any(x => x.Definition.AccumulatesSpice && x.Occupants.IsEmpty && x.BonusSpice == int.MaxValue))
            return Content("resource-bounds", "Makers would exceed bonus spice bounds.");
        if (_state.Conflict.Deck.Count == 0) return null;
        // Current VP can still fall through pending signed Influence or a selected PostWin card.
        // Require the continuation's support conservatively until Recall makes the actual terminal check.
        if (_state.Round == int.MaxValue)
            return Content("round-bounds", "The next round exceeds round bounds.");
        // Check every enabled future definition, never disclose or depend on the hidden top card.
        var enabled = _state.Definition.Content.Where(x => x.Quantity > 0 && x.Category == ContentCategory.ConflictCard)
            .OrderBy(x => x.Id.Value, StringComparer.Ordinal);
        foreach (var definition in enabled)
        {
            var conflict = _rules.Lifecycle.Conflicts.FirstOrDefault(x => x.Id == definition.Id);
            if (conflict is null)
                return Unsupported("lifecycle-deck-support", "An enabled deck definition is unsupported.");
            foreach (var tier in conflict.Rewards)
                foreach (var player in _state.Players)
                    if (ValidateContext(tier.Effects, false, false, player.Id) is { } issue) return DeckDiagnostic(issue);
        }
        return null;
    }

    private void CompleteCombat()
    {
        if (_frames.Count != 0 || _state.DecisionWindow is not null)
            throw new InvalidOperationException("Combat cleanup cannot interrupt reward work.");
        switch (_rules.Lifecycle!.Cleanup)
        {
            case CombatCleanupPolicy.ReturnTroopsToSupply:
                foreach (var player in _state.Players)
                {
                    player.Units = player.Units with
                    {
                        TroopsInSupply = checked(player.Units.TroopsInSupply + player.Units.TroopsInConflict),
                        TroopsInConflict = 0,
                    };
                    player.Temporary = player.Temporary with { Swords = 0 };
                    player.CombatMarker = 0;
                }
                break;
            default:
                throw new InvalidOperationException("Unsupported cleanup passed preflight.");
        }
        _state.DecisionOwnerId = null;
        _combatOutcome = null;
        _conflictRewards = null;
        _revealing = false;
        _deploying = false;
        _recruited = 0;
        _oldGarrison = 0;
        AssertInvariants();
        RunMakers();
    }

    private void RunMakers()
    {
        _state.Phase = GamePhase.Makers;
        foreach (var space in _state.Board.Spaces.Where(x => x.Definition.AccumulatesSpice && x.Occupants.IsEmpty))
            space.BonusSpice = checked(space.BonusSpice + 1);
        RunRecall();
    }

    private void RunRecall()
    {
        _state.Phase = GamePhase.Recall;
        _state.TerminalReason = (_state.Players.Any(x => x.Resources.VictoryPoints >= 10)
            ? DuneTerminalReason.VictoryPointThreshold : DuneTerminalReason.None) |
            (_state.Conflict.Deck.Count == 0 ? DuneTerminalReason.ConflictDeckExhausted : DuneTerminalReason.None);
        if (_state.TerminalReason != DuneTerminalReason.None)
        {
            _state.Phase = GamePhase.Endgame;
            OpenPhaseWindow();
            return;
        }
        // ProtectedThroughRound is the round in which the borrowed Agent must return at Recall.
        // A reward earned in round R stores R+1: retain at R's Recall, return at (R+1)'s Recall.
        var mentat = _state.Board.Mentat;
        if (mentat.OwnerId is not null && (mentat.ProtectedThroughRound is null || _state.Round >= mentat.ProtectedThroughRound))
            _state.Board.Mentat = new(_state.Board.Definition.InitialMentatSpace, null);
        foreach (var player in _state.Players)
        {
            var retainedMentat = _state.Board.Mentat.OwnerId == player.Id ? 1 : 0;
            player.Units = player.Units with
            {
                AvailableAgents = checked(player.Units.OrdinaryAgents + player.Units.AcquiredSwordmasters + retainedMentat),
                AvailableSwordmasters = player.Units.AcquiredSwordmasters,
                AvailableMentats = retainedMentat,
            };
        }
        foreach (var space in _state.Board.Spaces) space.Occupants = [];
        _state.FirstPlayerId = (_state.FirstPlayerId + 1) % _state.Players.Count;
        BeginRound();
    }
}
