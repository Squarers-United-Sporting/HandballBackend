using HandballBackend.Controllers;
using HandballBackend.Database;
using HandballBackend.Database.Models;
using HandballBackend.Utils;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.EndpointHelpers.GameManagement;

public static class GameManager {
    private static readonly string[] SIDES = ["Left", "Right", "Substitute"];

    private static readonly string?[] VALID_SCORE_METHODS = [
        null,
        "Double Bounce",
        "Straight",
        "Out of Court",
        "Double Touch",
        "Grabs",
        "Illegal Body Part",
        "Obstruction"
    ];

    private static readonly GameEventType[] IGNORED_BY_UNDO = [
        GameEventType.Notes,
        GameEventType.EndTimeout,
        GameEventType.Votes,
        GameEventType.Protest,
        GameEventType.Resolve,
    ];


    private static void BroadcastEvent(int gameId, GameEvent e) {
        _ = Task.Run(() => ScoreboardController.SendGameUpdate(gameId, e));
    }

    private static void BroadcastUpdate(int gameId) {
        _ = Task.Run(() => ScoreboardController.SendGame(gameId));
    }

    internal static GameEvent SetUpGameEvent(Game game, GameEventType type, bool? firstTeam, int? playerId,
        string? notes = null, int? details = null) {
        int? teamId = firstTeam != null ? (firstTeam.Value ? game.TeamOneId : game.TeamTwoId) : null;
        var prevEvent = game.Events.OrderByDescending(gE => gE.Id).FirstOrDefault()!;
        var player = game.Players.FirstOrDefault(pgs => pgs.PlayerId == playerId);
        var newEvent = new GameEvent {
            TeamId = teamId,
            PlayerId = playerId != null ? player!.PlayerId : null,
            EventType = type,
            GameId = game.Id,
            TournamentId = game.TournamentId,
            Details = details,
            Notes = notes,
            TeamWhoServedId = prevEvent.TeamToServeId,
            PlayerWhoServedId = prevEvent.PlayerToServeId,
            SideServed = prevEvent.SideToServe,

            TeamToServeId = prevEvent.TeamToServeId,
            PlayerToServeId = prevEvent.PlayerToServeId,
            SideToServe = prevEvent.SideToServe,
            TeamOneLeftId = prevEvent.TeamOneLeftId,
            TeamOneRightId = prevEvent.TeamOneRightId,
            TeamTwoLeftId = prevEvent.TeamTwoLeftId,
            TeamTwoRightId = prevEvent.TeamTwoRightId,
        };
        return newEvent;
    }


    private static async Task<GameEvent> AddPointToGame(HandballContext db, int gameNumber, bool firstTeam,
        int? playerId,
        bool penalty = false, string? notes = null) {
        var game = await db.Games.IncludeRelevant().Include(g => g.Events)
            .SingleOrDefaultAsync(g => g.GameNumber == gameNumber);
        var teamId = firstTeam ? game.TeamOneId : game.TeamTwoId;
        var prevEvent = game.Events.OrderByDescending(gE => gE.Id).FirstOrDefault()!;
        var newEvent = SetUpGameEvent(game, GameEventType.Score, firstTeam, playerId, penalty ? "Penalty" : notes);
        newEvent.TeamToServeId = teamId;
        if (teamId == prevEvent.TeamToServeId) {
            //We won this point and the last point
            if (game.Tournament.BadmintonServes) {
                newEvent.SideToServe = prevEvent.SideToServe == "Left" ? "Right" : "Left";
                foreach (var pgs in game.Players.Where(pgs => pgs.TeamId == teamId)) {
                    pgs.SideOfCourt = pgs.SideOfCourt switch {
                        "Left" => "Right",
                        "Right" => "Left",
                        _ => pgs.SideOfCourt
                    };
                }

                if (firstTeam) {
                    newEvent.TeamOneLeftId = prevEvent.TeamOneRightId;
                    newEvent.TeamOneRightId = prevEvent.TeamOneLeftId;
                } else {
                    newEvent.TeamTwoLeftId = prevEvent.TeamTwoRightId;
                    newEvent.TeamTwoRightId = prevEvent.TeamTwoLeftId;
                }
            } else {
                // we are not in badminton; so nothing needs to change
            }
        } else {
            // this is our first win of the service, so we need to make changes accordingly
            var lastService = game.Events.Where(gE => gE.TeamToServeId == teamId).OrderByDescending(gE => gE.Id)
                .FirstOrDefault();
            // default side is the only difference between badminton and normal serves; the second team starts on the 
            // right if using badminton.
            var defaultSide = game.Tournament!.BadmintonServes ? "Left" : "Right";
            newEvent.SideToServe = (lastService?.SideToServe ?? defaultSide) == "Left" ? "Right" : "Left";
            var teamPlayers = game.Players!.Where(pgs => pgs.TeamId == teamId).ToList();
            if (teamPlayers.Count == 1) {
                newEvent.PlayerToServeId = teamPlayers[0].PlayerId;
            } else {
                newEvent.PlayerToServeId = teamPlayers
                    .First(pgs => pgs.SideOfCourt == newEvent.SideToServe).PlayerId;
            }
        }

        await db.AddAsync(newEvent);
        GameEventSynchroniser.SyncScorePoint(game, newEvent);
        await db.SaveChangesAsync();
        return newEvent;
    }

