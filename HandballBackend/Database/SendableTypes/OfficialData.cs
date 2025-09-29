using HandballBackend.Database.Models;

namespace HandballBackend.Database.SendableTypes;

public class OfficialData : PersonData {
    public OfficialRole Role { get; set; }
    public int UmpireProficiency { get; set; }
    public int ScorerProficiency { get; set; }

    public OfficialData(Official official, Tournament? tournament = null, bool includeStats = false,
        bool isAdmin = false) : base(
        official.Person) {
        var to = tournament != null
            ? official.TournamentOfficials.FirstOrDefault(to => tournament.Id == to.TournamentId)
            : null;

        Role = to?.Role ?? OfficialRole.Umpire;

        if (isAdmin) {
            UmpireProficiency = to?.UmpireProficiency ?? official.Proficiency;
            ScorerProficiency = to?.ScorerProficiency ?? official.Proficiency;
        }

        if (!includeStats) return;

        var playerGameStats =
            official.Games
                .Where(g => tournament == null || g.TournamentId == tournament.Id)
                .Where(g => !g.IsBye && (g.Ranked || (!tournament?.Ranked ?? false)))
                .OrderBy(g => g.Id).SelectMany(g => g.Players);
        var prevGameId = 0;
        Stats = new Dictionary<string, dynamic?> {
            {"Games Umpired", 0},
            {"Caps", 0},
            {"Rounds Umpired", 0},
            {"Green Cards Given", 0},
            {"Yellow Cards Given", 0},
            {"Red Cards Given", 0},
            {"Cards Given", 0},
            {"Faults Called", 0},
            {"Double Faults Called", 0},
        };
        foreach (var pgs in playerGameStats) {
            if (pgs.GameId > prevGameId) {
                prevGameId = pgs.GameId;
                Stats["Games Umpired"] += 1;
                Stats["Rounds Umpired"] += pgs.Game.TeamOneScore + pgs.Game.TeamTwoScore;
                Stats["Caps"] += pgs.Game.Ended && pgs.Game.TournamentId != 1 ? 1 : 0;
            }

            Stats["Green Cards Given"] += pgs.GreenCards;
            Stats["Yellow Cards Given"] += pgs.YellowCards;
            Stats["Red Cards Given"] += pgs.RedCards;
            Stats["Cards Given"] += pgs.GreenCards + pgs.YellowCards + pgs.RedCards;
            Stats["Faults Called"] += pgs.Faults;
            Stats["Double Faults Called"] += pgs.DoubleFaults;
        }
    }
}