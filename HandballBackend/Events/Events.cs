namespace HandballBackend.Events;

public abstract record Event;

public record GameEndEvent(int GameId): Event;

public record UpdateElosEvent: Event;

public record RoundEndEvent(int TournamentId): Event;