    public static async Task StartGame(int gameNumber, bool swapService, string[]? playersTeamOne,
        string[]? playersTeamTwo,
        bool teamOneIsIGa, string? officialSearchable = null, string? scorerSearchable = null) {
        var db = new HandballContext();
        var game = await db.Games.IncludeRelevant().Include(g => g.Events).Include(game => game.Players)
            .ThenInclude(pgs => pgs.Player)
            .FirstOrDefaultAsync(g => g.GameNumber == gameNumber);
        if (game == null) throw new ArgumentException("The game number provided doesn't exist");
        if (game.Started) throw new InvalidOperationException("The game has already begun");
        List<PlayerGameStats> teamOneIds = [];
        List<PlayerGameStats> teamTwoIds = [];
        if (playersTeamOne == null) {
            teamOneIds = game.Players.Where(pgs => pgs.TeamId == game.TeamOneId).ToList();
        } else {
            foreach (var searchableName in playersTeamOne) {
                teamOneIds.Add(
                    game.Players.First(pgs => pgs.Player.SearchableName == searchableName));
            }
        }

        if (playersTeamTwo == null) {
            teamTwoIds = game.Players.Where(pgs => pgs.TeamId == game.TeamOneId).ToList();
        } else {
            foreach (var searchableName in playersTeamTwo) {
                teamTwoIds.Add(
                    game.Players.First(pgs => pgs.Player.SearchableName == searchableName));
            }
        }


        var igaId = teamOneIsIGa ? game.TeamOneId : game.TeamTwoId;
        game.Status = "In Progress";
        game.AdminStatus = "In Progress";
        game.NoteableStatus = "In Progress";
        game.IgaSideId = igaId;
        game.StartTime = Utilities.GetUnixSeconds();
        if (officialSearchable != null) {
            var official = await db.Officials.Where(o => o.Person.SearchableName == officialSearchable)
                .Select(o => o.Id)
                .FirstAsync();
            game.OfficialId = official;
        }

        if (scorerSearchable != null) {
            var scorer = await db.Officials.Where(o => o.Person.SearchableName == scorerSearchable).Select(o => o.Id)
                .FirstAsync();
            game.ScorerId = scorer;
        }

        foreach (var (pgs, side) in teamOneIds.Zip(SIDES)) {
            pgs.SideOfCourt = side;
            pgs.StartSide = side;
        }

        foreach (var (pgs, side) in teamTwoIds.Zip(SIDES)) {
            pgs.SideOfCourt = side;
            pgs.StartSide = side;
        }

        var servingTeamId = swapService ? game.TeamTwoId : game.TeamOneId;
        var servingPlayer = swapService ? teamTwoIds[0] : teamOneIds[0];
        game.TeamToServeId = servingTeamId;
        teamOneIds.Add(teamOneIds.Last());
        teamTwoIds.Add(teamTwoIds.Last());

        var startEvent = new GameEvent {
            GameId = game.Id,
            TournamentId = game.TournamentId,
            EventType = GameEventType.Start,
            SideToServe = "Left",
            TeamToServeId = servingTeamId,
            PlayerToServeId = servingPlayer.PlayerId,
            TeamOneLeftId = teamOneIds[0].PlayerId,
            TeamOneRightId = teamOneIds[1].PlayerId,
            TeamTwoLeftId = teamTwoIds[0].PlayerId,
            TeamTwoRightId = teamTwoIds[1].PlayerId,
        };
        db.GameEvents.Add(startEvent);
        GameEventSynchroniser.SyncStartGame(game, startEvent);
        await db.SaveChangesAsync();
        BroadcastUpdate(gameNumber);
    }

