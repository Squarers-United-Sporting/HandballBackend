using HandballBackend.Authentication;
using HandballBackend.Database;
using HandballBackend.Database.Models;
using HandballBackend.Database.SendableTypes;
using HandballBackend.EndpointHelpers;
using HandballBackend.ErrorTypes;
using HandballBackend.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class TournamentsController : ControllerBase {
    public record GetTournamentsResponse {
        public required TournamentData[] Tournaments { get; set; }
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<GetTournamentsResponse>> GetManyTournaments() {
        var db = new HandballContext();
        var tournaments = await db.Tournaments
            .OrderBy(t => t.Id)
            .Select(t => t.ToSendableData())
            .ToArrayAsync();
        return new GetTournamentsResponse {
            Tournaments = tournaments
        };
    }

    public record GetTournamentResponse {
        public required TournamentData Tournament { get; set; }
    }

    [HttpGet("{searchable}")]
    public async Task<ActionResult<GetTournamentResponse>> GetOneTournament(string searchable) {
        var db = new HandballContext();
        var tournament = await db.Tournaments
            .FirstOrDefaultAsync(a => a.SearchableName == searchable);
        if (tournament is null) {
            return NotFound("Invalid Tournament");
        }

        return new GetTournamentResponse {
            Tournament = tournament.ToSendableData()
        };
    }


    [HttpPost("{searchable}/start")]
    [TournamentAuthorize(PermissionType.UmpireManager)]
    public async Task<ActionResult> StartTournament(string searchable) {
        var db = new HandballContext();
        var tournament = await db.Tournaments
            .FirstOrDefaultAsync(a => a.SearchableName == searchable);
        if (tournament is null) {
            return NotFound("Invalid Tournament");
        }

        tournament.BeginTournament();
        return Ok();
    }

    [HttpPost("{searchable}/finalsNextRound")]
    [TournamentAuthorize(PermissionType.UmpireManager)]
    public async Task<ActionResult> PutTournamentInFinals(string searchable) {
        var db = new HandballContext();
        var tournament = await db.Tournaments
            .FirstOrDefaultAsync(a => a.SearchableName == searchable);
        if (tournament is null) {
            return NotFound("Invalid Tournament");
        }

        tournament.InFinals = true;
        await db.SaveChangesAsync();
        return Ok();
    }

    public class CreateTournamentRequest {
        public required string Name { get; set; }
        public required string FixturesType { get; set; }
        public required string FinalsType { get; set; }
    }

    public class CreateTournamentResponse {
        public required TournamentData Tournament;
    }

    [HttpPost("create")]
    [Authorize(Policy = Policies.IsAdmin)]
    public async Task<ActionResult> CreateTournament([FromBody] CreateTournamentRequest request) {
        var db = new HandballContext();
        var tournament = new Tournament {
            Name = request.Name,
            SearchableName = Utilities.ToSearchable(request.Name),
            Editable = false,
            FixturesType = request.FixturesType,
            FinalsType = request.FinalsType,
            Ranked = true,
            TwoCourts = true,
            Started = false,
            ImageUrl = "/api/image?name=blank"
        };
        await db.Tournaments.AddAsync(tournament);
        await db.SaveChangesAsync();
        return Created(Config.MY_ADDRESS + $"/api/tournaments/{tournament.SearchableName}", new CreateTournamentResponse {
            Tournament = tournament.ToSendableData()
        });
    }

    public class UpdateTournamentRequest {
        public required string SearchableName { get; set; }
        public string? Name { get; set; }
        public string? FixturesType { get; set; }
        public string? FinalsType { get; set; }
    }


    [HttpPost("update")]
    [Authorize(Policy = Policies.IsAdmin)]
    public async Task<ActionResult> UpdateTournament([FromBody] UpdateTournamentRequest request) {
        var db = new HandballContext();

        if (!Utilities.TournamentOrElse(db, request.SearchableName, out var tournament)) {
            return NotFound(new InvalidTournament($"The Tournament {request.SearchableName} does not exist"));
        }

        if (request.Name != null) {
            tournament.Name = request.Name;
        }

        if (request.FixturesType != null) {
            tournament.FixturesType = request.FixturesType;
        }

        if (request.FinalsType != null) {
            tournament.FinalsType = request.FinalsType;
        }

        await db.SaveChangesAsync();

        return Ok();
    }
}