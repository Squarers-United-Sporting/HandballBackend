using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HandballBackend.Database.SendableTypes;
using HandballBackend.Utils;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.Database.Models;

[Table("teams")]
public class Team : IHasRelevant<Team> {
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("name", TypeName = "TEXT")]
    public required string Name { get; set; }

    [Column("long_name", TypeName = "TEXT")]
    public string? LongName { get; set; }

    [Required]
    [Column("searchable_name", TypeName = "TEXT")]
    public required string SearchableName { get; set; }

    [Column("image_url", TypeName = "TEXT")]
    public string? ImageUrl { get; set; }

    [Column("captain_id")]
    public int? CaptainId { get; set; }

    [Column("non_captain_id")]
    public int? NonCaptainId { get; set; }

    [Column("substitute_id")]
    public int? SubstituteId { get; set; }

    [Required]
    [Column("created_at")]
    public long CreatedAt { get; set; } = Utilities.GetUnixSeconds();

    [Column("team_color", TypeName = "TEXT")]
    public string? TeamColor { get; set; }

    [Column("big_image_url", TypeName = "TEXT")]
    public string? BigImageUrl { get; set; }

    public Person? Captain { get; set; } = null!;

    public Person? NonCaptain { get; set; } = null!;

    public Person? Substitute { get; set; } = null!;

    public List<PlayerGameStats> PlayerGameStats { get; set; } = [];

    public List<TournamentTeam> TournamentTeams { get; set; } = [];

    [NotMapped]
    public List<Person> People =>
        new List<Person?> { Captain, NonCaptain, Substitute }
            .Where(p => p != null)
            .Select(p => p!)
            .ToList();

    public TeamData ToSendableData(
        bool generateStats = false,
        bool generatePlayerStats = false,
        bool formatData = false,
        Tournament? tournament = null
    ) {
        return new TeamData(this, tournament, generateStats, generatePlayerStats, formatData);
    }

    public double DisplayElo(Tournament? tournament = null) {
        var lastGame = PlayerGameStats.OrderByDescending(pgs => pgs.TournamentId == tournament?.Id)
            .ThenByDescending(pgs => pgs.GameId).GroupBy(pgs => pgs.GameId).SelectMany(i => i)
            .Select(pgs => pgs.EloDelta + pgs.InitialElo).Where(elo => elo.HasValue).Cast<double>().ToList();
        if (lastGame.Count != 0) {
            return lastGame.Average();
        }

        return TrueElo();
    }

    public double TrueElo() {
        var ids = new[] { CaptainId, NonCaptainId, SubstituteId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value);

        var allElos = EloCalculator.GetPlayerElos();

        return ids.Select(id => allElos.GetValueOrDefault(id, 1500)).DefaultIfEmpty(1500).Average();
    }

    public GameTeamData ToGameSendableData(Game game,
        bool generateStats = false,
        bool formatData = false,
        bool isUmpire = false,
        bool isAdmin = false) {
        if (Id == 1) {
            return new GameTeamData(this, game, false, false, false, false);
        }

        return new GameTeamData(this, game, generateStats, formatData, isUmpire, isAdmin);
    }

    public static IQueryable<Team> GetRelevant(IQueryable<Team> query) {
        return query
            .Include(t => t.Captain)
            .Include(t => t.NonCaptain)
            .Include(t => t.Substitute);
    }
}