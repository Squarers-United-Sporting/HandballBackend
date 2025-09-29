using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HandballBackend.Database.SendableTypes;
using HandballBackend.Utils;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.Database.Models;

[Table("officials")]
public class Official : IHasRelevant<Official> {
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("person_id")]
    public required int PersonId { get; set; }

    [Required]
    [Column("proficiency")]
    public required int Proficiency { get; set; }

    [Required]
    [Column("created_at")]
    public long CreatedAt { get; set; } = Utilities.GetUnixSeconds();

    [ForeignKey("PersonId")]
    public Person Person { get; set; }

    public List<Game> Games { get; set; } = [];

    public List<Game> ScoredGames { get; set; } = [];

    public List<TournamentOfficial> TournamentOfficials { get; set; } = [];

    public OfficialData ToSendableData(Tournament? tournament = null, bool includeStats = false, bool isAdmin = false) {
        return new OfficialData(this, tournament, includeStats, isAdmin);
    }

    public static IQueryable<Official> GetRelevant(IQueryable<Official> query) {
        return query.Include(o => o.Person).Include(o => o.TournamentOfficials);
    }
}