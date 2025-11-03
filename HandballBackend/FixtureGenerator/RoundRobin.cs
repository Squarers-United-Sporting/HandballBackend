using HandballBackend.Database;
using HandballBackend.EndpointHelpers.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.FixtureGenerator;

public class RoundRobin : AbstractFixtureGenerator {
    private readonly int _tournamentId;
    private readonly bool _blitz;


    public RoundRobin(int tournamentId, bool blitz = false) : base(tournamentId, true, true) {
        _tournamentId = tournamentId;
        _blitz = blitz;
    }


    public override async Task<bool> EndOfRound() {
        var db = new HandballContext();
        var tournament = (await db.Tournaments.FindAsync(_tournamentId))!;

        var rounds = await db.Games
            .Where(g => g.TournamentId == _tournamentId)
            .OrderByDescending(g => g.Round).Select(g => g.Round).FirstOrDefaultAsync();

        var teams = await db.TournamentTeams.Where(t => t.TournamentId == _tournamentId).IncludeRelevant()
            .Select(t => t.Team).ToListAsync();
        if (teams.Count <= rounds + 1) {
            // we are now in finals
            tournament.InFinals = true;
            await db.SaveChangesAsync();
            return true;
        }

        for (var i = 0; i < rounds; i++) {
            teams.Insert(1, teams.Last());
            teams.RemoveAt(teams.Count - 1);
        }


        for (var i = 0; i < teams.Count / 2; i++) {
            var teamOne = teams[i];
            var teamTwo = teams[teams.Count - i - 1];
            await GameManager.CreateGame(_tournamentId, teamOne.Id, teamTwo.Id, blitzGame: _blitz, round: rounds + 1);
        }

        await db.SaveChangesAsync();
        return await base.EndOfRound();
    }
}