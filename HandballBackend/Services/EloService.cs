using HandballBackend.Database.Models;
using HandballBackend.Events;

namespace HandballBackend.Utils;

/*
 * K = 40.0
 * initial_elo = 1500
 * D = 3000.0
 *
 *
 * numbers = {0: "One", 1: "Two", 2: "Three", 3: "Four"}
 *
 *
 * def probability(other, me):
 *     return 1.0 / (1.0 + math.pow(10, K * (other - me) / D))
 *
 *
 * def calc_elo(elo, elo_other, first_won):
 *     pa = probability(elo_other, elo)
 *     delta_ra = K * (first_won - pa)
 *     return delta_ra
 *
 */

public class EloService(HandballContext db) : IEventHandler<UpdateElosEvent> {
    private static double K = 40.0;
    private static double D = 3000.0;


    public static double InitialElo = 1500.0;

    private static double Probability(double opponentElo, double myElo) {
        return 1.0 / (1.0 + Math.Pow(10, K * (opponentElo - myElo) / D));
    }

    public static double CalculateEloDelta(double myElo, double opponentElo, bool win) {
        var pa = Probability(opponentElo, myElo);
        var delta = K * (win ? 1 - pa : -pa);
        return delta;
    }

    private static Dictionary<int, double> _cachedElos = new();

    public static Dictionary<int, double> GetPlayerElos() {
        return _cachedElos;
    }

    public void UpdatePlayerElos() {
        _cachedElos = db.PlayerGameStats
            .Join(
                db.PlayerGameStats
                    .GroupBy(s => s.PlayerId)
                    .Select(g => new {
                        PlayerId = g.Key,
                        GameId = g.Max(x => x.GameId)
                    }),
                pgs => new {pgs.PlayerId, pgs.GameId},
                latest => new {latest.PlayerId, latest.GameId},
                (pgs, latest) => new {
                    pgs.PlayerId,
                    Elo = (pgs.EloDelta ?? 0) + pgs.InitialElo
                }
            )
            .ToDictionary(x => x.PlayerId, x => x.Elo);
    }

    public Task Handle(UpdateElosEvent @event) {
        UpdatePlayerElos();
        return Task.CompletedTask;
    }
}