using HandballBackend.EndpointHelpers;
using System.Runtime.CompilerServices;
using HandballBackend.Controllers;
using HandballBackend.Database;
using HandballBackend.Database.Models;
using Microsoft.EntityFrameworkCore;

[assembly: InternalsVisibleTo("HandballBackend.Tests")]


namespace HandballBackend.FixtureGenerator;

public abstract class AbstractFixtureGenerator(int tournamentId, bool fillOfficials, bool fillCourts) {
    public enum UmpiringProficiencies {
        BestOfficial = 3,
        MiddleOfficial = 2,
        BadOfficial = 1,
        NotOfficial = 0,
        EmergencyOfficial = -1
    }

    private static readonly Dictionary<string, Func<int, AbstractFixtureGenerator>> FixtureGenerators = new();
    private static readonly Dictionary<string, Func<int, AbstractFixtureGenerator>> FinalsGenerators = new();
    private static bool _isPopulated = false;

    private static void Register(Func<int, AbstractFixtureGenerator> func, string name, bool isFinal) {
        if (isFinal) {
            FinalsGenerators[name] = func;
        } else {
            FixtureGenerators[name] = func;
        }
    }

    private static void PopulateFixtures() {
        _isPopulated = true;
        Register(tid => new OneRound(tid), "OneRound", false);
        Register(tid => new Pooled(tid), "Pooled", false);
        Register(tid => new RoundRobin(tid), "RoundRobin", false);
        Register(tid => new Swiss(tid), "Swiss", false);
        Register(tid => new Pooled(tid, blitz: true), "PooledBlitz", false);
        Register(tid => new RoundRobin(tid, blitz: true), "RoundRobinBlitz", false);


        Register(tid => new PooledFinals(tid), "PooledFinals", true);
        Register(tid => new BasicFinals(tid), "BasicFinals", true);
        Register(tid => new TopThreeFinals(tid), "TopThreeFinals", true);
    }

    public static AbstractFixtureGenerator GetControllerByName(string name, int tournamentId) {
        if (!_isPopulated) {
            PopulateFixtures();
        }

        if (FixtureGenerators.TryGetValue(name, out var func) || FinalsGenerators.TryGetValue(name, out func)) {
            return func(tournamentId);
        }

        throw new ArgumentException($"Unknown fixture generator {name}");
    }

    public static List<string> GetFixtureGeneratorNames() {
        if (!_isPopulated) {
            PopulateFixtures();
        }

        return FixtureGenerators.Keys.ToList();
    }

    public static List<string> GetFinalsGeneratorNames() {
        if (!_isPopulated) {
            PopulateFixtures();
        }

        return FinalsGenerators.Keys.ToList();
    }


    public virtual async Task<bool> EndOfRound() {
        if (fillCourts) {
            await AddCourts();
        }

        if (fillOfficials) {
            await AddUmpires();
        }

        return false;
    }

    protected void EndTournament(
        string note = "Thank you for participating in the tournament! We look forward to seeing you next time.") {
        var db = new HandballContext();
        var tournament = db.Tournaments.Find(tournamentId)!;
        tournament.Finished = true;
        tournament.Notes = note;
        db.SaveChanges();
        _ = Task.Run(() => PostgresBackup.MakeTimestampedBackup("Post Tournament Backup"));
    }

    public virtual async Task BeginTournament() {
        var db = new HandballContext();
        await EndOfRound();
        await db.SaveChangesAsync();
    }


