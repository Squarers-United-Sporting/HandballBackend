using HandballBackend.Database;
using HandballBackend.Database.Models;
using HandballBackend.Database.SendableTypes;
using HandballBackend.EndpointHelpers;
using HandballBackend.ErrorTypes;
using HandballBackend.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController() : ControllerBase {
    public record ChangeCodeResponse {
        public int Code { get; set; }
    }

    [HttpGet("change_code")]
    public ActionResult<ChangeCodeResponse> GetChangeCode(
        [FromQuery(Name = "id")] int gameNumber
    ) {
        var db = new HandballContext();
        var query = db.GameEvents.Where(gE => gE.Game.GameNumber == gameNumber).OrderByDescending(gE => gE.Id)
            .Select(gE => gE.Id).FirstOrDefault();

        return new ChangeCodeResponse {
            Code = query
        };
    }

    public record GetGameResponse {
        public required GameData Game { get; set; }
    }

    [HttpGet("{gameNumber:int}")]
    public ActionResult<GetGameResponse> GetOneGame(
        int gameNumber,
        [FromQuery] bool includeGameEvents = false,
        [FromQuery] bool includeStats = false,
        [FromQuery] bool formatData = false
    ) {
        var db = new HandballContext();
        var isAdmin = HttpContext.User.IsInRole(nameof(PermissionType.UmpireManager));

        var game = db.Games
            .IncludeRelevant()
            .Include(g => g.Events)
            .Include(g => g.Players)
            .ThenInclude(pgs => pgs.Player)
            .FirstOrDefault(g => g.GameNumber == gameNumber);
        if (game is null) {
            return NotFound(new DoesNotExist("Game", gameNumber.ToString()));
        }

        var isUmpire = PermissionHelper.IsUmpire(game);
        var cards = db.GameEvents.Where(gE =>
            gE.TournamentId == game.TournamentId
            && GameEvent.CardTypes.Contains(gE.EventType)
            && gE.TeamId == game.TeamOneId || gE.TeamId == game.TeamTwoId).Include(gE => gE.Game);
        foreach (var pgs in game.Players) {
            pgs.Player.Events = cards.Where(gE => pgs.PlayerId == gE.PlayerId).ToList();
        }

        return new GetGameResponse {
            Game = game.ToSendableData(true, includeGameEvents, includeStats, formatData, isUmpire, isAdmin)
        };
    }

    public record GetGamesResponse {
        public GameData[] Games { get; set; }
        public TournamentData? Tournament { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult<GetGamesResponse>> GetManyGames(
        [FromQuery(Name = "tournament")] string? tournamentSearchable,
        [FromQuery] bool includeGameEvents = false,
        [FromQuery] bool includeByes = false,
        [FromQuery] bool returnTournament = false,
        [FromQuery] bool includeStats = false,
        [FromQuery(Name = "player")] List<string>? players = null,
        [FromQuery(Name = "team")] List<string>? teams = null,
        [FromQuery(Name = "official")] List<string>? officials = null,
        [FromQuery] int? court = null,
        [FromQuery] bool formatData = false,
        [FromQuery] int limit = -1
    ) {
        var db = new HandballContext();


        if (!Utilities.TournamentOrElse(db, tournamentSearchable, out var tournament)) {
            return NotFound(new InvalidTournament(tournamentSearchable));
        }


        var query = db.Games.IncludeRelevant().Where(g => g.GameNumber > -2);
        if (tournament is not null) {
            query = query.Where(g => g.TournamentId == tournament.Id);
        }

        var isAdmin = PermissionHelper.IsUmpireManager(tournament);
        if (!includeByes) {
            query = query.Where(g => !g.IsBye);
        }

        if (players is not null) {
            foreach (var p in players) {
                query = query.Where(g => g.Players.Select(pgs => pgs.Player.SearchableName).Contains(p));
            }
        }

        if (teams is not null) {
            foreach (var t in teams) {
                query = query.Where(g => g.TeamOne.SearchableName == t || g.TeamTwo.SearchableName == t);
            }
        }

        if (officials is not null) {
            foreach (var p in officials) {
                query = query.Where(g =>
                    (g.Official != null && g.Official.Person.SearchableName == p) ||
                    (g.Scorer != null && g.Scorer.Person.SearchableName == p));
            }
        }

        if (court is not null) {
            query = query.Where(g => g.Court == court);
        }

        query = query.OrderByDescending(g => g.Id);

        if (limit > 0) {
            query = query.Take(limit);
        }

        if (includeGameEvents) {
            query = query.Include(g => g.Events).ThenInclude(gE => gE.Player);
        } else if (isAdmin) {
            query = query.Include(x => x.Events
                    .Where(gE => gE.EventType == GameEventType.Notes ||
                                 gE.EventType == GameEventType.Protest ||
                                 gE.EventType == GameEventType.EndGame ||
                                 GameEvent.CardTypes.Contains(gE.EventType))
                )
                .ThenInclude(gE => gE.Player);
        }

        var games = await query.OrderBy(g => g.Id)
            .Select(g => g.ToSendableData(false, includeGameEvents, includeStats, formatData, isAdmin, isAdmin))
            .ToArrayAsync();

        if (returnTournament && tournament is null) {
            return BadRequest(new TournamentNotProvidedForReturn());
        }


        return new GetGamesResponse {
            Games = games,
            Tournament = returnTournament ? tournament!.ToSendableData() : null
        };
    }

    public record GetNoteableResponse {
        public required GameData[] Games { get; set; }
        public TournamentData? Tournament { get; set; }
    }

    [HttpGet("noteable")]
    [TournamentAuthorize(PermissionType.UmpireManager)]
    public async Task<ActionResult<GetNoteableResponse>> GetNoteableGames(
        [FromQuery(Name = "tournament")] string? tournamentSearchable = null,
        [FromQuery] bool includeGameEvents = false,
        [FromQuery] bool returnTournament = false,
        [FromQuery] bool formatData = false,
        [FromQuery] bool includeStats = false,
        [FromQuery] int limit = -1
    ) {
        var db = new HandballContext();

        if (!Utilities.TournamentOrElse(db, tournamentSearchable, out var tournament)) {
            return NotFound(new InvalidTournament(tournamentSearchable));
        }

        var query = db.Games.Where(g => g.GameNumber > -2).IncludeRelevant()
            .Where(g => !g.IsBye && !Game.ResolvedStatuses.Contains(g.NoteableStatus));
        if (tournament is not null) {
            query = query.Where(g => g.TournamentId == tournament.Id);
        }


        query = query.OrderByDescending(g => g.Id);

        if (limit > 0) {
            query = query.Take(limit);
        }

        query = query.Include(g => g.Events);


        var games = await query
            .Select(g => g.ToSendableData(false, includeGameEvents, includeStats, formatData, true, true))
            .ToArrayAsync();

        if (returnTournament && tournament is null) {
            return BadRequest(new TournamentNotProvidedForReturn());
        }


        return new GetNoteableResponse {
            Games = games,
            Tournament = returnTournament ? tournament!.ToSendableData() : null
        };
    }

    public record GetFixturesResponse {
        public required FixturesRound[] Fixtures { get; set; }
        public FixturesRound[]? Finals { get; set; }
        public TournamentData? Tournament { get; set; }
    }

    [HttpGet("fixtures")]
    public async Task<ActionResult<GetFixturesResponse>> GetFixtures(
        [BindRequired, FromQuery(Name = "tournament")]
        string tournamentSearchable,
        [FromQuery] bool returnTournament = false,
        [FromQuery] bool separateFinals = false,
        [FromQuery] int maxRounds = -1
    ) {
        var db = new HandballContext();
        if (!Utilities.TournamentOrElse(db, tournamentSearchable, out var tournament)) {
            return NotFound(new InvalidTournament(tournamentSearchable));
        }

        var isAdmin = PermissionHelper.IsUmpireManager(tournament);


        var query = db.Games.Where(g => g.GameNumber > -2 && g.TournamentId == tournament!.Id).IncludeRelevant()
            .OrderBy(g => g.Round);

        query = query.OrderBy(g => g.Id);


        var isUmpire = PermissionHelper.IsUmpireManager(tournament);
        var games = await query.Select(g => g.ToSendableData(false, false, false, false, isUmpire, isAdmin))
            .ToArrayAsync();

        List<FixturesRound> fixtures = [];

        foreach (var game in games) {
            var roundIndex = game.Round - 1;
            while (fixtures.Count < roundIndex) {
                // creates all before the current round
                fixtures.Add(new FixturesRound());
            }

            if (fixtures.Count == roundIndex) {
                //create the current round
                fixtures.Add(new FixturesRound([game], game.IsFinal));
            } else {
                fixtures[game.Round - 1].Games.Add(game);
            }
        }

        foreach (var round in fixtures) {
            round.Sort();
        }

        if (maxRounds > 0) {
            fixtures = fixtures.TakeLast(maxRounds).ToList();
        }

        var output = new GetFixturesResponse {
            Fixtures = separateFinals ? fixtures.Where(f => !f.Final).ToArray() : fixtures.ToArray(),
            Finals = separateFinals ? fixtures.Where(f => f.Final).ToArray() : null
        };

        if (returnTournament) {
            if (tournament is null) {
                return BadRequest(new TournamentNotProvidedForReturn());
            }

            output.Tournament = tournament.ToSendableData();
        }


        return output;
    }
}

public class FixturesRound(List<GameData>? games = null, bool isFinal = false) {
    public List<GameData> Games { get; private set; } = games ?? [];
    public bool Final { get; set; } = isFinal;

    public void Sort() {
        Games = FixturesHelper.SortFixtures(Games);
    }
}