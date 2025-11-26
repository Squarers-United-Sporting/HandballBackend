using HandballBackend.EndpointHelpers;
using HandballBackend.EndpointHelpers.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.FixtureGenerator;

public class TopThreeFinals : AbstractFixtureGenerator {
    private readonly int _tournamentId;


    public TopThreeFinals(int tournamentId, FixtureGeneratorService fixtureGen) : base(tournamentId, fixtureGen, true,
        true) {
        _tournamentId = tournamentId;
    }

    public override async Task<bool> EndOfRound() {
        var db = FixtureGen.Context;
        var gameManager = FixtureGen.GameManager;
        var tournament = (await db.Tournaments.FindAsync(_tournamentId))!;

        var finalsGames = await db.Games.Where(g => g.TournamentId == _tournamentId && g.IsFinal).OrderBy(g => g.Id)
            .ToListAsync();

        if (finalsGames.Count > 1) {
            EndTournament();
            return true;
        }

        var (ladder, _, _) = await LadderHelper.GetTournamentLadder(db, tournament);
        if (finalsGames.Count != 0) {
            await gameManager.CreateGame(_tournamentId, ladder![0].Id, finalsGames[0].WinningTeamId!.Value,
                isFinal: true, round: finalsGames[0].Round + 1);
        } else {
            var lastGame = await db.Games.Where(g => g.TournamentId == _tournamentId).OrderByDescending(g => g.Id)
                .FirstAsync();
            await gameManager.CreateGame(_tournamentId, ladder![1].Id, ladder[2].Id, isFinal: true,
                round: lastGame.Round + 1);
        }

        return await base.EndOfRound();
    }
}