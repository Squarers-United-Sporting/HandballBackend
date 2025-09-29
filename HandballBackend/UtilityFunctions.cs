using HandballBackend.Database;
using HandballBackend.Database.Models;
using HandballBackend.EndpointHelpers;
using HandballBackend.EndpointHelpers.GameManagement;
using HandballBackend.FixtureGenerator;
using HandballBackend.Utils;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend;

internal static class UtilityFunctions {
    public static void init() {
        Config.SECRETS_FOLDER =
            @"G:\Programming\c#\HandballBackend\build\secrets";
        Config.RESOURCES_FOLDER =
            @"G:\Programming\c#\HandballBackend\build\resources";
    }


    public static void EvilTest() {
        init();
        var db = new HandballContext();
        var gE = db.GameEvents.OrderByDescending(gE => gE.Id).First();
    }

    public static void RegenerateElos() {
        init();
        var teamElos = new Dictionary<int, double>();
        var db = new HandballContext();
        var games = db.Games.Where(g => !g.IsFinal && g.Ranked && !g.IsBye)
            .OrderBy(g => g.TournamentId == 2)
            .ThenBy(g => g.TournamentId == 3)
            .ThenBy(g => g.StartTime)
            .ThenBy(g => g.Id)
            .IncludeRelevant().Include(g =>
                g.Events.Where(gE => gE.EventType == GameEventType.Forfeit || gE.EventType == GameEventType.Abandon));
        foreach (var game in games) {
            if (!game.Ended) continue;
            var isRandomAbandonment = Math.Max(game.TeamOneScore, game.TeamTwoScore) < 5 &&
                                      game.Events.Any(gE => gE.EventType == GameEventType.Abandon);
            if (isRandomAbandonment) continue;
            var playingPlayers = game.Players
                .Where(pgs =>
                    game.Events.Any(gE => gE.EventType == GameEventType.Forfeit) ||
                    pgs.RoundsCarded + pgs.RoundsOnCourt > 0).ToList();
            var teamOneElo = teamElos.GetValueOrDefault(game.TeamOne.Id, playingPlayers
                .Where(pgs => pgs.TeamId == game.TeamOneId).Select(pgs => pgs.InitialElo)
                .Average());
            var teamTwoElo = teamElos.GetValueOrDefault(game.TeamTwo.Id, playingPlayers
                .Where(pgs => pgs.TeamId == game.TeamTwoId).Select(pgs => pgs.InitialElo)
                .Average());
            foreach (var pgs in playingPlayers) {
                var myElo = pgs.TeamId == game.TeamOneId ? teamOneElo : teamTwoElo;
                var oppElo = pgs.TeamId == game.TeamOneId ? teamTwoElo : teamOneElo;
                var eloDelta = EloCalculator.CalculateEloDelta(myElo, oppElo, game.WinningTeamId == pgs.TeamId);
                pgs.EloDelta = eloDelta;
                teamElos[pgs.TeamId] = myElo + eloDelta;
                if (pgs.TeamId == 7) Console.WriteLine(teamElos[7]);
            }
        }

        db.SaveChanges();
    }


    public static void EncryptString() {
        string? x;
        do {
            Console.WriteLine("Enter the target string");
            x = Console.ReadLine();
            if (x != null && x != "x") {
                Console.WriteLine(EncryptionHelper.Encrypt(x));
            }
        } while (x != "x");
    }


    public static void ForceForfeitTournament() {
        init();
        Console.WriteLine("Enter the Lowest game Number");
        var db = new HandballContext();
        var i = int.Parse(Console.ReadLine() ?? string.Empty);
        var game = db.Games.Include(game => game.Players).ThenInclude(pgs => pgs.Player)
            .FirstOrDefault(g => g.GameNumber == i);
        var c = true;
        while (game != null && c) {
            Console.WriteLine($"Game {i}");
            if (game.IsBye) {
                i++;
                game = db.Games.Include(game => game.Players).ThenInclude(pgs => pgs.Player)
                    .FirstOrDefault(g => g.GameNumber == i);
                continue;
            }

            GameManager.StartGame(i, false, null, null, true);
            GameManager.Forfeit(i, false);
            GameManager.End(
                i,
                game.Players.Select(p => p.Player.SearchableName).ToList(),
                [], 3, 3,
                "Testing",
                null,
                null,
                "",
                "", false
            );
            i++;
            game = db.Games.Include(game => game.Players).ThenInclude(playerGameStats => playerGameStats.Player)
                .FirstOrDefault(g => g.GameNumber == i);
            c = Console.ReadLine().ToLower() != "x";
        }
    }

