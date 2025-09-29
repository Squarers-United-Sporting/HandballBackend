using HandballBackend.Database.Models;
using HandballBackend.Utils;

namespace HandballBackend.Database.SendableTypes;

public class PersonData {
    public string Name { get; protected set; }
    public string SearchableName { get; protected set; }
    public string ImageUrl { get; protected set; }
    public string BigImageUrl { get; protected set; }

    public static readonly string[] PercentageColumns = [
        "Percentage of Points Scored",
        "Percentage",
        "Percentage of Points Scored For Team",
        "Percentage of Points Served Won",
        "Serve Return Rate",
        "Serve Fault Rate",
        "Serve Ace Rate",
        "Percentage of Rounds Carded",
        "Percentage of Games Started Left"
    ];

    public Dictionary<string, dynamic?>? Stats { get; protected set; }


    public PersonData(Person person, Tournament? tournament = null, bool generateStats = false, Team? team = null,
        bool format = false, bool admin = false) {
        Name = person.Name;
        SearchableName = person.SearchableName;
        ImageUrl = ImageUrl = Utilities.FixImageUrl(person.ImageUrl);
        BigImageUrl = Utilities.FixImageUrl(person.BigImageUrl);

        if (!generateStats) return;

        Stats = new Dictionary<string, dynamic?> {
            ["B&F Votes"] = 0.0,
            ["Elo"] = 0.0,
            ["Games Won"] = 0.0,
            ["Games Lost"] = 0.0,
            ["Games Played"] = 0.0,
            ["Games Started Left"] = 0.0,
            ["Games Started Right"] = 0.0,
            ["Games Started Substitute"] = 0.0,
            ["Caps"] = 0.0,
            ["Percentage"] = 0.0,
            ["Penalty Points"] = 0.0,
            ["Points Scored"] = 0.0,
            ["Points Served"] = 0.0,
            ["Aces Scored"] = 0.0,
            ["Faults"] = 0.0,
            ["Double Faults"] = 0.0,
            ["Green Cards"] = 0.0,
            ["Yellow Cards"] = 0.0,
            ["Red Cards"] = 0.0,
            ["Rounds on Court"] = 0.0,
            ["Rounds Carded"] = 0.0,
            ["Points per Game"] = 0.0,
            ["Points per Loss"] = 0.0,
            ["Aces per Game"] = 0.0,
            ["Faults per Game"] = 0.0,
            ["Cards"] = 0.0,
            ["Cards per Game"] = 0.0,
            ["Points per Card"] = 0.0,
            ["Serves per Game"] = 0.0,
            ["Serves per Ace"] = 0.0,
            ["Serves per Fault"] = 0.0,
            ["Serve Ace Rate"] = 0.0,
            ["Serve Fault Rate"] = 0.0,
            ["Percentage of Points Scored"] = 0.0,
            ["Percentage of Points Scored For Team"] = 0.0,
            ["Percentage of Points Served Won"] = 0.0,
            ["Serves Received"] = 0.0,
            ["Serves Returned"] = 0.0,
            ["Max Serve Streak"] = 0.0,
            ["Max Ace Streak"] = 0.0,
            ["Serve Return Rate"] = 0.0,
            ["Votes per 100 Games"] = 0.0,
            ["Average Rating"] = 0.0,
            ["Merits"] = 0.0,
            ["Nefarious Votes"] = 0.0,
            ["Nefarious Rate"] = 0.0
        };
        var teamPoints = 0;
        var servedPointsWon = 0;
        var ratedGames = 0;
        var tournamentGames = 0;
        var playedTournaments = new HashSet<int>();
        foreach (var pgs in (person.PlayerGameStats ?? []).OrderBy(pgs => pgs.GameId)) {
            playedTournaments.Add(pgs.TournamentId);
            if (tournament != null && pgs.TournamentId != tournament.Id) continue;
            if (team != null && pgs.TeamId != team.Id) continue;
            if (pgs.RoundsCarded + pgs.RoundsOnCourt == 0) continue;
            if (pgs.Game.IsBye) continue;
            var game = pgs.Game;
            Stats["Caps"] += game.Ended && game.TournamentId != 1 ? 1 : 0;
            if (!pgs.Game.Ranked && tournament?.Ranked != false) continue;


            if (pgs.Game.IsFinal) continue;
            servedPointsWon += pgs.ServedPointsWon;
            teamPoints += game.TeamOneId == pgs.TeamId ? game.TeamOneScore : game.TeamTwoScore;
            if (tournament != null || pgs.TournamentId != 1) {
                tournamentGames++;
                Stats["B&F Votes"] += pgs.BestPlayerVotes;
            }


            Stats["Games Won"] += game.Ended && game.WinningTeamId == pgs.TeamId ? 1 : 0;
            Stats["Games Lost"] += game.Ended && game.WinningTeamId != pgs.TeamId ? 1 : 0;
            Stats["Games Played"] += game.Ended ? 1 : 0;
            Stats["Points Scored"] += pgs.PointsScored;
            Stats["Points Served"] += pgs.ServedPoints;
            Stats["Aces Scored"] += pgs.AcesScored;
            Stats["Faults"] += pgs.Faults;
            Stats["Double Faults"] += pgs.DoubleFaults;
            Stats["Green Cards"] += pgs.GreenCards;
            Stats["Yellow Cards"] += pgs.YellowCards;
            Stats["Red Cards"] += pgs.RedCards;
            Stats["Penalty Points"] += 2 * pgs.GreenCards + 6 * pgs.YellowCards + 12 * pgs.RedCards;
            if (pgs.Rating is not null) {
                Stats["Average Rating"] += pgs.Rating;
                ratedGames++;
            }

            Stats["Merits"] += pgs.Merits;
            Stats["Rounds on Court"] += pgs.RoundsOnCourt;
            Stats["Rounds Carded"] += pgs.RoundsCarded;
            Stats["Cards"] += pgs.GreenCards + pgs.YellowCards + pgs.RedCards;
            Stats["Serves Received"] += pgs.ServesReceived;
            Stats["Serves Returned"] += pgs.ServesReturned;
            Stats["Max Ace Streak"] = Math.Max(Stats["Max Ace Streak"], pgs.AceStreak);
            Stats["Max Serve Streak"] = Math.Max(Stats["Max Serve Streak"], pgs.ServeStreak);
            switch (pgs.StartSide) {
                case "Left":
                    Stats["Games Started Left"] += 1;
                    break;
                case "Right":
                    Stats["Games Started Right"] += 1;
                    break;
                default:
                    Stats["Games Started Substitute"] += 1;
                    break;
            }
            Stats["Nefarious Votes"] += pgs.IsEvil ? 1 : 0;
        }

        var tournaments = new HashSet<int>();
        tournaments.UnionWith(playedTournaments);
        if (tournament == null && person.Official is not null) {
            var umpiredTournaments = person.Official!.TournamentOfficials.Where(to => to.Tournament.Started)
                .Select(to => to.TournamentId).ToHashSet();
            tournaments.UnionWith(umpiredTournaments);
            tournaments.Remove(1);
        }

        Stats["Tournaments"] = tournaments.Count;
        Stats["Elo"] = person.Elo(tournamentId: tournament?.Id);
        var gamesPlayed = Stats["Games Played"];
        Stats["Percentage"] = Stats["Games Won"] / Stats["Games Played"];
        Stats["Points per Game"] = Stats["Points Scored"] / gamesPlayed;
        Stats["Points per Loss"] = Stats["Points Scored"] / Stats["Games Lost"];
        Stats["Aces per Game"] = Stats["Aces Scored"] / gamesPlayed;
        Stats["Faults per Game"] = Stats["Faults"] / gamesPlayed;
        Stats["Cards per Game"] = Stats["Cards"] / gamesPlayed;
        Stats["Points per Card"] = Stats["Points Scored"] / Stats["Cards"];
        Stats["Serves per Game"] = Stats["Points Served"] / gamesPlayed;
        Stats["Serves per Ace"] = Stats["Points Served"] / Stats["Aces Scored"];
        Stats["Serves per Fault"] = Stats["Points Served"] / Stats["Faults"];
        Stats["Serve Ace Rate"] = Stats["Aces Scored"] / Stats["Points Served"];
        Stats["Serve Fault Rate"] = Stats["Faults"] / Stats["Points Served"];
        Stats["Average Rating"] /= ratedGames;
        Stats["Percentage of Points Scored"] =
            Stats["Points Scored"] / Math.Max(Stats["Rounds on Court"], 1);
        Stats["Percentage of Points Scored For Team"] = Stats["Points Scored"] / Math.Max(teamPoints, 1);
        Stats["Percentage of Games Started Left"] = Stats["Games Started Left"] / gamesPlayed;
        Stats["Percentage of Points Served Won"] =
            servedPointsWon / Math.Max(Stats["Points Served"], 1);
        Stats["Serve Return Rate"] = Stats["Serves Returned"] / Math.Max(Stats["Serves Received"], 1);
        if (tournament == null) {
            Stats["Votes per 100 Games"] = 100.0f * Stats["B&F Votes"] / tournamentGames;
            Stats["Votes per Tournament"] = Stats["B&F Votes"] / playedTournaments.Count;
            Stats["Merits per Tournament"] = Stats["Merits"] / playedTournaments.Count;
        } else {
            Stats["Votes per 100 Games"] = 100.0f * Stats["B&F Votes"] / gamesPlayed;
        }

        Stats["Percentage of Rounds Carded"] =
            Stats["Rounds Carded"] / (Stats["Rounds on Court"] + Stats["Rounds Carded"]);
        Stats["Rounds per Game"] = Stats["Rounds on Court"] / gamesPlayed;
        Stats["Nefarious Rate"] = Stats["Nefarious Votes"] / gamesPlayed;
        if (admin) {
            Stats["Penalty Points per Game"] = Stats["Penalty Points"] / gamesPlayed;
        } else {
            Stats.Remove("Penalty Points");
            Stats.Remove("Average Rating");
        }

        if (tournament != null) {
            Stats.Remove("Tournaments");
        }

        if (!format) return;

        FormatData();
    }

    protected void FormatData() {
        foreach (var stat in Stats.Keys) {
            if (Stats[stat] == null) {
                Stats[stat] = "-";
                continue;
            }

            if (double.IsNaN(Stats[stat])) Stats[stat] = "-";
            else if (PercentageColumns.Contains(stat)) {
                if (double.IsInfinity(Stats[stat])) Stats[stat] = "\u221e%";
                else Stats[stat] = Stats[stat].ToString("P2");
            } else {
                if (double.IsInfinity(Stats[stat])) Stats[stat] = "\u221e";
                else Stats[stat] = Math.Round((double) Stats[stat], 2);
            }
        }
    }
}