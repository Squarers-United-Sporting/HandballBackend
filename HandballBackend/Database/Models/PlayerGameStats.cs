using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HandballBackend.Database.SendableTypes;
using HandballBackend.Utils;

namespace HandballBackend.Database.Models;

[Table("player_game_stats")]
public class PlayerGameStats {
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("game_id")]
    public required int GameId { get; set; }

    [Required]
    [Column("player_id")]
    public required int PlayerId { get; set; }

    [Required]
    [Column("team_id")]
    public required int TeamId { get; set; }

    [Required]
    [Column("opponent_id")]
    public required int? OpponentId { get; set; }

    [Required]
    [Column("tournament_id")]
    public required int TournamentId { get; set; }

    [Required]
    [Column("rounds_on_court")]
    public int RoundsOnCourt { get; set; } = 0;

    [Required]
    [Column("rounds_carded")]
    public int RoundsCarded { get; set; } = 0;

    [Required]
    [Column("points_scored")]
    public int PointsScored { get; set; } = 0;

    [Required]
    [Column("aces_scored")]
    public int AcesScored { get; set; } = 0;

    [Required]
    [Column("merits")]
    public int Merits { get; set; } = 0;

    [Required]
    [Column("faults")]
    public int Faults { get; set; } = 0;

    [Required]
    [Column("served_points")]
    public int ServedPoints { get; set; } = 0;

    [Required]
    [Column("served_points_won")]
    public int ServedPointsWon { get; set; } = 0;

    [Required]
    [Column("serves_received")]
    public int ServesReceived { get; set; } = 0;

    [Required]
    [Column("serves_returned")]
    public int ServesReturned { get; set; } = 0;

    [Required]
    [Column("double_faults")]
    public int DoubleFaults { get; set; } = 0;

    [Required]
    [Column("green_cards")]
    public int GreenCards { get; set; } = 0;

    [Required]
    [Column("warnings")]
    public int Warnings { get; set; } = 0;

    [Required]
    [Column("yellow_cards")]
    public int YellowCards { get; set; } = 0;

    [Required]
    [Column("red_cards")]
    public int RedCards { get; set; } = 0;

    [Required]
    [Column("card_time_remaining")]
    public int CardTimeRemaining { get; set; } = 0;

    [Required]
    [Column("card_time")]
    public int CardTime { get; set; } = 0;

    [Column("start_side", TypeName = "TEXT")]
    public string? StartSide { get; set; }

    [Required]
    [Column("created_at")]
    public long CreatedAt { get; set; } = Utilities.GetUnixSeconds();

    [Required]
    [Column("is_best_player")]
    public int BestPlayerVotes { get; set; } = 0;

    [Required]
    [Column("ace_streak")]
    public int AceStreak { get; set; } = 0;

    [Required]
    [Column("serve_streak")]
    public int ServeStreak { get; set; } = 0;

    [Required]
    [Column("demerits")]
    public int Demerits { get; set; } = 0;

    [Column("side_of_court")]
    public string? SideOfCourt { get; set; }

    [Column("rating")]
    public int? Rating { get; set; }

    [Column("initial_elo")]
    public double InitialElo { get; set; }

    [Column("elo_delta")]
    public double? EloDelta { get; set; }

    [Column("is_libero")]
    public bool IsLibero { get; set; }

    [ForeignKey("GameId")]
    public Game Game { get; set; }

    [ForeignKey("PlayerId")]
    public Person Player { get; set; }

    [ForeignKey("TeamId")]
    public Team Team { get; set; }

    [ForeignKey("OpponentId")]
    public Team Opponent { get; set; }

    [ForeignKey("TournamentId")]
    public Tournament Tournament { get; set; }

    [NotMapped]
    public string? ActingSideOfCourt {
        get {
            Console.WriteLine(Game.TeamOneScore);
            if (Game.Ended || !Game.Started) return SideOfCourt;
            var db = new HandballContext();
            var teammates = db.PlayerGameStats.Where(pgs2 =>
                pgs2.GameId == GameId &&
                pgs2.TeamId == TeamId &&
                pgs2.PlayerId != PlayerId);
            if (SideOfCourt == Game.SideToServe) {
                // the serve is on our side, meaning we need to swap sides if:
                //  1) we are carded (as carded players cannot serve or receive serves)
                //  2) we are not serving and our teammate is the libero
                if (
                    CardTimeRemaining != 0 || // we are carded
                    teammates.Any(pgs2 =>
                        pgs2.IsLibero && pgs2.SideOfCourt != "Substitute" && pgs2.CardTimeRemaining == 0) &&
                    TeamId != Game.TeamToServeId // our teammate is libero and we are receiving the serve
                ) {
                    return SideOfCourt == "Left" ? "Right" : "Left";
                }
            } else {
                // the serve is on the other side, meaning we need to swap sides if:
                //  1) our teammate is carded (as they can not serve or receive serves)
                //  2) we are the libero and the other team is serving
                if (teammates.Any(pgs2 => pgs2.CardTimeRemaining != 0) ||
                    (IsLibero && TeamId != Game.TeamToServeId && CardTimeRemaining == 0)) {
                    return SideOfCourt == "Left" ? "Right" : "Left";
                }
            }

            return SideOfCourt;
        }
    }

    public string? ActingSideOfCourtAtEvent(GameEvent gE) {
        var db = new HandballContext();
        var teammates = db.PlayerGameStats.Where(pgs2 =>
            pgs2.GameId == GameId &&
            pgs2.TeamId == TeamId &&
            pgs2.PlayerId != PlayerId);
        if (SideOfCourt == gE.SideToServe) {
            // the serve is on our side, meaning we need to swap sides if:
            //  1) we are carded (as carded players cannot serve or receive serves)
            //  2) we are not serving and our teammate is the libero
            if (
                CardTimeRemaining != 0 || // we are carded
                teammates.Any(pgs2 =>
                    pgs2.IsLibero && pgs2.SideOfCourt != "Substitute" && pgs2.CardTimeRemaining == 0) &&
                TeamId != gE.TeamToServeId // our teammate is libero and we are receiving the serve
            ) {
                return SideOfCourt == "Left" ? "Right" : "Left";
            }
        } else {
            // the serve is on the other side, meaning we need to swap sides if:
            //  1) our teammate is carded (as they can not serve or receive serves)
            //  2) we are the libero and the other team is serving
            if (teammates.Any(pgs2 => pgs2.CardTimeRemaining != 0) ||
                (IsLibero && TeamId != gE.TeamToServeId && CardTimeRemaining == 0)) {
                return SideOfCourt == "Left" ? "Right" : "Left";
            }
        }

        return SideOfCourt;
    }

    public GamePlayerData ToSendableData(bool includeStats = false,
        bool formatData = false,
        bool isUmpire = false,
        bool isAdmin = false) {
        return new GamePlayerData(this, includeStats, formatData, isUmpire, isAdmin);
    }

    public void ResetStats() {
        RoundsOnCourt = 0;
        RoundsCarded = 0;
        PointsScored = 0;
        AcesScored = 0;
        Faults = 0;
        DoubleFaults = 0;
        ServedPoints = 0;
        ServedPointsWon = 0;
        ServesReceived = 0;
        ServesReturned = 0;
        Warnings = 0;
        GreenCards = 0;
        YellowCards = 0;
        RedCards = 0;
        CardTime = 0;
        CardTimeRemaining = 0;
        BestPlayerVotes = 0;
        Merits = 0;
        Demerits = 0;
    }
}