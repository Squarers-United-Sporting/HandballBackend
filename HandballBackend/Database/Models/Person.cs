using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HandballBackend.Database.SendableTypes;
using HandballBackend.EndpointHelpers;
using HandballBackend.Utils;

namespace HandballBackend.Database.Models;

[Table("people")]
public class Person {
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("name", TypeName = "TEXT")]
    public required string Name { get; set; }

    [Required]
    [Column("searchable_name", TypeName = "TEXT")]
    public required string SearchableName { get; set; }

    [Column("password", TypeName = "TEXT")]
    public string? Password { get; set; }

    [Column("image_url", TypeName = "TEXT")]
    public string? ImageUrl { get; set; }

    [Column("big_image_url", TypeName = "TEXT")]
    public string? BigImageUrl { get; set; }

    [Column("session_token", TypeName = "TEXT")]
    public string? SessionToken { get; set; }

    [Column("token_timeout")]
    public int? TokenTimeout { get; set; }

    [Required]
    [Column("created_at")]
    public long? CreatedAt { get; set; } = Utilities.GetUnixSeconds();

    [Required]
    [Column("permission_level")]
    public PermissionType PermissionLevel { get; set; }

    [Column("phone_number", TypeName = "TEXT")]
    public string? PhoneNumber { get; set; }

    [Column("availability")]
    public int? Availability { get; set; }

    public IEnumerable<PlayerGameStats>? PlayerGameStats { get; set; }

    public List<GameEvent>? Events { get; set; }

    public Official? Official { get; set; }

    public string InitialLastName {
        get {
            if (!Name.Contains(' '))
                return Name;
            return Name[0] + ". " + string.Join(" ", Name.Split(" ")[1..]);
        }
    }


    public double Elo(int? gameId = null, int? tournamentId = null) {
        if (gameId.HasValue) {
            var pgs = PlayerGameStats?.First(g => g.GameId == gameId);
            if (pgs is not null) {
                if (pgs.EloDelta is not null) {
                    return (double) (pgs.EloDelta + pgs.InitialElo);
                }

                return pgs.InitialElo;
            }
        }

        if (tournamentId.HasValue) {
            var pgs = PlayerGameStats
                ?.Where(pgs => pgs.TournamentId == tournamentId)
                .OrderByDescending(pgs => pgs.GameId)
                .FirstOrDefault();
            if (pgs is not null) {
                if (pgs.EloDelta is not null) {
                    return (double) (pgs.EloDelta + pgs.InitialElo);
                }

                return pgs.InitialElo;
            }
        }

        return EloCalculator.GetPlayerElos().GetValueOrDefault(Id, 1500);
    }

    public PersonData ToSendableData(
        Tournament? tournament = null,
        bool generateStats = false,
        Team? team = null,
        bool formatData = false,
        bool admin = false
    ) {
        return new PersonData(this, tournament, generateStats, team, formatData, admin);
    }
}