using HandballBackend.Database;
using HandballBackend.Database.Models;
using HandballBackend.Database.SendableTypes;
using HandballBackend.EndpointHelpers;
using HandballBackend.ErrorTypes;
using HandballBackend.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class OfficialsController : ControllerBase {
    public record GetOfficialsResponse {
        public required OfficialData[] Officials { get; set; }
        public TournamentData? Tournament { get; set; }
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetOfficialsResponse>> GetManyOfficials(
        [FromQuery(Name = "tournament")] string? tournamentSearchable = null,
        [FromQuery] bool returnTournament = false,
        [FromQuery] int limit = -1,
        [FromQuery] int page = -1
    ) {
        var db = new HandballContext();
        IQueryable<Official> query;


        if (!Utilities.TournamentOrElse(db, tournamentSearchable, out var tournament)) {
            return NotFound(new InvalidTournament(tournamentSearchable));
        }

        var isAdmin = PermissionHelper.IsUmpireManager(tournament);

        if (tournament is not null) {
            query = db.TournamentOfficials
                .Where(a => a.TournamentId == tournament.Id)
                .IncludeRelevant()
                .OrderBy(p => p.Official.Person.SearchableName)
                .Select(to => to.Official);
        } else {
            query = db.Officials
                .IncludeRelevant()
                .OrderBy(p => p.Person.SearchableName);
        }

        if (returnTournament && tournament is null) {
            return BadRequest(new TournamentNotProvidedForReturn());
        }

        if (page > 0) {
            if (limit < 0) return BadRequest(new ActionNotAllowed("Cannot pass page without passing a limit"));
            query = query.Skip(page * limit);
        }

        if (limit > 0) {
            query = query.Take(limit);
        }

        var officials = await query.Select(o => o.ToSendableData(null, false, isAdmin)).ToArrayAsync();

        return new GetOfficialsResponse {
            Officials = officials,
            Tournament = returnTournament ? tournament!.ToSendableData() : null
        };
    }

    public record GetOfficialResponse {
        public required OfficialData Official { get; set; }
        public TournamentData? tournament { get; set; }
    }


    [HttpGet("{searchable}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetOfficialResponse>> GetOneOfficial(
        string searchable,
        [FromQuery(Name = "tournament")] string? tournamentSearchable = null,
        [FromQuery] bool returnTournament = false
    ) {
        var db = new HandballContext();

        var official = await db.Officials.Where(o => o.Person.SearchableName == searchable).IncludeRelevant()
            .Include(o => o.Games)
            .ThenInclude(g => g.Players)
            .FirstOrDefaultAsync();
        if (official is null) {
            return NotFound(new DoesNotExist(nameof(official), searchable));
        }

        if (!Utilities.TournamentOrElse(db, tournamentSearchable, out var tournament)) {
            return NotFound(new InvalidTournament(tournamentSearchable));
        }

        var isAdmin = PermissionHelper.IsUmpireManager(tournament);


        if (returnTournament && tournament is null) {
            return BadRequest(new TournamentNotProvidedForReturn());
        }


        return new GetOfficialResponse {
            Official = official.ToSendableData(tournament, true, isAdmin: isAdmin),
            tournament = returnTournament ? tournament!.ToSendableData() : null
        };
    }


    public class AddOfficialRequest {
        public required string OfficialSearchableName { get; set; }
        public required string Tournament { get; set; }

        public required int UmpireProficiency { get; set; }
        public required int ScorerProficiency { get; set; }

        public string Role { get; set; } = "Umpire";
    }

    [HttpPost("addToTournament")]
    [TournamentAuthorize(PermissionType.UmpireManager)]
    public async Task<ActionResult> AddOfficialToTournament(
        [FromBody] AddOfficialRequest request) {
        var db = new HandballContext();
        var tournament = await db.Tournaments
            .FirstOrDefaultAsync(a => a.SearchableName == request.Tournament);
        if (tournament is null) {
            return NotFound("Invalid Tournament");
        }

        if (tournament.Started) {
            return NotFound("Tournament has already started!");
        }

        var official = await db.Officials.IncludeRelevant().Include(official => official.TournamentOfficials)
            .FirstOrDefaultAsync(o => o.Person.SearchableName == request.OfficialSearchableName);


        if (official == null) {
            return BadRequest("The Official doesn't exist");
        }

        if (official.TournamentOfficials.Any(to => to.TournamentId == tournament.Id)) {
            return BadRequest("That official is already in this tournament!");
        }

        if (!Enum.TryParse<OfficialRole>(request.Role.Replace(" ", ""), out var role)) {
            return BadRequest("Invalid Role");
        }

        await db.TournamentOfficials.AddAsync(new TournamentOfficial {
            TournamentId = tournament.Id,
            OfficialId = official.Id,
            Role = role,
            UmpireProficiency = request.UmpireProficiency,
            ScorerProficiency = request.ScorerProficiency,
        });

        await db.SaveChangesAsync();
        return Ok();
    }

    public class RemoveOfficialRequest {
        public required string OfficialSearchableName { get; set; }
        public required string Tournament { get; set; }
    }

    [TournamentAuthorize(PermissionType.UmpireManager)]
    [HttpDelete("removeFromTournament")]
    public async Task<ActionResult> RemoveOfficialFromTournament([FromBody] RemoveOfficialRequest request) {
        var db = new HandballContext();
        var tournament = await db.Tournaments
            .FirstOrDefaultAsync(a => a.SearchableName == request.Tournament);
        if (tournament is null) {
            return NotFound("Invalid Tournament");
        }

        if (tournament.Started) {
            return NotFound("Tournament has already started!");
        }

        var tournamentOfficial = await db.TournamentOfficials.FirstOrDefaultAsync(to =>
            to.TournamentId == tournament.Id && to.Official.Person.SearchableName == request.OfficialSearchableName);


        if (tournamentOfficial == null) {
            return BadRequest("The Official doesn't exist");
        }

        if (tournamentOfficial.Role.ToPermissionType() >= PermissionHelper.GetRequestPermissions(tournament)) {
            return Forbid("You cannot delete someone with permissions higher than your own!");
        }


        db.TournamentOfficials.Remove(tournamentOfficial);

        await db.SaveChangesAsync();
        return Ok();
    }

    public class UpdateOfficialRequest {
        public required string OfficialSearchableName { get; set; }
        public required string Tournament { get; set; }

        public int? UmpireProficiency { get; set; }
        public int? ScorerProficiency { get; set; }
        public string? Role { get; set; }
    }

    [TournamentAuthorize(PermissionType.UmpireManager)]
    [HttpPost("updateForTournament")]
    public async Task<ActionResult> UpdateOfficialFromTournament([FromBody] UpdateOfficialRequest request) {
        var db = new HandballContext();
        var tournament = await db.Tournaments
            .FirstOrDefaultAsync(a => a.SearchableName == request.Tournament);
        if (tournament is null) {
            return NotFound("Invalid Tournament");
        }

        if (tournament.Started) {
            return NotFound("Tournament has already started!");
        }

        var tournamentOfficial = await db.TournamentOfficials.FirstOrDefaultAsync(to =>
            to.TournamentId == tournament.Id && to.Official.Person.SearchableName == request.OfficialSearchableName);


        if (tournamentOfficial == null) {
            return BadRequest("The Official doesn't exist");
        }

        if (request.UmpireProficiency.HasValue) {
            tournamentOfficial.UmpireProficiency = request.UmpireProficiency.Value;
        }

        if (request.ScorerProficiency.HasValue) {
            tournamentOfficial.ScorerProficiency = request.ScorerProficiency.Value;
        }

        if (request.Role != null) {
            if (Enum.TryParse<OfficialRole>(request.Role.Replace(" ", ""), out var role)) {
                if (tournamentOfficial.Role.ToPermissionType() >= PermissionHelper.GetRequestPermissions(tournament)) {
                    return Forbid("You cannot set permissions of someone who is higher than your own!");
                }

                if (role.ToPermissionType() >= PermissionHelper.GetRequestPermissions(tournament)) {
                    return Forbid("You cannot set permissions higher than your own!");
                }

                tournamentOfficial.Role = role;
            } else {
                return BadRequest("Invalid Role");
            }
        }

        await db.SaveChangesAsync();
        return Ok();
    }
}