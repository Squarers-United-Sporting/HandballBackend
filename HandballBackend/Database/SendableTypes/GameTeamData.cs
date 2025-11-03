using HandballBackend.Database.Models;
using HandballBackend.Utils;

namespace HandballBackend.Database.SendableTypes;

public class GameTeamData : TeamData {
    public bool ServingFromLeft { get; private set; }

    public new GamePlayerData? Captain { get; set; }
    public new GamePlayerData? NonCaptain { get; set; }
    public new GamePlayerData? Substitute { get; set; }

    public GameTeamData(Team team,
        Game game,
        bool generateStats = false,
        bool formatData = false,
        bool isUmpire = false,
        bool isAdmin = false)
        : base(team) {
        var tt = team.TournamentTeams.FirstOrDefault(tt => tt.TournamentId == game.TournamentId);
        ImageUrl = tt?.ImageUrl == null ? ImageUrl : Utilities.FixHandballUrl(tt.ImageUrl);
        BigImageUrl = tt?.BigImageUrl == null ? BigImageUrl : Utilities.FixHandballUrl(tt.BigImageUrl);
        Name = tt?.Name ?? Name;
        ExtendedName = tt?.LongName ?? tt?.Name ?? ExtendedName;
        var startGame = game.Events.FirstOrDefault(a => a.EventType == GameEventType.Start);
        var lastTimeServed = game
            .Events.OrderByDescending(a => a.Id)
            .FirstOrDefault(a => a.EventType == GameEventType.Score && a.TeamToServeId == team.Id);
        if (startGame is null) {
            ServingFromLeft = true;
        } else if (game.Tournament.BadmintonServes) {
            if (lastTimeServed is not null) {
                ServingFromLeft = lastTimeServed.SideToServe == "Left";
            } else {
                ServingFromLeft = true;
            }
        } else {
            if (lastTimeServed is not null) {
                ServingFromLeft = (lastTimeServed.SideToServe == "Left");
            } else {
                ServingFromLeft = startGame.TeamToServeId == team.Id;
            }
        }

        Captain = game
            .Players.FirstOrDefault(pgs => pgs.PlayerId == team.CaptainId)
            ?.ToSendableData(generateStats, formatData, isUmpire, isAdmin);
        NonCaptain = game
            .Players.FirstOrDefault(pgs => pgs.PlayerId == team.NonCaptainId)
            ?.ToSendableData(generateStats, formatData, isUmpire, isAdmin);
        Substitute = game
            .Players.FirstOrDefault(pgs => pgs.PlayerId == team.SubstituteId)
            ?.ToSendableData(generateStats, formatData, isUmpire, isAdmin);

        if (!generateStats)
            return;

        Stats = new Dictionary<string, dynamic> {
            ["Timeouts Called"] =
                game.TeamOneId == team.Id ? game.TeamOneTimeouts : game.TeamTwoTimeouts,
            ["Points Against"] = game.TeamOneId == team.Id ? game.TeamTwoScore : game.TeamOneScore,
            ["Green Cards"] = 0.0,
            ["Yellow Cards"] = 0.0,
            ["Red Cards"] = 0.0,
            ["Faults"] = 0.0,
            ["Double Faults"] = 0.0,
            ["Points Scored"] = 0.0,
        };

        foreach (var pgs in game.Players.Where(pgs => pgs.TeamId == team.Id)) {
            Stats["Green Cards"] += pgs.GreenCards;
            Stats["Yellow Cards"] += pgs.YellowCards;
            Stats["Red Cards"] += pgs.RedCards;
            Stats["Faults"] += pgs.Faults;
            Stats["Double Faults"] += pgs.DoubleFaults;
            Stats["Points Scored"] += pgs.PointsScored;
        }

        if (!formatData)
            return;
        FormatData();
    }
}