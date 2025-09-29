using HandballBackend.Authentication;
using HandballBackend.Utils;
using HandballBackend.Database;
using HandballBackend.Database.Models;
using HandballBackend.Database.SendableTypes;
using HandballBackend.EndpointHelpers;
using HandballBackend.ErrorTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase {
    public record GetTeamResponse {
        public required TeamData Team { get; set; }
        public TournamentData? Tournament { get; set; }
    }

    [HttpGet("{searchable}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GetTeamResponse>> GetOneTeam(
        string searchable,
        [FromQuery(Name = "tournament")] string? tournamentSearchable = null,
        [FromQuery] bool formatData = false,
        [FromQuery] bool returnTournament = false) {
        var db = new HandballContext();

        if (!Utilities.TournamentOrElse(db, tournamentSearchable, out var tournament)) {
            return NotFound(new InvalidTournament(tournamentSearchable));
        }

        TeamData? teamData;
        if (tournament == null) {
            teamData = await db.Teams
                .Where(t => t.SearchableName == searchable)
                .IncludeRelevant()
                .Include(t => t.PlayerGameStats)
                .ThenInclude(pgs => pgs.Game)
                .Select(t => t.ToSendableData(true, true, formatData, null))
                .FirstOrDefaultAsync();
        } else {
            teamData = await db.TournamentTeams
                .Where(t => t.Team.SearchableName == searchable && t.TournamentId == tournament.Id)
                .IncludeRelevant()
                .Include(t => t.Team.PlayerGameStats.Where(pgs => pgs.TournamentId == tournament.Id))
                .ThenInclude(pgs => pgs.Game)
                .Select(tt => tt.ToSendableData(true, true, formatData))
                .FirstOrDefaultAsync();
        }

        if (teamData is null) {
            return NotFound(new DoesNotExist("Team", searchable));
        }

        if (returnTournament && tournament is null) {
            return BadRequest(new TournamentNotProvidedForReturn());
        }


        return new GetTeamResponse {
            Team = teamData,
            Tournament = returnTournament ? tournament!.ToSendableData() : null
        };
    }

    public record GetTeamsResponse {
        public required TeamData[] Teams { get; set; }
        public TournamentData? Tournament { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<GetTeamsResponse>> GetManyTeams(
        [FromQuery(Name = "tournament")] string? tournamentSearchable = null,
        [FromQuery] List<string>? player = null,
        [FromQuery] bool includeStats = false,
        [FromQuery] bool includePlayerStats = false,
        [FromQuery] bool formatData = false,
        [FromQuery] bool returnTournament = false) {
        var db = new HandballContext();

        if (!Utilities.TournamentOrElse(db, tournamentSearchable, out var tournament)) {
            return NotFound(new InvalidTournament(tournamentSearchable));
        }

        TeamData[] teamData;
        if (tournament is not null) {
            IQueryable<TournamentTeam> query = db.TournamentTeams
                .Where(t => t.TournamentId == tournament.Id)
                .Include(t => t.Team.Captain)
                .Include(t => t.Team.NonCaptain)
                .Include(t => t.Team.Substitute);
            if (includeStats) {
                query = query
                    .Include(t => t.Team.PlayerGameStats.Where(pgs => pgs.TournamentId == tournament.Id))
                    .ThenInclude(pgs => pgs.Game);
            }


            if (player != null) {
                query = query.Where(t =>
                    (t.Team.Captain != null && player.Contains(t.Team.Captain.SearchableName)) ||
                    (t.Team.NonCaptain != null && player.Contains(t.Team.NonCaptain.SearchableName)) ||
                    (t.Team.Substitute != null && player.Contains(t.Team.Substitute.SearchableName))
                );
            }

            teamData = await query.OrderBy(t => EF.Functions.Like(t.Team.SearchableName, "solo_%"))
                .ThenBy(t => !EF.Functions.Like(t.Team.ImageUrl, "/api/%"))
                .ThenBy(t => t.Team.SearchableName)
                .Select(t => t.ToSendableData(includeStats, includePlayerStats, formatData)).ToArrayAsync();
        } else {
            //Not null captain removes bye team
            var query = db.Teams.IncludeRelevant();

            if (includeStats) {
                query = query
                    .Include(t => t.PlayerGameStats)
                    .ThenInclude(pgs => pgs.Game);
            }

            query = query.Where(t => t.Captain != null);
            if (player != null) {
                foreach (var p in player) {
                    query = query.Where(t =>
                        t.Captain != null && p == t.Captain.SearchableName ||
                        t.NonCaptain != null && p == t.NonCaptain.SearchableName ||
                        t.Substitute != null && p == t.Substitute.SearchableName
                    );
                }
            }

            teamData = await query.OrderByDescending(t => t.TournamentTeams.Any(tt => tt.TournamentId != 1))
                .ThenBy(t => EF.Functions.Like(t.SearchableName, "solo_%"))
                .ThenBy(t => t.SearchableName)
                .Select(t => t.ToSendableData(includeStats, includePlayerStats, formatData, null)).ToArrayAsync();
        }

        if (returnTournament && tournament is null) {
            return BadRequest(new TournamentNotProvidedForReturn());
        }


        return new GetTeamsResponse {
            Teams = teamData,
            Tournament = returnTournament ? tournament!.ToSendableData() : null
        };
    }


    public record GetLadderResponse {
        public TeamData[]? Ladder { get; set; }
        public TeamData[]? PoolOne { get; set; }
        public TeamData[]? PoolTwo { get; set; }
        public bool Pooled { get; set; }
        public TournamentData? Tournament { get; set; }
    }

    [HttpGet("ladder")]
    public async Task<ActionResult<GetLadderResponse>> GetLadder(
        [FromQuery(Name = "tournament")] string? tournamentSearchable = null,
        [FromQuery] bool formatData = false,
        [FromQuery] bool returnTournament = false) {
        var db = new HandballContext();

        TeamData[]? ladder;
        TeamData[]? poolOne = null;
        TeamData[]? poolTwo = null;
        if (!Utilities.TournamentOrElse(db, tournamentSearchable, out var tournament)) {
            return NotFound(new InvalidTournament(tournamentSearchable));
        }

        if (tournament is not null) {
            (ladder, poolOne, poolTwo) = await LadderHelper.GetTournamentLadder(db, tournament);
            if (tournament.Editable) {
                ladder = ladder?.Where(t => t.Stats!["Games Played"] > 0).ToArray();
                poolOne = poolOne?.Where(t => t.Stats!["Games Played"] > 0).ToArray();
                poolTwo = poolTwo?.Where(t => t.Stats!["Games Played"] > 0).ToArray();
            }
        } else {
            //Not null captain removes bye team
            ladder = await LadderHelper.GetLadder(db);
            ladder = ladder.Where(t => t.Stats!["Games Played"] > 0).ToArray();
        }


        if (formatData) {
            if (ladder is not null) {
                foreach (var team in ladder) {
                    team.FormatData();
                }
            }

            if (poolOne is not null) {
                foreach (var team in poolOne) {
                    team.FormatData();
                }
            }

            if (poolTwo is not null) {
                foreach (var team in poolTwo) {
                    team.FormatData();
                }
            }
        }

        if (returnTournament && tournament is null) {
            return BadRequest(new TournamentNotProvidedForReturn());
        }


        return new GetLadderResponse {
            Ladder = ladder,
            PoolOne = poolOne,
            PoolTwo = poolTwo,
            Pooled = poolOne is not null,
            Tournament = returnTournament ? tournament!.ToSendableData() : null
        };
    }


    public class AddTeamRequest {
        public required string TournamentSearchableName { get; set; }
        public string? TeamName { get; set; }
        public string? CaptainName { get; set; }
        public string? NonCaptainName { get; set; }
        public string? SubstituteName { get; set; }
    }


    public class AddTeamResponse {
        public required TeamData Team { get; set; }
    }

    [HttpPost("addToTournament")]
    [Authorize(Policy = Policies.IsAdmin)]
    public async Task<ActionResult<AddTeamResponse>> AddTeamToTournament(
        [FromBody] AddTeamRequest request) {
        var db = new HandballContext();
        var tournament = db.Tournaments
            .FirstOrDefault(a => a.SearchableName == request.TournamentSearchableName);
        if (tournament is null) {
            return NotFound("Invalid Tournament");
        }

        if (tournament.Started) {
            return NotFound("Tournament has already started!");
        }

        var team = await db.Teams.IncludeRelevant().Include(team => team.TournamentTeams)
            .FirstOrDefaultAsync(t => t.Name == request.TeamName);
        if (team is not null && (request.CaptainName is not null || request.NonCaptainName is not null ||
                                 request.SubstituteName is not null)) {
            return BadRequest("This Team already exists; you may not provide players");
        }


        if (team is null) {
            var playerIds = ((string?[]) [request.CaptainName, request.NonCaptainName, request.SubstituteName])
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(a => db.People.FirstOrDefault(p => p.Name == a)?.Id)
                .ToList();
            while (playerIds.Count < 3) {
                playerIds.Add(null);
            }

            var maybeTeam = db.Teams.IncludeRelevant().Include(t => t.TournamentTeams).FirstOrDefault(t =>
                // Both players must be in one of the roles
                (playerIds.Contains(t.CaptainId ?? null) &&
                 playerIds.Contains(t.NonCaptainId ?? null) &&
                 playerIds.Contains(t.SubstituteId ?? null)) &&

                // Count of non-null player references should be exactly 2
                ((t.CaptainId.HasValue ? 1 : 0) +
                    (t.NonCaptainId.HasValue ? 1 : 0) +
                    (t.SubstituteId.HasValue ? 1 : 0) == playerIds.Count(a => a.HasValue))
            );
            if (maybeTeam == null) {
                team = new Team {
                    CaptainId = playerIds![0],
                    NonCaptainId = playerIds[1],
                    SubstituteId = playerIds[2],
                    Name = request.TeamName!,
                    SearchableName = Utilities.ToSearchable(request.TeamName!)
                };
                await db.Teams.AddAsync(team);
                await db.SaveChangesAsync();
            } else {
                team = maybeTeam;
            }
        }

        if (team.TournamentTeams.Any(tt => tt.TournamentId == tournament.Id)) {
            return BadRequest("That team is already in this tournament!");
        }

        await db.TournamentTeams.AddAsync(new TournamentTeam {
            TournamentId = tournament.Id,
            TeamId = team.Id,
            Name = request.TeamName == null || request.TeamName == team.Name ? null : request.TeamName,
        });


        await db.SaveChangesAsync();
        return Ok(new AddTeamResponse {
            Team = team.ToSendableData()
        });
    }


    public class UpdateTeamRequest {
        public required string TournamentSearchableName { get; set; }
        public required string TeamSearchableName { get; set; }
        public string? NewName { get; set; }
        public string? NewColor { get; set; }
    }

    public class UpdateTeamResponse {
        public required TeamData Team { get; set; }
    }

    [HttpPatch("updateForTournament")]
    [TournamentAuthorize(PermissionType.UmpireManager)]
    public async Task<ActionResult<UpdateTeamResponse>> UpdateTeamForTournament(
        [FromBody] UpdateTeamRequest request) {
        var db = new HandballContext();
        var tournament = await db.Tournaments
            .FirstOrDefaultAsync(a => a.SearchableName == request.TournamentSearchableName);
        if (tournament is null) {
            return NotFound("Invalid Tournament");
        }

        if (tournament.Started) {
            return NotFound("Tournament has already started!");
        }

        var team = await db.Teams.IncludeRelevant().Include(team => team.TournamentTeams)
            .SingleAsync(team => team.SearchableName == request.TeamSearchableName);

        if (team.TournamentTeams.All(tt => tt.TournamentId != tournament.Id)) {
            return BadRequest("Team not in tournament!");
        }

        var tournamentTeam = team.TournamentTeams.Single(tt => tt.TournamentId == tournament.Id);
        if (team.TournamentTeams.Count(tt => tt.Id != 1) == 1) {
            if (request.NewName != null) {
                team.Name = request.NewName;
                team.SearchableName = Utilities.ToSearchable(request.NewName);
            }

            if (request.NewColor != null) {
                team.TeamColor = request.NewColor;
            }
        } else {
            if (request.NewName != null) {
                tournamentTeam.Name = request.NewName;
            }

            if (request.NewColor != null) {
                tournamentTeam.TeamColor = request.NewColor;
            }
        }


        await db.SaveChangesAsync();
        return Ok(new UpdateTeamResponse {
            Team = tournamentTeam.ToSendableData()
        });
    }

    public class RemoveTeamRequest {
        public string? TeamSearchableName { get; set; }
        public string? TournamentSearchableName { get; set; }
    }

    [HttpDelete("removeFromTournament")]
    [TournamentAuthorize(PermissionType.UmpireManager)]
    public async Task<ActionResult> RemoveTeamFromTournament([FromBody] RemoveTeamRequest request) {
        var db = new HandballContext();
        var tournament = await db.Tournaments
            .FirstOrDefaultAsync(a => a.SearchableName == request.TournamentSearchableName);
        if (tournament is null) {
            return NotFound("Invalid Tournament");
        }

        if (tournament.Started) {
            return NotFound("Tournament has already started!");
        }

        var team = await db.Teams.Include(team => team.TournamentTeams)
            .SingleAsync(t => t.SearchableName == request.TeamSearchableName);
        var deleteTeam = team.TournamentTeams.Count(tt => tt.TournamentId != 1) < 1;

        var tournamentTeam = team.TournamentTeams.Single(tt => tt.TournamentId == tournament.Id);
        db.TournamentTeams.Remove(tournamentTeam);
        if (deleteTeam) {
            db.Teams.Remove(team);
        }

        db.TournamentTeams.Remove(tournamentTeam);
        await db.SaveChangesAsync();


        return Ok();
    }
}