    public static void ResetTournament() {
        init();
        const int tournamentId = 11;
        Console.WriteLine($"Please Type 'CONFRIM' to confirm you want to reset the {tournamentId - 1}th tournament:");
        if (Console.ReadLine() != "CONFIRM") return;


        var db = new HandballContext();
        db.RemoveRange(db.GameEvents.Where(gE => gE.TournamentId == tournamentId));
        db.RemoveRange(db.PlayerGameStats.Where(gE => gE.TournamentId == tournamentId));
        db.RemoveRange(db.Games.Where(gE => gE.TournamentId == tournamentId));
        db.Tournaments.Single(t => t.Id == tournamentId).Started = false;
        db.Tournaments.Single(t => t.Id == tournamentId).InFinals = false;
        db.Tournaments.Single(t => t.Id == tournamentId).Finished = false;
        var tournamentTeams = db.TournamentTeams.Where(tt => tt.TournamentId == tournamentId).ToList();
        foreach (var tt in tournamentTeams) {
            tt.Pool = 0;
        }

        db.SaveChanges();
    }

    public static void SendGroupText() {
        init();
        Console.WriteLine("Please Type 'CONFRIM' to confirm you want to send a group text:");
        if (Console.ReadLine() != "CONFIRM") return;
        var db = new HandballContext();
        var people = db.TournamentTeams.Where(tt => tt.TournamentId == 11).IncludeRelevant().Select(t => t.Team)
            .ToArray()
            .SelectMany(t => t.People).ToList();
        var tasks = new List<Task>();
        foreach (var p in people) {
            Console.WriteLine($"Texting {p.Name}");
            tasks.Add(TextHelper.Text(p,
                $"Hi {p.Name.Split(" ")[0]}!\n  Just a reminder that the 10th SUSS Championship is on at 5pm today at Manning Library (2 Conochie Cres). Don't forget to bring a jumper as it is set to get quite cold!\n\nThanks, and as always, Happy Balling!")
            );
        }

        Task.WaitAll(tasks.ToArray());
    }


    public static void ListPhoneNumbers() {
        init();
        var db = new HandballContext();
        var people = db.People;
        foreach (var p in people) {
            Console.WriteLine($"{p.PhoneNumber} - {p.Name}");
        }
    }


    public static void FixImages() {
        init();
        var db = new HandballContext();
        var list = new List<Team>();
        var teams = db.Teams.Where(t => t.NonCaptainId != null && (t.ImageUrl == null || !t.ImageUrl.StartsWith("/")));
        foreach (var team in teams) {
            list.Add(team);
            Console.WriteLine($"Team {team.Name}");
        }

        var tasks = list.Select(t => ImageHelper.SetGoogleImageForTeam(t.Id)).ToList();
        Task.WaitAll(tasks.ToArray());
    }

    public static void ResynchroniseEveryGame() {
        init();
        var db = new HandballContext();
        var prevGame = -1;
        foreach (var g in db.Games.IncludeRelevant().ToArray()) {
            if (g.GameNumber < 0) continue;
            if (g.GameNumber >= prevGame) {
                Console.WriteLine($"Game {g.GameNumber}");
                prevGame = g.GameNumber - g.GameNumber % 50 + 50;
            }

            try {
                GameEventSynchroniser.SyncGame(db, g.GameNumber);
            } catch (Exception e) {
                Console.WriteLine($"Game {g.GameNumber}: {e.Message}");
                throw;
            }
        }

        db.SaveChanges();
    }


    public static void VotesFixerer() {
        init();
        var db = new HandballContext();
        var gamesToFix =
            db.Games.Where(g => g.Ended && !g.IsBye && g.Events.All(gE => gE.EventType != GameEventType.Votes));
        var prevGame = -1;
        foreach (var game in gamesToFix.Include(game => game.Events).Include(game => game.Players)) {
            if (game.GameNumber >= prevGame) {
                Console.WriteLine($"Game {game.GameNumber}");
                prevGame = game.GameNumber - game.GameNumber % 50 + 50;
            }

            var endEvent = game.Events.First(e => e.EventType == GameEventType.EndGame);
            var bestPlayerId = endEvent.Details;
            if (bestPlayerId is null) continue;
            if (!game.Ranked || (endEvent.TeamOneLeftId == endEvent.TeamOneRightId ||
                                 endEvent.TeamTwoLeftId == endEvent.TeamTwoRightId)) {
                endEvent.Details = null;
                continue;
            }

            if (game.Players.All(pgs => pgs.PlayerId != bestPlayerId)) {
                Console.WriteLine($"WTF?!: game {game.GameNumber} has players " +
                                  string.Join(", ", game.Players.Select(p => p.PlayerId.ToString())) +
                                  $" but best player {bestPlayerId}");
            }

            var pgs = game.Players.First(pgs => pgs.PlayerId == bestPlayerId);
            var newEvent = GameManager.SetUpGameEvent(game, GameEventType.Votes, pgs.TeamId == game.TeamOneId,
                pgs.PlayerId, details: 2);
            newEvent.CreatedAt = endEvent.CreatedAt;
            db.Add(newEvent);
        }

        db.SaveChanges();
    }

