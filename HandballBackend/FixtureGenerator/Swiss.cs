using HandballBackend.Database.Models;
using HandballBackend.Database.SendableTypes;
using HandballBackend.EndpointHelpers;
using HandballBackend.EndpointHelpers.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.FixtureGenerator;

//TODO: Test this file; I got lazy and used deepseek
public class Swiss : AbstractFixtureGenerator {
    private readonly int _tournamentId;


    public Swiss(int tournamentId, FixtureGeneratorService fixtureGen) : base(tournamentId, fixtureGen, true, true) {
        _tournamentId = tournamentId;
    }


    public override async Task<bool> EndOfRound() {
        var db = FixtureGen.Context;
        var gameManager = FixtureGen.GameManager;

        var tournament = (await db.Tournaments.FindAsync(_tournamentId))!;
        var (ladder, _, _) = await LadderHelper.GetTournamentLadder(db, tournament);


        var currentRound = await db.Games
            .Where(g => g.TournamentId == _tournamentId)
            .MaxAsync(g => (int?) g.Round) ?? 0;
        var nextRound = currentRound + 1;

        if (nextRound > Math.Ceiling(Math.Log2(ladder!.Length))) {
            return true;
        }

        var remainingTeams = new List<TeamData>(ladder);
        List<(int team1, int team2)> pairings = [];

        while (remainingTeams.Count >= 2) {
            var topTeam = remainingTeams.First();
            remainingTeams.Remove(topTeam);

            TeamData? opponent = null;
            foreach (var potentialOpponent in remainingTeams) {
                if (await HaveTeamsPlayed(db, topTeam.Id, potentialOpponent.Id)) continue;
                opponent = potentialOpponent;
                break;
            }

            opponent ??= remainingTeams.First();
            remainingTeams.Remove(opponent);

            pairings.Add((topTeam.Id, opponent.Id));
        }

        if (remainingTeams.Count == 1) {
            pairings.Add((remainingTeams.First().Id, 1));
        }

        foreach (var pairing in pairings) {
            await gameManager.CreateGame(
                tournamentId: _tournamentId,
                teamOneId: pairing.team1,
                teamTwoId: pairing.team2,
                round: nextRound
            );
        }

        return false;
    }

    private async Task<bool> HaveTeamsPlayed(HandballContext db, int teamOneId, int teamTwoId) {
        return await db.Games.AnyAsync(g =>
            g.TournamentId == _tournamentId &&
            ((g.TeamOneId == teamOneId && g.TeamTwoId == teamTwoId) ||
             (g.TeamOneId == teamTwoId && g.TeamTwoId == teamOneId))
        );
    }
}