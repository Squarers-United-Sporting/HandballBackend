using HandballBackend.Authentication;
using HandballBackend.Database;
using HandballBackend.Database.Models;
using HandballBackend.Database.SendableTypes;
using HandballBackend.EndpointHelpers;
using HandballBackend.ErrorTypes;
using HandballBackend.FixtureGenerator;
using HandballBackend.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class TournamentsController(HandballContext db) : ControllerBase {
    public record GetTournamentsResponse {
        public required TournamentData[] Tournaments { get; set; }
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<GetTournamentsResponse>> GetManyTournaments(
        [FromQuery] int limit = -1,
        [FromQuery] int page = -1,
        [FromQuery(Name = "player")] List<string>? players = null,
        [FromQuery(Name = "team")] List<string>? teams = null,
        [FromQuery(Name = "official")] List<string>? officials = null) {
        IQueryable<Tournament> query = db.Tournaments
            .OrderBy(t => t.Id);

        if (players is not null) {
            foreach (var p in players) {
                query = query.Where(t => t.PlayerGameStats.Any(pgs => pgs.Player.SearchableName == p));
            }
        }

        if (teams is not null) {
            foreach (var team in teams) {
                query = query.Where(t => t.Teams.Any(tt => tt.Team.SearchableName == team));
            }
        }

        if (officials is not null) {
            foreach (var p in officials) {
                query = query.Where(t => t.Officials.Any(o => o.Official.Person.SearchableName == p));
            }
        }

        if (page > 0) {
            if (limit < 0) return BadRequest(new ActionNotAllowed("Cannot pass page without passing a limit"));
            query = query.Skip(page * limit);
        }

        if (limit > 0) {
            query = query.Take(limit);
        }

        var tournaments = await query
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
    [TournamentSpecific("tournament")]
    public async Task<ActionResult<GetTournamentResponse>> GetOneTournament(string searchable) {
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
    [Authorize(Policy = Policies.IsTournamentDirector)]
    [TournamentSpecific("searchable")]
    public async Task<ActionResult> StartTournament(string searchable) {
        var tournament = await db.Tournaments
            .FirstOrDefaultAsync(a => a.SearchableName == searchable);
        if (tournament is null) {
            return NotFound("Invalid Tournament");
        }

        await tournament.BeginTournament();
        return Ok();
    }

    [HttpPost("{searchable}/finalsNextRound")]
    [TournamentSpecific("searchable")]
    [Authorize(Policy = Policies.IsTournamentDirector)]
    public async Task<ActionResult> PutTournamentInFinals(string searchable) {
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
        public required string Color { get; set; }
        public required string FixturesType { get; set; }
        public required string FinalsType { get; set; }

        public bool HasScorer { get; set; } = true;
        public bool TwoCourts { get; set; } = true;
        public bool BadmintonServes { get; set; } = true;
    }

    public class CreateTournamentResponse {
        public required TournamentData Tournament;
    }

    [HttpPost("create")]
    [Authorize(Policy = Policies.IsAdmin)]
    public async Task<ActionResult> CreateTournament([FromBody] CreateTournamentRequest request) {
        var tournament = new Tournament {
            Name = request.Name,
            SearchableName = Utilities.ToSearchable(request.Name),
            Editable = false,
            FixturesType = request.FixturesType,
            FinalsType = request.FinalsType,
            Ranked = true,
            TwoCourts = request.TwoCourts,
            HasScorer = request.HasScorer,
            Started = false,
            BadmintonServes = request.BadmintonServes,
            ImageUrl = "/api/image?name=SUSS",
            Color = request.Color,
        };
        await db.Tournaments.AddAsync(tournament);
        await db.SaveChangesAsync();
        return Created(Config.MY_ADDRESS + $"/api/tournaments/{tournament.SearchableName}", new CreateTournamentResponse {
            Tournament = tournament.ToSendableData()
        });
    }

    public class UpdateTournamentRequest {
        public required string Tournament { get; set; }
        public string? Name { get; set; }
        public string? FixturesType { get; set; }
        public string? FinalsType { get; set; }
        public string? Color { get; set; }
        public bool? HasScorer { get; set; }
        public bool? TwoCourts { get; set; }
        public bool? BadmintonServes { get; set; }
    }


    [HttpPost("update")]
    [TournamentSpecific("tournament")]
    [Authorize(Policy = Policies.IsAdmin)]
    public async Task<ActionResult> UpdateTournament([FromBody] UpdateTournamentRequest request) {
        if (!Utilities.TournamentOrElse(db, request.Tournament, out var tournament)) {
            return NotFound(new InvalidTournament($"The Tournament {request.Tournament} does not exist"));
        }

        if (request.Name != null) {
            tournament!.Name = request.Name;
        }

        if (request.FixturesType != null) {
            tournament!.FixturesType = request.FixturesType;
        }

        if (request.FinalsType != null) {
            tournament!.FinalsType = request.FinalsType;
        }

        if (request.Color != null) {
            tournament!.Color = request.Color;
        }

        if (request.HasScorer != null) {
            tournament!.HasScorer = request.HasScorer.Value;
        }

        if (request.TwoCourts != null) {
            tournament!.TwoCourts = request.TwoCourts.Value;
        }

        if (request.BadmintonServes != null) {
            tournament!.BadmintonServes = request.BadmintonServes.Value;
        }

        await db.SaveChangesAsync();

        return Ok();
    }

    public class FixtureTypesResponse {
        public List<string> FixturesTypes { get; set; } = [];
        public List<string> FinalsTypes { get; set; } = [];
    }

    [HttpGet("fixtureTypes")]
    public ActionResult<FixtureTypesResponse> GetFixtureTypes() {
        return new FixtureTypesResponse {
            FixturesTypes = AbstractFixtureGenerator.GetFixtureGeneratorNames(),
            FinalsTypes = AbstractFixtureGenerator.GetFinalsGeneratorNames()
        };
    }
}