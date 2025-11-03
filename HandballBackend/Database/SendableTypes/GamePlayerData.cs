using HandballBackend.Database.Models;

namespace HandballBackend.Database.SendableTypes;

public class GamePlayerData : PersonData {
    public int BestPlayerVotes { get; set; }
    public int CardTime { get; set; }
    public int CardTimeRemaining { get; set; }
    public string? SideOfCourt { get; set; }
    public bool IsCaptain { get; set; }
    public string? StartSide { get; set; }
    public List<GameEventData> PrevCards { get; set; }

    public GamePlayerData(
        Person player,
        Game game,
        bool includeStats = false,
        bool formatData = false
    )
        : this(game.Players.First(p => p.PlayerId == player.Id), includeStats, formatData) { }

    public GamePlayerData(PlayerGameStats pgs,
        bool includeStats = false,
        bool formatData = false,
        bool isUmpire = false,
        bool isAdmin = false)
        : base(pgs.Player) {
        BestPlayerVotes = pgs.BestPlayerVotes;
        CardTime = pgs.CardTime;
        CardTimeRemaining = pgs.CardTimeRemaining;
        SideOfCourt = pgs.SideOfCourt;
        IsCaptain = pgs.PlayerId == pgs.Team.CaptainId;
        StartSide = pgs.StartSide;
        PrevCards = isUmpire
            ? pgs.Player.Events?.Where(gE =>
                    gE.TournamentId == pgs.TournamentId && gE.IsCard && gE.GameId <= pgs.GameId
                )
                .Select(gE => gE.ToSendableData())
                .ToList() ?? []
            : [];
        if (!includeStats)
            return;
        Stats = new Dictionary<string, dynamic?> {
            ["Elo"] = pgs.InitialElo,
            ["Elo Delta"] = pgs.EloDelta,
            ["B&F Votes"] = pgs.BestPlayerVotes,
            ["Games Won"] = pgs.Game.Ended && pgs.Game.WinningTeamId == pgs.TeamId ? 1 : 0,
            ["Games Lost"] = pgs.Game.Ended && pgs.Game.WinningTeamId != pgs.TeamId ? 1 : 0,
            ["Games Played"] = pgs.Game.Ended ? 1 : 0,
            ["Points Scored"] = pgs.PointsScored,
            ["Points Served"] = pgs.ServedPoints,
            ["Aces Scored"] = pgs.AcesScored,
            ["Faults"] = pgs.Faults,
            ["Double Faults"] = pgs.DoubleFaults,
            ["Green Cards"] = pgs.GreenCards,
            ["Yellow Cards"] = pgs.YellowCards,
            ["Red Cards"] = pgs.RedCards,
            ["Rounds on Court"] = pgs.RoundsOnCourt,
            ["Rounds Carded"] = pgs.RoundsCarded,
            ["Cards"] = pgs.GreenCards + pgs.YellowCards + pgs.RedCards,
            ["Serves Received"] = pgs.ServesReceived,
            ["Serves Returned"] = pgs.ServesReturned,
            ["Max Ace Streak"] = pgs.AceStreak,
            ["Max Serve Streak"] = pgs.ServeStreak,
        };
        if (!formatData)
            return;
        FormatData();
    }
}