    internal async Task AddCourts(int rounds = -1) {
        var db = new HandballContext();
        // Get the highest round number

        if (rounds == -1) {
            rounds = (await db.Games
                .Where(g => g.TournamentId == tournamentId)
                .OrderByDescending(g => g.Round)
                .FirstOrDefaultAsync())?.Round ?? 0;
        }

        var games = await db.Games
            .Where(g => g.TournamentId == tournamentId &&
                        g.Round == rounds &&
                        !g.IsBye &&
                        !g.Started &&
                        !g.IsFinal)
            .Include(g => g.TeamOne.PlayerGameStats.Where(pgs => pgs.Game.TournamentId == tournamentId))
            .ThenInclude(pgs => pgs.Game)
            .Include(g => g.TeamTwo.PlayerGameStats.Where(pgs => pgs.Game.TournamentId == tournamentId))
            .ThenInclude(pgs => pgs.Game)
            .ToListAsync();
        var tourney = db.Tournaments
            .FirstOrDefault(t => t.Id == tournamentId);

        var finals = await db.Games
            .Where(g => g.TournamentId == tournamentId &&
                        g.Round == rounds &&
                        !g.IsBye &&
                        !g.Started &&
                        g.IsFinal)
            .ToListAsync();

        // Calculate the split point
        var splitPoint = (int) Math.Ceiling(games.Count / 2.0) - 1;

        // Sort games by combined wins of both teams
        games = games.OrderByDescending(g =>
                g.TeamOne.ToSendableData(true, tournament: tourney).Stats!["Games Won"] +
                g.TeamTwo.ToSendableData(true, tournament: tourney).Stats!["Games Won"])
            .ToList();

        // Assign courts (0 for first half, 1 for second half)
        for (var i = 0; i < games.Count; i++) {
            games[i].Court = i > splitPoint ? 1 : 0;
        }

        // All finals go to court 0
        foreach (var final in finals) {
            final.Court = 0;
        }

        await db.SaveChangesAsync();
    }

    internal class UmpiringSolution {
        public required int GameId;
        public required int CourtId;
        public required List<int> PlayerIds;
        public OfficialContainer? Official;
        public OfficialContainer? Scorer;
    }

    internal class OfficialContainer {
        public required string Name;
        public int PlayerId;
        public int OfficialId;
        public int GamesUmpired;
        public int GamesScored;
        public UmpiringProficiencies UmpireProficiency;
        public UmpiringProficiencies ScorerProficiency;
    }

    private async Task AddUmpires() {
        var db = new HandballContext();
        var games = await db.Games.Where(g => g.TournamentId == tournamentId && !g.Started && !g.IsBye)
            .IncludeRelevant().ToListAsync();
        if (games.Count <= 0) return;
        var tournament = (await db.Tournaments.FindAsync(tournamentId))!;
        var round = games.Max(g => g.Round);
        var courtOneGames = games.Where(g => g.Court == 0).ToList();
        var courtTwoGames = games.Where(g => g.Court == 1).Cast<Game?>().ToList();
        var officials =
            db.TournamentOfficials
                .Where(to => to.TournamentId == tournamentId)
                .Include(to => to.Official.Person)
                .Include(to => to.Official.Games.Where(g => g.TournamentId == tournamentId && g.Round < round))
                .Include(to =>
                    to.Official.ScoredGames.Where(g => g.TournamentId == tournamentId && g.Round < round))
                .ToList()
                .Select(to => new OfficialContainer {
                    PlayerId = to.Official.PersonId,
                    OfficialId = to.OfficialId,
                    GamesUmpired = to.Official.Games.Count(g => g.TournamentId == tournamentId && g.Round < round),
                    Name = to.Official.Person.Name,
                    GamesScored = to.Official.ScoredGames.Count(g => g.TournamentId == tournamentId && g.Round < round),
                    UmpireProficiency = (UmpiringProficiencies) to.UmpireProficiency,
                    ScorerProficiency = (UmpiringProficiencies) to.ScorerProficiency
                }).OrderBy(o => o.GamesUmpired).ToList();

        while (courtOneGames.Count > courtTwoGames.Count) {
            courtTwoGames.Add(null);
        }

        var solution = new List<(UmpiringSolution, UmpiringSolution?)>();
        foreach (var (courtOne, courtTwo) in courtOneGames.Zip(courtTwoGames)) {
            var courtOneGame = new UmpiringSolution {
                GameId = courtOne.Id,
                CourtId = 0,
                PlayerIds = courtOne.TeamOne.People.Concat(courtOne.TeamTwo.People).Select(p => p.Id).ToList(),
            };
            var courtTwoGame = courtTwo == null
                ? null
                : new UmpiringSolution {
                    GameId = courtTwo.Id,
                    CourtId = 1,
                    PlayerIds = courtTwo.TeamOne.People.Concat(courtTwo.TeamTwo.People).Select(p => p.Id).ToList(),
                };
            solution.Add(new(courtOneGame, courtTwoGame));
        }

        var solutionArray = solution.ToArray();
        if (!TrySolution(solutionArray, officials, requiresScorer: tournament.HasScorer)) {
            //the solution found no possible result
            TrySolution(solutionArray, officials, 0, true, false, true, requiresScorer: true);
        }

        foreach (var soln in solution.SelectMany(i => new[] { i.Item1, i.Item2 }).Where(i => i != null)
                     .Cast<UmpiringSolution>()) {
            var game = games.First(g => g.Id == soln.GameId);
            if (soln.Official!.OfficialId > 0) {
                game.OfficialId = soln.Official.OfficialId;
            }

            if (tournament.HasScorer && soln.Scorer!.OfficialId > 0) {
                game.ScorerId = soln.Scorer.OfficialId;
            }
        }

        await db.SaveChangesAsync();
    }

