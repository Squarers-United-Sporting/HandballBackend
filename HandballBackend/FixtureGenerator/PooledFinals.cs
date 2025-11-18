using HandballBackend.EndpointHelpers;
using HandballBackend.EndpointHelpers.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.FixtureGenerator;

public class PooledFinals : AbstractFixtureGenerator {
    private readonly int _tournamentId;


    public PooledFinals(int tournamentId) : base(tournamentId, true, true) {
        _tournamentId = tournamentId;
    }

    public override async Task<bool> EndOfRound() {
        var db = ServiceLocator.Get<HandballContext>();
        var gameManager = ServiceLocator.Get<IGameManagementService>();
        var tournament = (await db.Tournaments.FindAsync(_tournamentId))!;

        var finalsGames = await db.Games.Where(g => g.TournamentId == _tournamentId && g.IsFinal).OrderBy(g => g.Id)
            .ToListAsync();

        if (finalsGames.Count > 2) {
            // each round is 2 games, so > 2 means we've had both rounds
            EndTournament();
            return true;
        }

        if (finalsGames.Count != 0) {
            await gameManager.CreateGame(_tournamentId, finalsGames[0].LosingTeamId, finalsGames[1].LosingTeamId,
                isFinal: true, round: finalsGames[0].Round + 1);
            await gameManager.CreateGame(_tournamentId, finalsGames[0].WinningTeamId!.Value,
                finalsGames[1].WinningTeamId!.Value, isFinal: true, round: finalsGames[0].Round + 1);
        } else {
            var (_, poolOne, poolTwo) = await LadderHelper.GetTournamentLadder(db, tournament);
            var lastGame = await db.Games.Where(g => g.TournamentId == _tournamentId).OrderByDescending(g => g.Id)
                .FirstAsync();
            await gameManager.CreateGame(_tournamentId, poolOne![0].Id, poolTwo![1].Id, isFinal: true,
                round: lastGame.Round + 1);
            await gameManager.CreateGame(_tournamentId, poolTwo[0].Id, poolOne[1].Id, isFinal: true,
                round: lastGame.Round + 1);
        }

        return await base.EndOfRound();
    }
}