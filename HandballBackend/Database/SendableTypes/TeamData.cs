using System.Drawing;
using System.Text.Json.Serialization;
using HandballBackend.Database.Models;
using HandballBackend.Utils;

namespace HandballBackend.Database.SendableTypes;

public class TeamData {
    [JsonIgnore]
    public int Id { get; protected set; }

    public string Name { get; protected set; }
    public string ExtendedName { get; protected set; }
    public string SearchableName { get; protected set; }
    public string? ImageUrl { get; protected set; }
    public string? BigImageUrl { get; protected set; }
    public PersonData? Captain { get; protected set; }
    public PersonData? NonCaptain { get; protected set; }
    public PersonData? Substitute { get; protected set; }
    public string? TeamColor { get; protected set; }
    public int[]? TeamColorAsRGBABecauseDigbyIsLazy { get; protected set; }
    public double Elo { get; protected set; }

    public Dictionary<string, dynamic>? Stats { get; protected set; }

    private static int[] GenerateRgba(string backgroundColor) {
        var color = ColorTranslator.FromHtml(backgroundColor);
        int r = Convert.ToInt16(color.R);
        int g = Convert.ToInt16(color.G);
        int b = Convert.ToInt16(color.B);
        return [r, g, b, 255];
    }

    private static readonly string[] PercentageColumns = ["Percentage"];

    public TeamData(
        Team team,
        Tournament? tournament = null,
        bool generateStats = false,
        bool generatePlayerStats = false,
        bool formatData = false
    ) {
        Id = team.Id;
        Name = team.Name;
        ExtendedName = team.LongName ?? team.Name;
        SearchableName = team.SearchableName;
        ImageUrl = Utilities.FixHandballUrl(team.ImageUrl);
        BigImageUrl = Utilities.FixHandballUrl(team.BigImageUrl);
        Captain = team.Captain?.ToSendableData(
            tournament,
            generateStats && generatePlayerStats,
            team,
            formatData
        );
        NonCaptain = team.NonCaptain?.ToSendableData(
            tournament,
            generateStats && generatePlayerStats,
            team,
            formatData
        );
        Substitute = team.Substitute?.ToSendableData(
            tournament,
            generateStats && generatePlayerStats,
            team,
            formatData
        );
        TeamColor = team.TeamColor;
        TeamColorAsRGBABecauseDigbyIsLazy = TeamColor != null ? GenerateRgba(TeamColor) : null;

        if (!generateStats)
            return;

        Elo = team.DisplayElo(tournament);

        Stats = new Dictionary<string, dynamic>
        {
            { "Games Played", 0.0 },
            { "Games Won", 0.0 },
            { "Games Lost", 0.0 },
            { "Timeouts Called", 0.0 },
            { "Points Scored", 0.0 },
            { "Points Against", 0.0 },
            { "Green Cards", 0.0 },
            { "Yellow Cards", 0.0 },
            { "Red Cards", 0.0 },
            { "Faults", 0.0 },
            { "Double Faults", 0.0 },
        };
        var gameId = 0;
        foreach (
            var pgs in team
                .PlayerGameStats.Where(pgs =>
                    (tournament == null || pgs.TournamentId == tournament.Id)
                )
                .OrderBy(pgs => pgs.GameId)
        ) {
            if (tournament?.Ranked != false) {
                if (!pgs.Game.Ranked && NonCaptain != null)
                    continue;
            }

            if (pgs.Game.IsFinal)
                continue;
            if (gameId < pgs.GameId) {
                gameId = pgs.GameId;
                Stats["Games Played"] += pgs.Game.Ended ? 1 : 0;
                Stats["Games Won"] += pgs.Game.Ended && pgs.Game.WinningTeamId == team.Id ? 1 : 0;
                Stats["Games Lost"] += pgs.Game.Ended && pgs.Game.WinningTeamId != team.Id ? 1 : 0;
                Stats["Timeouts Called"] +=
                    pgs.Game.TeamOneId == team.Id
                        ? pgs.Game.TeamOneTimeouts
                        : pgs.Game.TeamTwoTimeouts;
                Stats["Points Against"] =
                    pgs.Game.TeamOneId == team.Id ? pgs.Game.TeamTwoScore : pgs.Game.TeamOneScore;
            }

            Stats["Green Cards"] += pgs.GreenCards;
            Stats["Yellow Cards"] += pgs.YellowCards;
            Stats["Red Cards"] += pgs.RedCards;
            Stats["Faults"] += pgs.Faults;
            Stats["Double Faults"] += pgs.DoubleFaults;
            Stats["Points Scored"] += pgs.PointsScored;
        }

        Stats["Point Difference"] = Stats["Points Scored"] - Stats["Points Against"];
        Stats["Percentage"] = Stats["Games Won"] / Stats["Games Played"];
        Stats["Timeouts per Game"] = Stats["Timeouts Called"] / Stats["Games Played"];
        Stats["Elo"] = Elo;

        if (!formatData)
            return;

        FormatData();
    }

    public void FormatData() {
        foreach (var stat in Stats.Keys) {
            if (Stats[stat] == null) {
                Stats[stat] = "-";
                continue;
            }

            if (double.IsNaN(Stats[stat]))
                Stats[stat] = "-";
            else if (PercentageColumns.Contains(stat)) {
                if (double.IsInfinity(Stats[stat]))
                    Stats[stat] = "\u221e%";
                else
                    Stats[stat] = Stats[stat].ToString("P2");
            } else {
                if (double.IsInfinity(Stats[stat]))
                    Stats[stat] = "\u221e";
                else
                    Stats[stat] = Math.Round((double) Stats[stat], 2);
            }
        }
    }
}