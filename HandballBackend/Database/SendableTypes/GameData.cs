using HandballBackend.Database.Models;

namespace HandballBackend.Database.SendableTypes;

public class AdminGameData {
    private static readonly string[] NO_ACTION_REQUIRED = {
        "Resolved", "In Progress", "Official", "Ended", "Waiting For Start", "Forfeit", "Bye"
    };

    public bool MarkedForReview { get; set; }
    public bool RequiresAction { get; set; }
    public string NoteableStatus { get; set; }
    public string Notes { get; set; }
    public int TeamOneRating { get; set; }
    public int TeamTwoRating { get; set; }
    public string? TeamOneNotes { get; set; }
    public string? TeamTwoNotes { get; set; }
    public string? TeamOneProtest { get; set; }
    public string? TeamTwoProtest { get; set; }
    public GameEventData[] Cards { get; set; }
    public bool Resolved { get; set; }

    public AdminGameData(Game game) {
        var teamNotes = game.Events.Where(a => a.EventType is GameEventType.Notes).ToArray();
        var protests = game.Events.Where(a => a.EventType is GameEventType.Protest).ToArray();
        MarkedForReview = game.MarkedForReview;
        RequiresAction = !NO_ACTION_REQUIRED.Contains(game.AdminStatus);
        NoteableStatus = game.NoteableStatus;
        Notes = (game.Notes?.Trim().Length ?? 0) > 0 ? game.Notes!.Trim() : "";
        TeamOneRating = game.Players.FirstOrDefault(pgs => pgs.TeamId == game.TeamOneId)?.Rating ?? 3;
        TeamTwoRating = game.Players.FirstOrDefault(pgs => pgs.TeamId == game.TeamTwoId)?.Rating ?? 3;
        TeamOneNotes = teamNotes
            .Where(ge => ge.TeamId == game.TeamOneId && ge.Notes != null)
            .Select(gE => gE.Notes)
            .FirstOrDefault();
        TeamTwoNotes = teamNotes
            .Where(ge => ge.TeamId == game.TeamTwoId && ge.Notes != null)
            .Select(gE => gE.Notes)
            .FirstOrDefault();
        TeamOneProtest = protests
            .Where(ge => ge.TeamId == game.TeamOneId && ge.Notes != null)
            .Select(gE => gE.Notes)
            .FirstOrDefault();
        TeamTwoProtest = protests
            .Where(ge => ge.TeamId == game.TeamTwoId && ge.Notes != null)
            .Select(gE => gE.Notes)
            .FirstOrDefault();
        Cards = game.Events.Where(a => GameEvent.CardTypes.Contains(a.EventType))
            .Select(a => a.ToSendableData())
            .OrderBy(ge => ge.Id)
            .ToArray();
        Resolved = game.Resolved;
    }
}

public class GameData {
    public int Id { get; private set; }
    public TournamentData? Tournament { get; private set; }
    public GameTeamData TeamOne { get; private set; }
    public GameTeamData TeamTwo { get; private set; }
    public int TeamOneScore { get; private set; }
    public int TeamTwoScore { get; private set; }
    public int TeamOneTimeouts { get; private set; }
    public int TeamTwoTimeouts { get; private set; }
    public bool FirstTeamWinning { get; private set; }
    public bool Started { get; private set; }
    public bool SomeoneHasWon { get; private set; }
    public bool Ended { get; private set; }
    public bool Protested { get; private set; }
    public bool Abandoned { get; private set; }
    public bool Ranked { get; private set; }
    public PersonData? BestPlayer { get; private set; }
    public OfficialData? Official { get; private set; }
    public OfficialData? Scorer { get; private set; }
    public bool FirstTeamIga { get; private set; }
    public bool FirstTeamToServe { get; private set; }

