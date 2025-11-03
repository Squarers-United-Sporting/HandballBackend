using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HandballBackend.Database.SendableTypes;
using HandballBackend.EndpointHelpers;
using HandballBackend.FixtureGenerator;
using HandballBackend.Utils;

namespace HandballBackend.Database.Models;

[Table("tournaments")]
public class Tournament {
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("name", TypeName = "TEXT")]
    public required string Name { get; set; }

    [Required]
    [Column("searchable_name", TypeName = "TEXT")]
    public required string SearchableName { get; set; }

    [Required]
    [Column("editable")]
    public required bool Editable { get; set; }

    [Required]
    [Column("fixtures_type", TypeName = "TEXT")]
    public required string FixturesType { get; set; }

    [Column("finals_type", TypeName = "TEXT")]
    public string FinalsType { get; set; }

    [Required]
    [Column("ranked")]
    public required bool Ranked { get; set; }

    [Required]
    [Column("two_courts")]
    public required bool TwoCourts { get; set; }

    [Required]
    [Column("finished")]
    public bool Finished { get; set; } = false;

    [Required]
    [Column("in_finals")]
    public bool InFinals { get; set; } = false;

    [Required]
    [Column("has_scorer")]
    public bool HasScorer { get; set; } = true;

    [Required]
    [Column("text_alerts")]
    public bool TextAlerts { get; set; } = true;

    [Required]
    [Column("is_pooled")]
    public bool IsPooled { get; set; } = false;

    [Column("notes", TypeName = "TEXT")]
    public string? Notes { get; set; }

    [Column("image_url", TypeName = "TEXT")]
    public string ImageUrl { get; set; }

    [Required]
    [Column("created_at")]
    public long CreatedAt { get; set; } = Utilities.GetUnixSeconds();

    [Required]
    [Column("badminton_serves")]
    public bool BadmintonServes { get; set; } = false;

    [Required]
    [Column("started")]
    public required bool Started { get; set; }

    [Required]
    [Column("color")]
    public required string Color { get; set; }

    public async Task EndRound() {
        var finals = InFinals;
        if (!finals) {
            finals = await GetFixtureGenerator.EndOfRound();
        }

        if (finals && !Finished) {
            await GetFinalGenerator.EndOfRound();
        }
    }

    public async Task BeginTournament() {
        var db = new HandballContext();
        (await db.Tournaments.FindAsync(Id))!.Started = true;
        await db.SaveChangesAsync();
        await GetFixtureGenerator.BeginTournament();
    }

    [NotMapped]
    public AbstractFixtureGenerator GetFinalGenerator =>
        AbstractFixtureGenerator.GetControllerByName(FinalsType, Id);

    [NotMapped]
    public AbstractFixtureGenerator GetFixtureGenerator =>
        AbstractFixtureGenerator.GetControllerByName(FixturesType, Id);


    public IQueryable<Person> GetPeopleInTournament() {
        var db = new HandballContext();
        var captainIds = db
            .TournamentTeams.Where(tt => tt.TournamentId == Id)
            .Select(tt => tt.Team.CaptainId);

        var nonCaptainIds = db
            .TournamentTeams.Where(tt => tt.TournamentId == Id)
            .Select(tt => tt.Team.NonCaptainId);

        var substituteIds = db
            .TournamentTeams.Where(tt => tt.TournamentId == Id)
            .Select(tt => tt.Team.SubstituteId);

        var personIds = captainIds
            .Union(nonCaptainIds)
            .Union(substituteIds)
            .Where(id => id != null)
            .Distinct()
            .ToList();

        return db.People.Where(p => personIds.Contains(p.Id))!;
    }

    public TournamentData ToSendableData() {
        return new TournamentData(this);
    }
}