using HandballBackend.Database;
using HandballBackend.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.EndpointHelpers.GameManagement;

internal static class GameEventSynchroniser {
    public static void SyncGame(HandballContext db, int gameNumber) {
        var game = db.Games.IncludeRelevant().Include(g => g.Events).SingleOrDefault(g => g.GameNumber == gameNumber);
        if (game is null) throw new InvalidOperationException($"Game {gameNumber} not found");
        game.Reset();
        var carryOverCards = game.TournamentId >= 8;
        foreach (var pgs in game.Players) {
            pgs.ResetStats();
            if (!carryOverCards) continue;
            var prevGamePlayer = db.PlayerGameStats
                .FirstOrDefault(prev => prev.TournamentId == game.TournamentId && pgs.PlayerId == prev.PlayerId);
            var cardTime = prevGamePlayer?.RedCards == 0 ? Math.Max(prevGamePlayer?.CardTimeRemaining ?? 0, 0) : -1;
            pgs.CardTimeRemaining = cardTime;
            pgs.CardTime = cardTime;
        }

        foreach (var gameEvent in game.Events.OrderBy(g => g.Id)) {
            switch (gameEvent.EventType) {
                case GameEventType.Start:
                    SyncStartGame(game, gameEvent);
                    break;
                case GameEventType.Score:
                    SyncScorePoint(game, gameEvent);
                    break;
                case GameEventType.Fault:
                    SyncFault(game, gameEvent);
                    break;
                case GameEventType.Timeout:
                    SyncTimeout(game, gameEvent);
                    break;
                case GameEventType.Forfeit:
                    SyncForfeit(game, gameEvent);
                    break;
                case GameEventType.Warning:
                case GameEventType.GreenCard:
                case GameEventType.YellowCard:
                case GameEventType.RedCard:
                    SyncCard(game, gameEvent);
                    break;
                case GameEventType.Protest:
                    game.Protested = true;
                    break;
                case GameEventType.Resolve:
                    SyncResolve(game, gameEvent);
                    break;
                case GameEventType.EndGame:
                    SyncGameEnd(game, gameEvent);
                    break;
                case GameEventType.Votes:
                    SyncVotes(game, gameEvent);
                    break;
                case GameEventType.Abandon:
                    SyncAbandon(game, gameEvent);
                    break;
                case GameEventType.Merit:
                    SyncMerit(game, gameEvent);
                    break;
                case GameEventType.Demerit:
                    SyncDemerit(game, gameEvent);
                    break;
                case GameEventType.Notes:
                case GameEventType.Substitute:
                case GameEventType.EndTimeout:
                case GameEventType.Replay:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        var lastEvent = game.Events.OrderByDescending(gE => gE.Id).FirstOrDefault();
        foreach (var pgs in game.Players) {
            if (lastEvent == null) {
                pgs.SideOfCourt = pgs.StartSide;
            } else if (
                (pgs.TeamId == game.TeamOneId && lastEvent.TeamOneLeftId == lastEvent.TeamOneRightId) ||
                (pgs.TeamId == game.TeamTwoId && lastEvent.TeamTwoLeftId == lastEvent.TeamTwoRightId)
            ) {
                pgs.SideOfCourt = lastEvent.SideToServe;
            } else if (pgs.PlayerId == lastEvent.TeamTwoLeftId || pgs.PlayerId == lastEvent.TeamOneLeftId) {
                pgs.SideOfCourt = "Left";
            } else if (pgs.PlayerId == lastEvent.TeamTwoRightId || pgs.PlayerId == lastEvent.TeamOneRightId) {
                pgs.SideOfCourt = "Right";
            } else {
                pgs.SideOfCourt = "Substitute";
            }
        }

        if (lastEvent != null) {
            game.TeamToServeId = lastEvent.TeamToServeId;
            game.SideToServe = lastEvent.SideToServe;
            game.PlayerToServeId = lastEvent.PlayerToServeId;
        }
    }

    public static void SyncResolve(Game game, GameEvent gameEvent) {
        game.Resolved = true;
        game.AdminStatus = "Resolved";
    }

    public static void SyncGameEnd(Game game, GameEvent gameEvent) {
        var isForfeit = game.Events.Any(gE => gE.EventType == GameEventType.Forfeit);
        var badBehaviour = game.Events.Where(ge => ge.EventType == GameEventType.Notes).Any(gE => gE.Details == 1);
        var protested = game.Events.Any(gE => gE.EventType == GameEventType.Protest);
        game.Protested = protested;

        if (game.Players.Any(i => i.RedCards != 0)) {
            game.AdminStatus = "Red Card Awarded";
        } else if (protested) {
            game.AdminStatus = "Protested";
        } else if (game.MarkedForReview) {
            game.AdminStatus = "Marked for Review";
        } else if (game.Players.Any(i => i.YellowCards != 0)) {
            game.AdminStatus = "Yellow Card Awarded";
        } else if (badBehaviour) {
            game.AdminStatus = "Unsportsmanlike Conduct";
        } else if (isForfeit) {
            game.AdminStatus = "Forfeit";
        } else {
            game.AdminStatus = "Official";
        }


        if (Math.Max(game.TeamOneScore, game.TeamTwoScore) < 5) {
            game.WinningTeamId = Random.Shared.NextSingle() >= 0.5f ? game.TeamOneId : game.TeamTwoId;
        } else if (game.TeamOneScore == game.TeamTwoScore) {
            var mostRecentPoint = game.Events.Where(ge => ge.EventType == GameEventType.Score)
                .OrderByDescending(gE => gE.Id).First();
            game.WinningTeamId = game.TeamOneId == mostRecentPoint.TeamId ? game.TeamTwoId : game.TeamOneId;
        } else {
            game.WinningTeamId = game.TeamOneScore > game.TeamTwoScore ? game.TeamOneId : game.TeamTwoId;
        }

        game.NoteableStatus = game.AdminStatus;
        game.Status = "Official";
        game.Ended = true;
        game.Notes = gameEvent.Notes;
    }

    public static void SyncMerit(Game game, GameEvent gameEvent) {
        var player = game.Players.FirstOrDefault(p => p.PlayerId == gameEvent.PlayerId)!;
        player.Merits++;
    }

    public static void SyncDemerit(Game game, GameEvent gameEvent) {
        var player = game.Players.FirstOrDefault(p => p.PlayerId == gameEvent.PlayerId)!;
        player.Demerits++;
    }

    public static void SyncCard(Game game, GameEvent gameEvent) {
        var player = game.Players.FirstOrDefault(p => p.PlayerId == gameEvent.PlayerId)!;
        switch (gameEvent.EventType) {
            case GameEventType.Warning:
                player.Warnings += 1;
                break;
            case GameEventType.GreenCard:
                player.GreenCards += 1;
                break;
            case GameEventType.YellowCard:
                player.YellowCards += 1;
                break;
            case GameEventType.RedCard:
                player.RedCards += 1;
                break;
            default:
                throw new ArgumentException("Only card events can be synced as cards");
        }

        if (player.CardTimeRemaining < 0) return;
        //player isn't already red carded

        if (gameEvent.Details > 0) {
            //the card is not a red or warning
            player.CardTimeRemaining += gameEvent.Details ?? 0;
        } else if (gameEvent.Details < 0) {
            //red card
            player.CardTimeRemaining = -1;
        }

        player.CardTime = player.CardTimeRemaining;
    }


    public static void SyncFault(Game game, GameEvent gameEvent) {
        var player = game.Players.FirstOrDefault(pgs => pgs.PlayerId == gameEvent.PlayerId);
        var faulted = game.Events.OrderByDescending(gE => gE.EventType is GameEventType.Fault or GameEventType.Score)
            .Select(gE => gE.EventType is GameEventType.Fault).FirstOrDefault(false);
        player.Faults += 1;
        player.ServedPoints += 1;
        if (faulted) {
            player.DoubleFaults += 1;
        }
    }

    public static void SyncScorePoint(Game game, GameEvent gameEvent) {
        var player = game.Players.FirstOrDefault(pgs => pgs.PlayerId == gameEvent.PlayerId);
        var playersOnCourt = game.Players.Where(p =>
            p.PlayerId == gameEvent.TeamOneLeftId ||
            p.PlayerId == gameEvent.TeamOneRightId ||
            p.PlayerId == gameEvent.TeamTwoLeftId ||
            p.PlayerId == gameEvent.TeamTwoRightId).ToArray();
        var serveStreak = game.Events.OrderByDescending(ge => ge.Id)
            .TakeWhile(ge => ge.PlayerToServeId == player?.PlayerId).Count();
        var aceStreak = game.Events.OrderByDescending(ge => ge.Id)
            .TakeWhile(gE => gE.PlayerToServeId == player?.PlayerId && gE.Notes == "Ace").Count();

        var nonServingTeam = playersOnCourt.Where(pgs => gameEvent.TeamWhoServedId != pgs.TeamId).OrderBy(pgs =>
                pgs.PlayerId != gameEvent.TeamOneLeftId && pgs.PlayerId != gameEvent.TeamTwoLeftId)
            .Cast<PlayerGameStats?>()
            .ToList(); //force the team into LTR order
        nonServingTeam.Add(null);
        var leftServed = gameEvent.SideServed == "Left";
        var isFirstTeam = gameEvent.TeamId == game.TeamOneId;
        if (isFirstTeam) {
            game.TeamOneScore += 1;
        } else {
            game.TeamTwoScore += 1;
        }

        game.TeamToServeId = gameEvent.TeamToServeId;
        game.PlayerToServeId = gameEvent.PlayerToServeId;
        game.SideToServe = gameEvent.SideToServe;


        foreach (var pgs in playersOnCourt) {
            if (pgs.CardTimeRemaining == 0) {
                pgs.RoundsOnCourt += 1;
            } else {
                pgs.RoundsCarded += 1;
                if (pgs.CardTime > 0) {
                    pgs.CardTimeRemaining -= 1;
                }
            }
        }

        var highScore = Math.Max(game.TeamOneScore, game.TeamTwoScore);
        if (highScore >= game.ScoreToWin &&
            (Math.Abs(game.TeamOneScore - game.TeamTwoScore) >= 2 || highScore >= game.ScoreToForceWin)) {
            game.SomeoneHasWon = true;
        }

        if (player is null) {
            return;
        }

        player.PointsScored += 1;
        if (gameEvent.TeamWhoServedId is not null) {
            var playerWhoServed = //doing this like this means that it won't give served points to carded players
                playersOnCourt.Where(pgs => pgs.TeamId == gameEvent.TeamWhoServedId)
                    .OrderByDescending(pgs => pgs.CardTimeRemaining == 0)
                    .ThenByDescending(pgs => pgs.PlayerId == gameEvent.PlayerWhoServedId).First();
            playerWhoServed.ServedPoints += 1;
            if (playerWhoServed.TeamId == gameEvent.TeamId) {
                playerWhoServed.ServedPointsWon += 1;
            }
        }


        if (gameEvent.PlayerWhoServedId == gameEvent.PlayerToServeId) {
            serveStreak += 1;
        } else {
            serveStreak = 1;
        }

        if (gameEvent.Notes == "Ace") {
            player.AcesScored += 1;
            if (gameEvent.PlayerWhoServedId == gameEvent.PlayerToServeId) {
                aceStreak += 1;
            } else {
                aceStreak = 1;
            }
        } else {
            aceStreak = 0;
        }

        player.ServeStreak = Math.Max(player.ServeStreak, serveStreak);
        player.AceStreak = Math.Max(player.AceStreak, aceStreak);
        var receivingPlayer = nonServingTeam[leftServed ? 0 : 1] ??
                              nonServingTeam.FirstOrDefault(a => a != null);
        if (receivingPlayer?.CardTimeRemaining != 0) {
            receivingPlayer = nonServingTeam.FirstOrDefault(pgs => (pgs?.CardTimeRemaining ?? 1) == 0);
        }

        if (receivingPlayer != null && gameEvent.PlayerId != null) {
            receivingPlayer.ServesReceived += 1;
            if (gameEvent.Notes != "Ace") {
                receivingPlayer.ServesReturned += 1;
            }
        }
    }


    public static void SyncStartGame(Game game, GameEvent gameEvent) {
        game.Started = true;
        foreach (var pgs in game.Players) {
            if (pgs.PlayerId == gameEvent.TeamOneLeftId || pgs.PlayerId == gameEvent.TeamTwoLeftId) {
                pgs.StartSide = "Left";
            } else if (pgs.PlayerId == gameEvent.TeamOneRightId ||
                       pgs.PlayerId == gameEvent.TeamTwoRightId) {
                pgs.StartSide = "Right";
            } else {
                pgs.StartSide = "Substitute";
            }
        }
    }

    public static void SyncTimeout(Game game, GameEvent gameEvent) {
        if (gameEvent.TeamId == game.TeamOneId) {
            game.TeamOneTimeouts += 1;
        } else {
            game.TeamTwoTimeouts += 1;
        }
    }

    public static void SyncForfeit(Game game, GameEvent gameEvent) {
        if (gameEvent.TeamId == game.TeamOneId) {
            game.TeamTwoScore = Math.Min(Math.Max(game.TeamOneScore + 2, game.ScoreToWin), game.ScoreToForceWin);
        } else {
            game.TeamOneScore = Math.Min(Math.Max(game.TeamTwoScore + 2, game.ScoreToWin), game.ScoreToForceWin);
        }

        game.SomeoneHasWon = true;
    }

    public static void SyncVotes(Game game, GameEvent gameEvent) {
        var player = game.Players.FirstOrDefault(p => p.PlayerId == gameEvent.PlayerId);
        if (player is null) return;
        player.BestPlayerVotes = gameEvent.Details!.Value;
    }

    public static void SyncAbandon(Game game, GameEvent gameEvent) {
        game.SomeoneHasWon = true;
    }
}