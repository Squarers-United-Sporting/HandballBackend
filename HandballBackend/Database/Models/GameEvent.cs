using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HandballBackend.Database.SendableTypes;
using HandballBackend.Utils;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.Database.Models;

public enum GameEventType {
    Start,
    EndGame,
    Score,
    Fault,
    Timeout,
    EndTimeout,
    Abandon,
    Forfeit,
    Warning,
    GreenCard,
    YellowCard,
    RedCard,
    Substitute,
    Notes,
    Protest,
    Resolve,
    Votes,
    Merit,
    Demerit,
    Replay
}

[Table("game_events")]
public class GameEvent : IHasRelevant<GameEvent> {
    [NotMapped]
    public static readonly GameEventType[] CardTypes = [
        GameEventType.Warning,
        GameEventType.GreenCard,
        GameEventType.YellowCard,
        GameEventType.RedCard,
    ];

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("game_id")]
    public required int GameId { get; set; }

    [Column("player_id")]
    public int? PlayerId { get; set; }

    [Column("team_id")]
    public int? TeamId { get; set; }

    [Required]
    [Column("tournament_id")]
    public required int TournamentId { get; set; }

    [Required]
    [Column("event_type", TypeName = "TEXT")]
    public required GameEventType EventType { get; set; }

    [Required]
    [Column("created_at")]
    public long CreatedAt { get; set; } = Utilities.GetUnixSeconds();

    [Column("details")]
    public int? Details { get; set; }

    [Column("notes", TypeName = "TEXT")]
    public string? Notes { get; set; }

    [Column("player_who_served_id")]
    public int? PlayerWhoServedId { get; set; }

    [Column("team_who_served_id")]
    public int? TeamWhoServedId { get; set; }

    [Column("side_served", TypeName = "TEXT")]
    public string? SideServed { get; set; }

    [Column("player_to_serve_id")]
    public int? PlayerToServeId { get; set; }

    [Column("team_to_serve_id")]
    public int? TeamToServeId { get; set; }

    [Column("side_to_serve", TypeName = "TEXT")]
    public string SideToServe { get; set; }

    [Column("team_one_left_id")]
    public int? TeamOneLeftId { get; set; }

    [Column("team_one_right_id")]
    public int? TeamOneRightId { get; set; }

    [Column("team_two_left_id")]
    public int? TeamTwoLeftId { get; set; }

    [Column("team_two_right_id")]
    public int? TeamTwoRightId { get; set; }

    [ForeignKey("PlayerId")]
    public Person Player { get; set; }

    [ForeignKey("TeamId")]
    public Team Team { get; set; }

    [ForeignKey("TournamentId")]
    public Tournament Tournament { get; set; }

    [ForeignKey("TeamOneLeftId")]
    public Person? TeamOneLeft { get; set; }

    [ForeignKey("TeamOneRightId")]
    public Person? TeamOneRight { get; set; }

    [ForeignKey("TeamTwoLeftId")]
    public Person? TeamTwoLeft { get; set; }

    [ForeignKey("TeamTwoRightId")]
    public Person? TeamTwoRight { get; set; }

    [ForeignKey("GameId")]
    public Game Game { get; set; }

    [NotMapped]
    public bool IsCard =>
        EventType == GameEventType.Warning || EventType.ToString().EndsWith("Card");

    public GameEventData ToSendableData(bool includeGame = false) {
        return new GameEventData(this, includeGame);
    }

    public static IQueryable<GameEvent> GetRelevant(IQueryable<GameEvent> query) {
        return query
            .Include(v => v.Player)
            .Include(v => v.Tournament)
            .Include(v => v.Team)
            .Include(v => v.TeamOneLeft)
            .Include(v => v.TeamOneRight)
            .Include(v => v.TeamTwoLeft)
            .Include(v => v.TeamTwoRight)
            .Include(v => v.Game);
    }
}