    public static async Task Merit(int gameNumber, bool firstTeam, bool leftPlayer, string? meritReason) {
        var db = new HandballContext();
        var game = await db.Games.IncludeRelevant().Include(g => g.Events).FirstAsync(g => g.GameNumber == gameNumber);
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");

        int? player;
        var gameEvent = game.Events.OrderBy(gE => gE.Id).FirstOrDefault()!;
        if (firstTeam) {
            player = leftPlayer ? gameEvent.TeamOneLeftId : gameEvent.TeamOneRightId;
        } else {
            player = leftPlayer ? gameEvent.TeamTwoLeftId : gameEvent.TeamTwoRightId;
        }

        var e = SetUpGameEvent(game, GameEventType.Merit, firstTeam, player, notes: meritReason);
        await db.AddAsync(e);
        GameEventSynchroniser.SyncMerit(game, e);
        await db.SaveChangesAsync();
        BroadcastEvent(gameNumber, e);
    }

    public static async Task Merit(int gameNumber, bool firstTeam, string playerSearchable, string? meritReason) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");

        var player = game.Players.First(pgs => pgs.Player.SearchableName == playerSearchable);

        var e = SetUpGameEvent(game, GameEventType.Merit, firstTeam, player.PlayerId, notes: meritReason);
        await db.AddAsync(e);
        GameEventSynchroniser.SyncMerit(game, e);
        await db.SaveChangesAsync();
        BroadcastEvent(gameNumber, e);
    }

    public static async Task ScorePoint(int gameNumber, bool firstTeam, bool leftPlayer, string? scoreMethod) {
        var db = new HandballContext();
        var game = await db.Games.IncludeRelevant().Include(g => g.Events).FirstAsync(g => g.GameNumber == gameNumber);
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");
        if (!VALID_SCORE_METHODS.Contains(scoreMethod)) {
            throw new ArgumentException("The score method provided is invalid");
        }

        int? player;
        var gameEvent = game.Events.OrderBy(gE => gE.Id).FirstOrDefault()!;
        if (firstTeam) {
            player = leftPlayer ? gameEvent.TeamOneLeftId : gameEvent.TeamOneRightId;
        } else {
            player = leftPlayer ? gameEvent.TeamTwoLeftId : gameEvent.TeamTwoRightId;
        }

        var e = await AddPointToGame(db, gameNumber, firstTeam, player, notes: scoreMethod);
        await db.SaveChangesAsync();
        BroadcastEvent(gameNumber, e);
    }

    public static async Task ScorePoint(int gameNumber, bool firstTeam, string playerSearchable, string? scoreMethod) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");
        if (!VALID_SCORE_METHODS.Contains(scoreMethod)) {
            throw new ArgumentException("The score method provided is invalid");
        }

        var player = game.Players.First(pgs => pgs.Player.SearchableName == playerSearchable);
        var e = await AddPointToGame(db, gameNumber, firstTeam, player.PlayerId, notes: scoreMethod);
        await db.SaveChangesAsync();
        BroadcastEvent(gameNumber, e);
    }

    public static async Task Ace(int gameNumber) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");
        var prevGameEvent = game.Events.OrderByDescending(gE => gE.Id).FirstOrDefault()!;
        var firstTeam = prevGameEvent.TeamToServeId == game.TeamOneId;
        var e = await AddPointToGame(db, gameNumber, firstTeam, prevGameEvent.PlayerToServeId, notes: "Ace");
        await db.SaveChangesAsync();
        BroadcastEvent(gameNumber, e);
    }

    public static async Task Fault(int gameNumber) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");
        var lastGameEvent = game.Events.OrderByDescending(gE => gE.Id).FirstOrDefault()!;
        var firstTeam = lastGameEvent.TeamToServeId == game.TeamOneId;
        var gameEvent = SetUpGameEvent(game, GameEventType.Fault, firstTeam, lastGameEvent.PlayerToServeId);
        var faulted = game.Events.Where(gE => gE.EventType is GameEventType.Fault or GameEventType.Score)
            .OrderByDescending(gE => gE.Id)
            .Select(gE => gE.EventType is GameEventType.Fault).FirstOrDefault(false);
        await db.AddAsync(gameEvent);
        if (faulted) {
            await AddPointToGame(db, gameNumber, !firstTeam, null, true);
        }

        GameEventSynchroniser.SyncFault(game, gameEvent);
        await db.SaveChangesAsync();
        BroadcastEvent(gameNumber, gameEvent);
    }

    public static async Task Timeout(int gameNumber, bool firstTeam) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");
        var gameEvent = SetUpGameEvent(game, GameEventType.Timeout, firstTeam, null);
        await db.AddAsync(gameEvent);
        GameEventSynchroniser.SyncTimeout(game, gameEvent);
        await db.SaveChangesAsync();
        BroadcastEvent(gameNumber, gameEvent);
    }

    public static async Task Forfeit(int gameNumber, bool firstTeam) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");
        var gameEvent = SetUpGameEvent(game, GameEventType.Forfeit, firstTeam, null);
        await db.AddAsync(gameEvent);
        GameEventSynchroniser.SyncForfeit(game, gameEvent);
        await db.SaveChangesAsync();
        BroadcastEvent(gameNumber, gameEvent);
    }

    public static async Task EndTimeout(int gameNumber) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");
        var gameEvent = SetUpGameEvent(game, GameEventType.EndTimeout, null, null);
        await db.AddAsync(gameEvent);
        await db.SaveChangesAsync();
        BroadcastEvent(gameNumber, gameEvent);
    }

    public static async Task Substitute(int gameNumber, bool firstTeam, string playerSearchable) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");
        var gameEvent = SetUpGameEvent(game, GameEventType.Substitute, null, null);
        var teamId = firstTeam ? game.TeamOneId : game.TeamTwoId;
        var playerComingIn = game.Players.First(pgs => pgs.TeamId == teamId && pgs.SideOfCourt == "Substitute");
        var playerGoingOut =
            game.Players.First(pgs => pgs.TeamId == teamId && pgs.Player.SearchableName == playerSearchable);
        var leftSide = playerGoingOut.SideOfCourt == "Left";
        playerComingIn.SideOfCourt = playerGoingOut.SideOfCourt;
        playerGoingOut.SideOfCourt = "Substitute";
        if (firstTeam) {
            if (leftSide) {
                gameEvent.TeamOneLeftId = playerComingIn.PlayerId;
            } else {
                gameEvent.TeamOneRightId = playerComingIn.PlayerId;
            }
        } else {
            if (leftSide) {
                gameEvent.TeamTwoLeftId = playerComingIn.PlayerId;
            } else {
                gameEvent.TeamTwoRightId = playerComingIn.PlayerId;
            }
        }

        await db.AddAsync(gameEvent);
        // GameEventSynchroniser.SyncSubstitute(game, gameEvent);  //Doesn't exist
        await db.SaveChangesAsync();
        BroadcastEvent(gameNumber, gameEvent);
    }

    public static async Task Substitute(int gameNumber, bool firstTeam, bool leftPlayer) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");
        var gameEvent = SetUpGameEvent(game, GameEventType.Substitute, null, null);
        var teamId = firstTeam ? game.TeamOneId : game.TeamTwoId;
        var playerComingIn = game.Players.First(pgs => pgs.TeamId == teamId && pgs.SideOfCourt == "Substitute");
        var prevEvent = game.Events.OrderBy(gE => gE.Id).FirstOrDefault()!;
        int playerGoingOutId;
        if (firstTeam) {
            playerGoingOutId = (leftPlayer ? prevEvent.TeamOneLeftId : prevEvent.TeamOneRightId)!.Value;
        } else {
            playerGoingOutId = (leftPlayer ? prevEvent.TeamTwoLeftId : prevEvent.TeamTwoRightId)!.Value;
        }

        var playerGoingOut = game.Players.First(p => p.PlayerId == playerGoingOutId);
        var leftSide = playerGoingOut.SideOfCourt == "Left";
        playerComingIn.SideOfCourt = playerGoingOut.SideOfCourt;
        playerGoingOut.SideOfCourt = "Substitute";
        if (firstTeam) {
            if (leftSide) {
                gameEvent.TeamOneLeftId = playerComingIn.PlayerId;
            } else {
                gameEvent.TeamOneRightId = playerComingIn.PlayerId;
            }
        } else {
            if (leftSide) {
                gameEvent.TeamTwoLeftId = playerComingIn.PlayerId;
            } else {
                gameEvent.TeamTwoRightId = playerComingIn.PlayerId;
            }
        }

        await db.AddAsync(gameEvent);
        // GameEventSynchroniser.SyncSubstitute(game, gameEvent);  //Doesn't exist
        await db.SaveChangesAsync();
        BroadcastEvent(gameNumber, gameEvent);
    }

    public static async Task Card(int gameNumber, bool firstTeam, string playerSearchable, string color, int duration,
        string reason) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();

        var player = game.Players.First(pgs => pgs.Player.SearchableName == playerSearchable).PlayerId;
        await CardInternal(db, gameNumber, firstTeam, player, color, duration, reason);
        await db.SaveChangesAsync();
        //broadcast happens in internal
    }

    public static async Task Card(int gameNumber, bool firstTeam, bool leftPlayer, string color, int duration,
        string reason) {
        var db = new HandballContext();
        var game = await db.Games.IncludeRelevant().Include(g => g.Events)
            .FirstOrDefaultAsync(g => g.GameNumber == gameNumber);
        int player;
        var prevEvent = game.Events.OrderBy(gE => gE.Id).FirstOrDefault()!;
        if (firstTeam) {
            player = (leftPlayer ? prevEvent.TeamOneLeftId : prevEvent.TeamOneRightId)!.Value;
        } else {
            player = (leftPlayer ? prevEvent.TeamTwoLeftId : prevEvent.TeamTwoRightId)!.Value;
        }


        await CardInternal(db, gameNumber, firstTeam, player, color, duration, reason);

        await db.SaveChangesAsync();
        //broadcast happens in internal
    }

    private static async Task CardInternal(HandballContext db, int gameNumber, bool firstTeam, int playerId,
        string color,
        int duration,
        string reason) {
        var game = await db.Games.IncludeRelevant().Include(g => g.Events)
            .FirstOrDefaultAsync(g => g.GameNumber == gameNumber);
        if (game == null) throw new ArgumentException("The game has not been found");
        if (color != "Warning" && !color.EndsWith(" Card")) {
            color += " Card";
        }

        var type = color switch {
            "Warning" => GameEventType.Warning,
            "Green Card" => GameEventType.GreenCard,
            "Yellow Card" => GameEventType.YellowCard,
            "Red Card" => GameEventType.RedCard,
            _ => throw new ArgumentOutOfRangeException(nameof(color), color, null)
        };


        var gameEvent = SetUpGameEvent(game, type, firstTeam, playerId, reason, duration);
        await db.AddAsync(gameEvent);
        GameEventSynchroniser.SyncCard(game, gameEvent);


        var teamId = firstTeam ? game.TeamOneId : game.TeamTwoId;
        var players = game.Players.Where(pgs => pgs.SideOfCourt != "Substitute" && pgs.TeamId == teamId).ToList();

        var bothCarded = players
            .Select(i => i.CardTimeRemaining >= 0 ? i.CardTimeRemaining : game.ScoreToForceWin)
            .DefaultIfEmpty(0)
            .Min();

        if (!game.SomeoneHasWon && (bothCarded != 0 || players.Count == 1)) {
            var myScore = game.TeamOneScore;
            var theirScore = game.TeamTwoScore;

            if (!firstTeam) {
                (myScore, theirScore) = (theirScore, myScore);
            }

            bothCarded = Math.Min(bothCarded,
                Math.Min(Math.Max(myScore + 2, game.ScoreToWin), game.ScoreToForceWin) - theirScore);

            for (var i = 0; i < (players.Count == 1 ? duration : bothCarded); i++) {
                await AddPointToGame(
                    db,
                    gameNumber,
                    !firstTeam,
                    null,
                    penalty: true
                );
                foreach (var pgs in players.Where(pgs => pgs.CardTimeRemaining > 0)) {
                    pgs.CardTimeRemaining--;
                }
            }
        }

        BroadcastEvent(gameNumber, gameEvent);
    }

    public static async Task Undo(int gameNumber) {
        var db = new HandballContext();
        var game = await db.Games.IncludeRelevant().Include(g => g.Events).FirstAsync(g => g.GameNumber == gameNumber);
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");
        var smallestId = game.Events.Where(gE => !IGNORED_BY_UNDO.Contains(gE.EventType) && gE.Notes != "Penalty")
            .OrderByDescending(gE => gE.Id).First().Id;
        await db.GameEvents.Where(gE => gE.GameId == game.Id && gE.Id >= smallestId).ExecuteDeleteAsync();
        await db.SaveChangesAsync(); // Not necessary but probably still a good idea


        db = new HandballContext();
        GameEventSynchroniser.SyncGame(db, gameNumber);
        await db.SaveChangesAsync();
        BroadcastUpdate(gameNumber);
    }

    public static async Task Delete(int gameNumber) {
        var db = new HandballContext();
        var game = await db.Games.Include(game => game.Tournament).FirstOrDefaultAsync(g => g.GameNumber == gameNumber);
        if (!game.Tournament.Editable) {
            throw new InvalidOperationException("The game is not in an editable tournament");
        }

        await db.GameEvents.Where(gE => gE.GameId == game.Id).ExecuteDeleteAsync();
        await db.PlayerGameStats.Where(pgs => pgs.GameId == game.Id).ExecuteDeleteAsync();
        db.Remove(game);
        await db.SaveChangesAsync();
        BroadcastUpdate(gameNumber);
    }

    public static async Task End(
        int gameNumber,
        List<string> bestPlayerOrder,
        List<string> nefariousPlayers,
        int teamOneRating, int teamTwoRating,
        string notes,
        string? protestReasonTeamOne, string? protestReasonTeamTwo,
        string notesTeamOne, string notesTeamTwo,
        bool markedForReview
    ) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();
        if (!game.SomeoneHasWon) throw new InvalidOperationException("The game has not ended!");
        var playersInOrder = game.Players.OrderBy(p => bestPlayerOrder.IndexOf(p.Player.SearchableName)).ToList();

        var endEvent = SetUpGameEvent(game, GameEventType.EndGame, null, null, notes);
        await db.AddAsync(endEvent);


        if (!string.IsNullOrEmpty(protestReasonTeamOne)) {
            var protestEvent = SetUpGameEvent(game, GameEventType.Protest, true, null, protestReasonTeamOne);
            await db.AddAsync(protestEvent);
        }

        if (!string.IsNullOrEmpty(protestReasonTeamTwo)) {
            var protestEvent = SetUpGameEvent(game, GameEventType.Protest, false, null, protestReasonTeamTwo);
            db.Add(protestEvent);
        }

        var notesTeamOneEvent = SetUpGameEvent(game, GameEventType.Notes, true, null, notesTeamOne, teamOneRating);
        await db.AddAsync(notesTeamOneEvent);
        var notesTeamTwoEvent = SetUpGameEvent(game, GameEventType.Notes, false, null, notesTeamTwo, teamTwoRating);
        await db.AddAsync(notesTeamTwoEvent);
        var votes = 2;
        var task = new List<Task>();
        foreach (var pgs in playersInOrder) {
            pgs.IsEvil = nefariousPlayers.Contains(pgs.Player.SearchableName);
            pgs.Rating = pgs.TeamId == game.TeamOneId ? teamOneRating : teamTwoRating;
            if (votes <= 0) continue;
            var votesEvent = SetUpGameEvent(game, GameEventType.Votes, true, pgs.PlayerId, details: votes--);
            task.Add(db.AddAsync(votesEvent).AsTask());
            GameEventSynchroniser.SyncVotes(game, votesEvent);
        }

        await Task.WhenAll(task);

        var isRandomAbandonment = Math.Max(game.TeamOneScore, game.TeamTwoScore) < 5 &&
                                  game.Events.Any(gE => gE.EventType == GameEventType.Abandon);
        var isForfeit = game.Events.Any(gE => gE.EventType == GameEventType.Forfeit);

        game.MarkedForReview = markedForReview;
        game.Length = Utilities.GetUnixSeconds() - game.StartTime;
        GameEventSynchroniser.SyncGameEnd(game, endEvent);
        if (!isRandomAbandonment && game is {
            Ranked:
                true,
            IsFinal:
                false
        }) {
            var playingPlayers = game.Players
                .Where(pgs => (isForfeit || pgs.RoundsCarded + pgs.RoundsOnCourt > 0)).ToList();
            var teamOneElo = playingPlayers.Where(pgs => pgs.TeamId == game.TeamOneId).Select(pgs => pgs.InitialElo)
                .Average();
            var teamTwoElo = playingPlayers.Where(pgs => pgs.TeamId == game.TeamTwoId).Select(pgs => pgs.InitialElo)
                .Average();
            foreach (var pgs in playingPlayers) {
                var myElo = pgs.TeamId == game.TeamOneId ? teamOneElo : teamTwoElo;
                var oppElo = pgs.TeamId == game.TeamOneId ? teamTwoElo : teamOneElo;
                pgs.EloDelta = EloCalculator.CalculateEloDelta(myElo, oppElo, game.WinningTeamId == pgs.TeamId);
            }
        }

        await db.SaveChangesAsync();
        if (game.Tournament.TextAlerts && markedForReview) {
            _ = Task.Run(() => TextHelper.TextTournamentStaff(game));
        }

        var remainingGames =
            await db.Games.AnyAsync(g =>
                g.TournamentId == game.TournamentId && !g.IsBye && !g.Ended && g.Id != game.Id);
        if (!remainingGames) {
            await game.Tournament.EndRound();
        }

        BroadcastUpdate(gameNumber);
        await PostgresBackup.MakeBackup();
    }

    public static async Task<Game> CreateGame(int tournamentId, string?[]? playersTeamOne, string?[]? playersTeamTwo,
        string? teamOneName, string? teamTwoName, bool blitzGame, int officialId = -1,
        int scorerId = -1, int round = -1, int court = 0, bool isFinal = false) {
        var db = new HandballContext();
        var allNames = (playersTeamOne ?? []).Concat(playersTeamTwo ?? []).Where(n => n != null).Cast<string>()
            .ToList();
        var teams = new List<Team>();
        var people = await
            db.People.Where(p => allNames.Contains(p.Name)).ToListAsync();
        foreach (var (players, teamName) in new[] { (playersTeamOne, teamOneName), (playersTeamTwo, teamTwoName) }) {
            Team team;
            if (players == null || players.Length == 0) {
                if (teamName == null) {
                    throw new ArgumentNullException(teams.Count == 0 ? nameof(playersTeamOne) : nameof(playersTeamTwo),
                        "You must specify either a team name or the players for the team");
                }

                team = (await db.Teams.IncludeRelevant().FirstOrDefaultAsync(t => t.Name == teamName))!;
            } else {
                var playerIds = players.Select(a => people.FirstOrDefault(p => p.Name == a)?.Id)
                    .ToList();
                while (playerIds.Count < 3) {
                    playerIds.Add(null);
                }

                var maybeTeam = await db.Teams.IncludeRelevant().FirstOrDefaultAsync(t =>
                    // Both players must be in one of the roles
                    (playerIds.Contains(t.CaptainId ?? null) &&
                     playerIds.Contains(t.NonCaptainId ?? null) &&
                     playerIds.Contains(t.SubstituteId ?? null)) &&

                    // Count of non-null player references should be exactly 2
                    ((t.CaptainId.HasValue ? 1 : 0) +
                        (t.NonCaptainId.HasValue ? 1 : 0) +
                        (t.SubstituteId.HasValue ? 1 : 0) == playerIds.Count(a => a.HasValue))
                );
                if (maybeTeam == null) {
                    team = new Team {
                        CaptainId = playerIds![0],
                        NonCaptainId = playerIds[1],
                        SubstituteId = null,
                        Name = teamName,
                        SearchableName = Utilities.ToSearchable(teamName)
                    };
                    _ = Task.Run(() => ImageHelper.SetGoogleImageForTeam(team.Id));
                    await db.Teams.AddAsync(team);
                } else {
                    team = maybeTeam;
                }
            }

            teams.Add(team);
        }

        await db.SaveChangesAsync();
        return await CreateGame(tournamentId, teams[0].Id, teams[1].Id, blitzGame, officialId, scorerId, round, court,
            isFinal);
    }


    public static async Task<Game> CreateGame(int tournamentId, int teamOneId, int teamTwoId, bool blitzGame = false,
        int officialId = -1,
        int scorerId = -1, int round = -1, int court = 0, bool isFinal = false) {
        var db = new HandballContext();
        var oneId = teamOneId;
        var twoId = teamTwoId;
        var teams = await db.Teams.Where(t => t.Id == oneId || t.Id == twoId).IncludeRelevant().ToListAsync();
        var teamOne = teams.First(t => t.Id == oneId);
        var teamTwo = teams.First(t => t.Id == twoId);
        var tournament = (await db.Tournaments.FindAsync(tournamentId))!;
        var ranked = tournament.Ranked;
        var isBye = false;
        var tasks = new List<Task>();
        foreach (var team in new[] { teamOne, teamTwo }) {
            if (team.Id == 1) {
                // this is the bye team
                isBye = true;
                continue;
            }

            ranked &= team.CaptainId != null;
            var tt = await db.TournamentTeams.FirstOrDefaultAsync(t =>
                t.TournamentId == tournamentId && t.TeamId == team.Id);
            if (tt != null) continue;
            tt = new TournamentTeam {
                TeamId = team.Id,
                TournamentId = tournamentId
            };
            tasks.Add(db.AddAsync(tt).AsTask());
        }

        await Task.WhenAll(tasks.ToArray());
        if (round < 0) {
            var lastGame = await db.Games.Where(g => g.StartTime != null && g.TournamentId == tournamentId)
                .OrderByDescending(g => g.GameNumber)
                .FirstOrDefaultAsync();
            int lastStartTime;
            if (lastGame == null) {
                lastStartTime = -1;
                round = 0;
            } else {
                lastStartTime = lastGame.StartTime!.Value;
                round = lastGame.Round;
            }

            if (Utilities.GetUnixSeconds() > lastStartTime + 32400) {
                round++;
            }
        }

        court = isBye ? -1 : court;
        if (isBye && teamOneId == 1) {
            teamOneId = teamTwoId;
            teamTwoId = 1;
        }

        var gameNumber =
            isBye
                ? -1
                : ((await db.Games.OrderByDescending(g => g.GameNumber).FirstOrDefaultAsync())?.GameNumber ?? 0) + 1;
        var game = new Game {
            GameNumber = gameNumber,
            TournamentId = tournamentId,
            TeamOneId = teamOneId,
            TeamTwoId = teamTwoId,
            IgaSideId = teamOneId,
            OfficialId = officialId > 0 ? officialId : null,
            ScorerId = scorerId > 0 ? scorerId : null,
            BlitzGame = blitzGame,
            Court = court,
            IsFinal = isFinal,
            Round = round,
            Ranked = ranked,
            IsBye = isBye,
            SomeoneHasWon = isBye
        };
        if (isBye) {
            game.Status = "Bye";
            game.AdminStatus = "Bye";
            game.WinningTeamId = 1;
        }

        await db.AddAsync(game);
        await db.SaveChangesAsync();
        game = await db.Games.Where(g => g.Id == game.Id)
            .IncludeRelevant()
            .SingleAsync(); //used to pull extra gamey data
        var playerIds = new[] {
            teamOne.CaptainId, teamOne.NonCaptainId, teamOne.SubstituteId, teamTwo.CaptainId, teamTwo.NonCaptainId,
            teamTwo.SubstituteId
        };
        var prevGames = await db.PlayerGameStats
            .Where(pgs => playerIds.Contains(pgs.PlayerId))
            .GroupBy(pgs => pgs.PlayerId)
            .Select(g => g.OrderByDescending(x => x.GameId).FirstOrDefault())
            .ToDictionaryAsync(pgs => pgs!.PlayerId);

        tasks.Clear();
        foreach (var team in new[] { teamOne, teamTwo }) {
            if (team.Id == 1) continue;
            Person?[] teamPlayers = [team.Captain, team.NonCaptain, team.Substitute];
            foreach (var p in teamPlayers.Where(p => p != null).Cast<Person>()) {
                prevGames.TryGetValue(p.Id, out var prevGame);
                var carryCardTimes = game.TournamentId >= 7 && prevGame?.TournamentId == game.TournamentId;
                tasks.Add(db.AddAsync(new PlayerGameStats {
                    GameId = game.Id,
                    PlayerId = p.Id,
                    TournamentId = tournamentId,
                    TeamId = team.Id,
                    OpponentId = team.Id == teamOneId ? teamTwoId : teamOneId,
                    InitialElo = (prevGame?.InitialElo ?? 1500.0) + (prevGame?.EloDelta ?? 0),
                    CardTime = carryCardTimes ? Math.Max(prevGame?.CardTime ?? 0, 0) : 0,
                    CardTimeRemaining = carryCardTimes ? Math.Max(prevGame?.CardTimeRemaining ?? 0, 0) : 0
                }).AsTask());
            }
        }

        await Task.WhenAll(tasks);
        await db.SaveChangesAsync();
        return game;
    }

    public static async Task Resolve(int gameNumber) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();
        if (!game.Ended) throw new InvalidOperationException("The game has not ended");
        if (Game.ResolvedStatuses.Contains(game.AdminStatus))
            throw new InvalidOperationException("The game is resolved");
        var gameEvent = SetUpGameEvent(game, GameEventType.Resolve, null, null);
        game.AdminStatus = "Resolved";
        game.Resolved = true;
        await db.AddAsync(gameEvent);
        await db.SaveChangesAsync();
        BroadcastUpdate(gameNumber);
    }

    public static async Task Abandon(int gameNumber) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");
        var gameEvent = SetUpGameEvent(game, GameEventType.Abandon, null, null);
        await db.AddAsync(gameEvent);
        GameEventSynchroniser.SyncAbandon(game, gameEvent);
        await db.SaveChangesAsync();
        BroadcastEvent(gameNumber, gameEvent);
    }

    public static async Task Replay(int gameNumber) {
        var db = new HandballContext();
        var game = await db.Games.Where(g => g.GameNumber == gameNumber).IncludeRelevant().Include(g => g.Events)
            .FirstAsync();
        if (!game.Started) throw new InvalidOperationException("The game has not started");
        if (game.Ended) throw new InvalidOperationException("The game has ended");
        var gameEvent = SetUpGameEvent(game, GameEventType.Replay, null, null);
        await db.AddAsync(gameEvent);
        await db.SaveChangesAsync();
        BroadcastEvent(gameNumber, gameEvent);
    }
}