    public static void PrintAllHandballQuotes() {
        init();
        var db = new HandballContext();
        var today = DateTime.Today.DayOfYear;
        var quotes = db.QuotesOfTheDay
            .ToArray();
        var year = DateTime.Now.Year; //Or any year you want

        for (var i = 0; i < 365; i++) {
            var theDate = new DateTime(year, 1, 1).AddDays(i);
            var b = theDate.ToString("d.M.yyyy");
            Console.WriteLine($"{b}: {quotes[i % quotes.Length].Quote} - {quotes[i % quotes.Length].Author}");
        }
    }

    public static void TestUmpireAssignments() {
        init();
        const int tournamentId = 11;
        const int round = 1;
        List<Game> courtOneGames;
        List<Game?> courtTwoGames;
        List<AbstractFixtureGenerator.OfficialContainer> officials;
        var db = new HandballContext();
        var games = db.Games.Where(g => g.TournamentId == tournamentId && g.Round == round)
            .IncludeRelevant().ToList();
        courtOneGames = games.Where(g => g.Court == 0).ToList();
        courtTwoGames = games.Where(g => g.Court == 1).Cast<Game?>().ToList();
        officials =
            db.TournamentOfficials
                .Where(to => to.TournamentId == tournamentId)
                .Include(to => to.Official.Person)
                .Include(to => to.Official.Games.Where(g => g.TournamentId == tournamentId && g.Round < round))
                .Include(to =>
                    to.Official.ScoredGames.Where(g => g.TournamentId == tournamentId && g.Round < round))
                .ToList()
                .Select(to => new AbstractFixtureGenerator.OfficialContainer {
                    PlayerId = to.Official.PersonId,
                    OfficialId = to.OfficialId,
                    GamesUmpired = to.Official.Games.Count(g => g is { TournamentId: tournamentId, Round: < round }),
                    Name = to.Official.Person.Name,
                    GamesScored = to.Official.ScoredGames.Count(g => g is { TournamentId: tournamentId, Round: < round }),
                    UmpireProficiency = to.UmpireProficiency,
                    ScorerProficiency = to.ScorerProficiency,
                }).OrderBy(o => o.GamesUmpired).ToList();


        while (courtOneGames.Count > courtTwoGames.Count) {
            courtTwoGames.Add(null);
        }

        var solution =
            new List<(AbstractFixtureGenerator.UmpiringSolution, AbstractFixtureGenerator.UmpiringSolution?)>();
        foreach (var (courtOne, courtTwo) in courtOneGames.Zip(courtTwoGames)) {
            var courtOneGame = new AbstractFixtureGenerator.UmpiringSolution {
                GameId = courtOne.Id,
                CourtId = 0,
                PlayerIds = courtOne.TeamOne.People.Concat(courtOne.TeamTwo.People).Select(p => p.Id).ToList(),
            };
            var courtTwoGame = courtTwo == null
                ? null
                : new AbstractFixtureGenerator.UmpiringSolution {
                    GameId = courtTwo.Id,
                    CourtId = 1,
                    PlayerIds = courtTwo.TeamOne.People.Concat(courtTwo.TeamTwo.People).Select(p => p.Id).ToList(),
                };
            solution.Add(new(courtOneGame, courtTwoGame));
        }

        var solutionArray = solution.ToArray();
        Console.WriteLine("All Umpires:");
        foreach (var officialList in officials.GroupBy(o => o.UmpireProficiency)) {
            Console.WriteLine($"Proficiency: {officialList.Key}");
            Console.WriteLine(
                $"{string.Join("\n", officialList.Select(o => $"\t{o.Name} ({o.PlayerId}) : {o.GamesUmpired}, {o.GamesScored}"))}");
        }

        Console.WriteLine("--------------------");
        Console.WriteLine($"Success: {AbstractFixtureGenerator.TrySolution(solutionArray, officials, force: true)}");
        Console.WriteLine("--------------------");
        foreach (var game in solutionArray.SelectMany(g => new[] { g.Item1, g.Item2 })) {
            if (game == null) continue;
            Console.WriteLine($"Game {game.GameId} on Court {game.CourtId + 1}");
            Console.WriteLine($"\tPlayers: {string.Join(", ", game.PlayerIds)}");
            Console.WriteLine($"\tUmpire: {game.Official?.Name} ({game.Official?.PlayerId})");
            Console.WriteLine($"\tScorer: {game.Scorer?.Name} ({game.Scorer?.PlayerId})");
        }

        Console.WriteLine("--------------------");
        Console.WriteLine("All Umpires:");
        Console.WriteLine("--------------------");
        foreach (var officialList in officials.GroupBy(o => o.UmpireProficiency)) {
            Console.WriteLine($"Proficiency: {officialList.Key}");
            Console.WriteLine(
                $"{string.Join("\n", officialList.Select(o => $"\t{o.Name} ({o.PlayerId}) : {o.GamesUmpired}, {o.GamesScored}"))}");
        }
    }
}