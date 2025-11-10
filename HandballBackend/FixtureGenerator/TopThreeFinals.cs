using HandballBackend.EndpointHelpers;
using HandballBackend.EndpointHelpers.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.FixtureGenerator;

public class TopThreeFinals : AbstractFixtureGenerator {
    private readonly int _tournamentId;


    public TopThreeFinals(int tournamentId) : base(tournamentId, true, true) {
        _tournamentId = tournamentId;
    }

    public override async Task<bool> EndOfRound() {
        var db = new HandballContext();
        var tournament = (await db.Tournaments.FindAsync(_tournamentId))!;

        var finalsGames = await db.Games.Where(g => g.TournamentId == _tournamentId && g.IsFinal).OrderBy(g => g.Id).ToListAsync();

        if (finalsGames.Count > 1) {
            EndTournament();
            return true;
        }
        var (ladder, _, _) = await LadderHelper.GetTournamentLadder(db, tournament);
        if (finalsGames.Count != 0) {
            await GameManager.CreateGame(_tournamentId, ladder![0].Id, finalsGames[0].WinningTeamId!.Value,
                isFinal: true, round: finalsGames[0].Round + 1);
        } else {
            var lastGame = await db.Games.Where(g => g.TournamentId == _tournamentId).OrderByDescending(g => g.Id).FirstAsync();
            await GameManager.CreateGame(_tournamentId, ladder![1].Id, ladder[2].Id, isFinal: true,
                round: lastGame.Round + 1);
        }

        return await base.EndOfRound();
    }
}