// ReSharper disable InconsistentNaming
// Disabled as these are sent to the frontend; we don't care too much about the cs naming conventions

using System.Drawing;
using HandballBackend.Database.Models;
using HandballBackend.Utils;

namespace HandballBackend.Database.SendableTypes;

public class TournamentTeamData : TeamData {
    public TournamentTeamData(
        TournamentTeam tt,
        bool generateStats = false,
        bool generatePlayerStats = false,
        bool formatData = false
    )
        : base(tt.Team, tt.Tournament, generateStats, generatePlayerStats, formatData) {
        ImageUrl = tt.ImageUrl == null ? ImageUrl : Utilities.FixHandballUrl(tt.ImageUrl);
        BigImageUrl = tt.BigImageUrl == null ? BigImageUrl : Utilities.FixHandballUrl(tt.BigImageUrl);
        Name = tt.Name ?? Name;
        TeamColor = tt.TeamColor ?? TeamColor;
        ExtendedName = tt.LongName ?? tt.Name ?? ExtendedName;
    }
}