    public bool FirstTeamScoredLast { get; private set; }
    public string SideToServe { get; private set; }
    public int? StartTime { get; private set; }
    public int? ServeTimer { get; private set; }
    public int? Length { get; private set; }
    public bool IsFinal { get; private set; }
    public int Round { get; private set; }
    public bool IsBye { get; private set; }
    public string Status { get; private set; }
    public bool Faulted { get; private set; }
    public int ChangeCode { get; private set; }
    public long? TimeoutExpirationTime { get; private set; }
    public bool IsOfficialTimeout { get; private set; }

    public GameEventData[]? Events { get; private set; }

    public AdminGameData? Admin { get; private set; }
    public int Court { get; private set; }

    public bool BlitzGame { get; private set; }


    public GameData(
        Game game,
        bool includeTournament = false,
        bool includeGameEvents = false,
        bool includeStats = false,
        bool formatData = false,
        bool isAdmin = false
    ) {
        Id = game.GameNumber;
        Tournament = includeTournament ? game.Tournament.ToSendableData() : null;
        TeamOne = game.TeamOne.ToGameSendableData(game, includeStats, formatData, isAdmin);
        TeamTwo = game.TeamTwo.ToGameSendableData(game, includeStats, formatData, isAdmin);
        TeamOneScore = game.TeamOneScore;
        TeamTwoScore = game.TeamTwoScore;
        TeamOneTimeouts = game.TeamOneTimeouts;
        TeamTwoTimeouts = game.TeamTwoTimeouts;
        FirstTeamWinning = game.WinningTeamId == game.TeamOneId;
        Started = game.Started;
        SomeoneHasWon = game.SomeoneHasWon;
        Ended = game.Ended;
        Protested = game.Protested;
        Ranked = game.Ranked;
        BestPlayer = game.BestPlayer?.ToSendableData();
        Official = game.Official?.ToSendableData(isAdmin: isAdmin);
        Scorer = game.Scorer?.ToSendableData(isAdmin: isAdmin);
        FirstTeamIga = game.TeamOneId == game.IgaSideId;
        FirstTeamToServe = game.TeamToServeId == game.TeamOneId;
        SideToServe = game.SideToServe ?? "Left";
        StartTime = game.StartTime;
        ServeTimer = game.ServeTimer;
        Length = game.Length;
        IsFinal = game.IsFinal;
        Round = game.Round;
        IsBye = game.IsBye;
        Status = isAdmin ? game.Status : game.AdminStatus;
        Faulted = game.Events
            .Where(a => a.EventType is GameEventType.Fault or GameEventType.Score)
            .OrderBy(a => a.Id)
            .Select(a => a.EventType == GameEventType.Fault)
            .LastOrDefault(false);
        ChangeCode = game.Events.Select(a => a.Id).OrderByDescending(a => a).FirstOrDefault(game.Id);
        var lastTimeoutEvent = game.Events
            .Where(a => a.EventType is GameEventType.Timeout or GameEventType.EndTimeout)
            .OrderByDescending(a => a.Id).FirstOrDefault();
        TimeoutExpirationTime =
            lastTimeoutEvent?.EventType == GameEventType.Timeout
                ? (lastTimeoutEvent.CreatedAt + Config.TimeoutTime) * 1000
                : -1;

        IsOfficialTimeout = game.Events
            .Where(a => a.EventType is GameEventType.Timeout)
            .Select(a => a.TeamId is null)
            .LastOrDefault(false);
        Court = game.Court;

        Abandoned = game.Events.Any(gE => gE.EventType == GameEventType.Abandon);
        var mostRecentPoint = game.Events.Where(ge => ge.EventType == GameEventType.Score)
            .OrderByDescending(gE => gE.Id).FirstOrDefault();
        FirstTeamScoredLast = game.TeamOneId == mostRecentPoint?.TeamId;
        BlitzGame = game.BlitzGame;
        if (includeGameEvents) {
            Events = game.Events.Select(a => a.ToSendableData()).OrderBy(gE => gE.Id).ToArray();
        }

        if (isAdmin) {
            Admin = new AdminGameData(game);
            Status = game.AdminStatus;
        }
    }
}