    internal static bool TrySolution((UmpiringSolution, UmpiringSolution?)[] solutions,
        List<OfficialContainer> officials,
        int index = 0,
        bool courtOne = true,
        bool scorer = false,
        bool force = false,
        bool requiresScorer = true) {
        if (index >= solutions.Length) {
            if (scorer || !requiresScorer) return true;
            // we have reached the last game
            if (!TrySolution(solutions, officials, 0, true, true)) {
                return TrySolution(solutions, officials, 0, true, true, true);
            }

            return true;
        }

        var (myGame, otherGame) = solutions[index];
        if (!courtOne) {
            (myGame, otherGame) = (otherGame, myGame);
        }

        if (myGame == null) {
            return TrySolution(solutions, officials, index + (courtOne ? 0 : 1), !courtOne, scorer);
        }

        var officialsByPreference = officials
            .Where(to => (scorer ? to.ScorerProficiency : to.UmpireProficiency) != UmpiringProficiencies.NotOfficial)
            .GroupBy(to => scorer ? to.ScorerProficiency : to.UmpireProficiency)
            .OrderByDescending(k => k.Key != UmpiringProficiencies.EmergencyOfficial)
            .ThenByDescending(k => k.Key)
            .Select(to => to.ToList())
            .ToList();

        if (!courtOne && !scorer) {
            officialsByPreference.Reverse();
        }

        foreach (var officialsList in officialsByPreference) {
            foreach (var official in officialsList.OrderBy(o => scorer ? o.GamesScored : o.GamesUmpired)
                         .ThenBy(o => scorer ? o.GamesUmpired : o.GamesScored)) {
                if (myGame.PlayerIds.Contains(official.PlayerId)) continue;
                if (otherGame?.PlayerIds.Contains(official.PlayerId) ?? false) continue;
                if (otherGame?.Official == official) continue;

                if (myGame.Official == official) continue;
                if (otherGame?.Scorer == official) continue;

                if (scorer) {
                    myGame.Scorer = official;
                    official.GamesScored++;
                } else {
                    myGame.Official = official;
                    official.GamesUmpired++;
                }

                var success = TrySolution(solutions,
                    officials, index + (courtOne ? 0 : 1), !courtOne, scorer, force);

                if (success) {
                    return true;
                }

                if (scorer) {
                    myGame.Scorer = null;
                    official.GamesScored--;
                } else {
                    myGame.Official = null;
                    official.GamesUmpired--;
                }
            }
        }

        if (force) {
            if (scorer) {
                var official = myGame.Official!;

                myGame.Scorer = official;
                official.GamesScored++;
            } else {
                var official = new OfficialContainer {
                    GamesScored = 0,
                    GamesUmpired = 0,
                    Name = "Unfillable",
                    OfficialId = -1,
                    PlayerId = -1,
                };

                myGame.Official = official;
            }

            return TrySolution(solutions,
                officials, index + (courtOne ? 0 : 1), !courtOne, scorer, force);
        }

        return false;
    }
}