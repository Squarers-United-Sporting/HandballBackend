using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HandballBackend.Database.SendableTypes;
using HandballBackend.Utils;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.Database.Models;

[Table("games")]
public class Game : IHasRelevant<Game> {
    public static readonly string[] ResolvedStatuses = [
        "Resolved",
        "In Progress",
        "Official",
        "Ended",
        "Waiting for Start",
        "Forfeit",
    ];

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("tournament_id")]
    public required int TournamentId { get; set; }

    [Required]
    [Column("team_one_id")]
    public required int TeamOneId { get; set; }

    [Required]
    [Column("team_two_id")]
    public required int TeamTwoId { get; set; }

    [Required]
    [Column("team_one_score")]
    public int TeamOneScore { get; set; } = 0;

    [Required]
    [Column("team_two_score")]
    public int TeamTwoScore { get; set; } = 0;

    [Required]
    [Column("team_one_timeouts")]
    public int TeamOneTimeouts { get; set; } = 0;

    [Required]
    [Column("team_two_timeouts")]
    public int TeamTwoTimeouts { get; set; } = 0;

    [Column("winning_team_id")]
    public int? WinningTeamId { get; set; }

    [Required]
    [Column("started")]
    public bool Started { get; set; } = false;

    [Required]
    [Column("ended")]
    public bool Ended { get; set; } = false;

    [Required]
    [Column("someone_has_won")]
    public required bool SomeoneHasWon { get; set; }

    [Required]
    [Column("protested")]
    public bool Protested { get; set; } = false;

    [Required]
    [Column("resolved")]
    public bool Resolved { get; set; } = false;

    [Required]
    [Column("ranked")]
    public bool Ranked { get; set; } = true;

    [Column("best_player_id")]
    public int? BestPlayerId { get; set; }

    [Column("official_id")]
    public int? OfficialId { get; set; }

    [Column("scorer_id")]
    public int? ScorerId { get; set; }

    [Column("iga_side_id")]
    public int? IgaSideId { get; set; }

    [Column("player_to_serve_id")]
    public int? PlayerToServeId { get; set; }

    [Column("team_to_serve_id")]
    public int? TeamToServeId { get; set; }

    [Column("side_to_serve", TypeName = "TEXT")]
    public string? SideToServe { get; set; }

    [Column("start_time")]
    public int? StartTime { get; set; }

    [Column("length")]
    public int? Length { get; set; }

    [Required]
    [Column("court")]
    public int Court { get; set; } = 0;

    [Required]
    [Column("is_final")]
    public required bool IsFinal { get; set; }

    [Required]
    [Column("round")]
    public required int Round { get; set; }

    [Column("notes", TypeName = "TEXT")]
    public string? Notes { get; set; }

    [Required]
    [Column("is_bye")]
    public required bool IsBye { get; set; }

    [Required]
    [Column("status", TypeName = "TEXT")]
    public string Status { get; set; } = "Waiting For Start";

    [Column("admin_status", TypeName = "TEXT")]
    public string AdminStatus { get; set; } = "Waiting For Start";

    [Required]
    [Column("noteable_status", TypeName = "TEXT")]
    public string NoteableStatus { get; set; } = "Waiting For Start";

    [Column("created_at")]
    public long CreatedAt { get; set; } = Utilities.GetUnixSeconds();

    [Column("serve_timer")]
    public int? ServeTimer { get; set; }

    [Required]
    [Column("marked_for_review")]
    public bool MarkedForReview { get; set; } = false;

    [Required]
    [Column("game_number")]
    public required int GameNumber { get; set; }

    [Required]
    [Column("blitz_game")]
    public bool BlitzGame { get; set; } = false;

    [ForeignKey("TournamentId")]
    public Tournament Tournament { get; set; }

    [ForeignKey("TeamOneId")]
    public Team TeamOne { get; set; }

    [ForeignKey("TeamTwoId")]
    public Team TeamTwo { get; set; }

    [ForeignKey("BestPlayerId")]
    public Person? BestPlayer { get; set; }

    [ForeignKey("OfficialId")]
    public Official? Official { get; set; }

    [ForeignKey("ScorerId")]
    public Official? Scorer { get; set; }

    public ICollection<GameEvent> Events { get; set; } = new List<GameEvent>();

    public ICollection<PlayerGameStats> Players { get; set; } = new List<PlayerGameStats>();

    [NotMapped]
    public int LosingTeamId => TeamOneId == WinningTeamId ? TeamTwoId : TeamOneId;

    [NotMapped]
    public Team WinningTeam => TeamOneId == WinningTeamId ? TeamOne : TeamTwo;

    [NotMapped]
    public Team LosingTeam => TeamOneId == LosingTeamId ? TeamOne : TeamTwo;

    [NotMapped]
    public int ScoreToWin => BlitzGame ? 7 : 11;

    [NotMapped]
    public int ScoreToForceWin => 2 * ScoreToWin;

    public GameData ToSendableData(bool includeTournament = false,
        bool includeGameEvents = false,
        bool includeStats = false,
        bool formatData = false,
        bool isUmpire = false,
        bool isAdmin = false) {
        return new GameData(
            this,
            includeTournament,
            includeGameEvents,
            includeStats,
            formatData,
            isUmpire,
            isAdmin
        );
    }

    public void Reset() {
        Started = false;
        SomeoneHasWon = false;
        Ended = false;
        Protested = false;
        Resolved = false;
        BestPlayerId = null;
        TeamOneScore = 0;
        TeamTwoScore = 0;
        TeamOneTimeouts = 0;
        TeamTwoTimeouts = 0;
        Notes = null;
        WinningTeamId = null;
        Status = "Waiting For Start";
        AdminStatus = "Waiting For Start";
        NoteableStatus = "Waiting For Start";
    }

    public static IQueryable<Game> GetRelevant(IQueryable<Game> query) {
        return query
            .Include(x => x.Tournament)
            .Include(x => x.TeamOne.Captain)
            .Include(x => x.TeamOne.NonCaptain)
            .Include(x => x.TeamOne.Substitute)
            .Include(t => t.TeamOne.TournamentTeams)
            .Include(x => x.TeamTwo.Captain)
            .Include(x => x.TeamTwo.NonCaptain)
            .Include(x => x.TeamTwo.Substitute)
            .Include(t => t.TeamTwo.TournamentTeams)
            .Include(x => x.Tournament)
            .Include(x => x.BestPlayer)
            .Include(x => x.Official.Person)
            .Include(x => x.Scorer.Person)
            .Include(x => x.Players)
            .ThenInclude(pgs => pgs.Player);
    }
}