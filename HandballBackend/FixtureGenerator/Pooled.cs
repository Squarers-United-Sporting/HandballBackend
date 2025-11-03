using HandballBackend.Database;
using HandballBackend.Database.Models;
using HandballBackend.EndpointHelpers.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.FixtureGenerator;

public class Pooled : AbstractFixtureGenerator {
    private readonly int _tournamentId;
    private readonly bool _blitz;


    public Pooled(int tournamentId, bool blitz = false) : base(tournamentId, true, true) {
        _tournamentId = tournamentId;
        _blitz = blitz;
    }

    public override async Task BeginTournament() {
        var db = new HandballContext();
        var tournament = (await db.Tournaments.FindAsync(_tournamentId))!;
        tournament.IsPooled = true;

        var teams = (await db.TournamentTeams
            .Where(t => t.TournamentId == _tournamentId)
            .IncludeRelevant()
            .ToArrayAsync()).OrderByDescending(t => t.Team.TrueElo()).ToList();

        var pool = 0;
        foreach (var team in teams) {
            team.Pool = 1 + pool;
            pool = 1 - pool;
        }

        await db.SaveChangesAsync();
        await base.BeginTournament();
    }

    public override async Task<bool> EndOfRound() {
        var db = new HandballContext();
        var tournament = (await db.Tournaments.FindAsync(_tournamentId))!;
        var tournamentTeams = await db.TournamentTeams
            .Where(t => t.TournamentId == _tournamentId)
            .IncludeRelevant()
            .ToListAsync();

        var rounds = await db.Games
            .Where(g => g.TournamentId == _tournamentId)
            .OrderByDescending(g => g.Round).Select(g => g.Round).FirstOrDefaultAsync();

        var poolOne = tournamentTeams.Where(tt => tt.Pool == 1).Select(tt => tt.Team).OrderBy(t => t.Id).ToList();
        var poolTwo = tournamentTeams.Where(tt => tt.Pool == 2).Select(tt => tt.Team).OrderBy(t => t.Id).ToList();
        if (poolOne.Count % 2 != 0) {
            poolOne.Add(db.Teams.First(t => t.Id == 1));
        }

        if (poolTwo.Count % 2 != 0) {
            poolTwo.Add(db.Teams.First(t => t.Id == 1));
        }

        if (Math.Min(poolTwo.Count, poolOne.Count) <= rounds + 1) {
            // we are now in finals
            tournament.InFinals = true;
            await db.SaveChangesAsync();
            return true;
        }

        for (var i = 0; i < rounds; i++) {
            poolOne.Insert(1, poolOne.Last());
            poolOne.RemoveAt(poolOne.Count - 1);
            poolTwo.Insert(1, poolTwo.Last());
            poolTwo.RemoveAt(poolTwo.Count - 1);
        }

        for (var i = 0; i < poolOne.Count / 2; i++) {
            var teamOne = poolOne[i];
            var teamTwo = poolOne[poolOne.Count - i - 1];
            await GameManager.CreateGame(_tournamentId, teamOne.Id, teamTwo.Id, blitzGame: _blitz,
                round: rounds + 1);
        }

        for (var i = 0; i < poolTwo.Count / 2; i++) {
            var teamOne = poolTwo[i];
            var teamTwo = poolTwo[poolTwo.Count - i - 1];
            await GameManager.CreateGame(_tournamentId, teamOne.Id, teamTwo.Id, blitzGame: _blitz,
                round: rounds + 1);
        }

        await db.SaveChangesAsync();
        return await base.EndOfRound();
    }
}