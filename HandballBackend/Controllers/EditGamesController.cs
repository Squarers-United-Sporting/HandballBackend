using HandballBackend.Authentication;
using HandballBackend.Database;
using HandballBackend.Database.SendableTypes;
using HandballBackend.EndpointHelpers;
using HandballBackend.EndpointHelpers.GameManagement;
using HandballBackend.ErrorTypes;
using HandballBackend.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HandballBackend.Controllers;

[Authorize(Policy = Policies.IsUmpire)]
[ApiController]
[Route("api/games/update")]
public class EditGamesController(HandballContext db, IGameManagementService gameManager) : ControllerBase {
    [HttpPost("create")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateGame([FromBody] CreateRequest create) {
        if (!Utilities.TournamentOrElse(db, create.Tournament, out var tournament))
            return NotFound(new InvalidTournament(create.Tournament));

        var official = db.Officials.FirstOrDefault(o => o.Person.SearchableName == create.Official);
        var scorer = db.Officials.FirstOrDefault(o => o.Person.SearchableName == create.Scorer);
        if (official == null) return NotFound(new DoesNotExist("Official", create.Official));

        var g = await gameManager.CreateGame(
            tournament!.Id,
            create.PlayersOne,
            create.PlayersTwo,
            create.TeamOne,
            create.TeamTwo,
            create.BlitzGame,
            official.Id,
            scorer?.Id ?? -1
        );

        return Created(Config.MY_ADDRESS + $"/api/games/{g.GameNumber}", new CreateResponse {
            Game = g.ToSendableData()
        });
    }



    [TournamentSpecific("Id", true)]
    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> StartGame(
        [FromBody] StartRequest startRequest
    ) {
        await gameManager.StartGame(startRequest.Id, startRequest.SwapService, startRequest.TeamOne,
            startRequest.TeamTwo,
            startRequest.TeamOneIga, startRequest.TeamOneLibero, startRequest.TeamTwoLibero, startRequest.Official,
            startRequest.Scorer);
        return NoContent();
    }

    [HttpPost("score")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ScorePointForGame([FromBody] ScorePointRequest scorePointRequest) {
        if (!string.IsNullOrEmpty(scorePointRequest.PlayerSearchable))
            await gameManager.ScorePoint(scorePointRequest.Id, scorePointRequest.FirstTeam,
                scorePointRequest.PlayerSearchable, scorePointRequest.Method, scorePointRequest.Location);
        else if (scorePointRequest.LeftPlayer.HasValue)
            await gameManager.ScorePoint(scorePointRequest.Id, scorePointRequest.FirstTeam,
                scorePointRequest.LeftPlayer.Value, scorePointRequest.Method, scorePointRequest.Location);
        else
            return BadRequest(new MustProvideArgument(nameof(scorePointRequest.LeftPlayer),
                nameof(scorePointRequest.PlayerSearchable)));

        return NoContent();
    }

    [HttpPost("merit")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MeritForGame([FromBody] MeritRequest meritRequest) {
        if (!string.IsNullOrEmpty(meritRequest.PlayerSearchable))
            await gameManager.Merit(meritRequest.Id, meritRequest.FirstTeam,
                meritRequest.PlayerSearchable, meritRequest.Reason);
        else if (meritRequest.LeftPlayer.HasValue)
            await gameManager.Merit(meritRequest.Id, meritRequest.FirstTeam,
                meritRequest.LeftPlayer.Value, meritRequest.Reason);
        else
            return BadRequest(new MustProvideArgument(nameof(meritRequest.LeftPlayer),
                nameof(meritRequest.PlayerSearchable)));

        return NoContent();
    }

    [HttpPost("demerit")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DemeritForGame([FromBody] DemeritRequest demeritRequest) {
        if (!string.IsNullOrEmpty(demeritRequest.PlayerSearchable))
            await gameManager.Demerit(demeritRequest.Id, demeritRequest.FirstTeam,
                demeritRequest.PlayerSearchable, demeritRequest.Reason);
        else if (demeritRequest.LeftPlayer.HasValue)
            await gameManager.Demerit(demeritRequest.Id, demeritRequest.FirstTeam,
                demeritRequest.LeftPlayer.Value, demeritRequest.Reason);
        else
            return BadRequest(new MustProvideArgument(nameof(demeritRequest.LeftPlayer),
                nameof(demeritRequest.PlayerSearchable)));

        return NoContent();
    }

    [HttpPost("card")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CardForGame([FromBody] CardRequest cardRequest) {
        if (!string.IsNullOrEmpty(cardRequest.PlayerSearchable))
            await gameManager.Card(cardRequest.Id, cardRequest.FirstTeam, cardRequest.PlayerSearchable,
                cardRequest.Color,
                cardRequest.Duration, cardRequest.Reason ?? "Not Provided");
        else if (cardRequest.LeftPlayer.HasValue)
            await gameManager.Card(cardRequest.Id, cardRequest.FirstTeam, cardRequest.LeftPlayer.Value,
                cardRequest.Color,
                cardRequest.Duration, cardRequest.Reason ?? "Not Provided");
        else
            return BadRequest(new MustProvideArgument(nameof(cardRequest.LeftPlayer),
                nameof(cardRequest.PlayerSearchable)));

        return NoContent();
    }

    [HttpPost("ace")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AceForGame([FromBody] AceRequest aceRequest) {
        await gameManager.Ace(aceRequest.Id, aceRequest.Location);
        return NoContent();
    }

    [HttpPost("fault")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> FaultForGame([FromBody] FaultRequest faultRequest) {
        await gameManager.Fault(faultRequest.Id, faultRequest.Method);
        return NoContent();
    }

    [HttpPost("timeout")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> TimeoutForGame([FromBody] TimeoutRequest timeoutRequest) {
        await gameManager.Timeout(timeoutRequest.Id, timeoutRequest.FirstTeam);
        return NoContent();
    }

    [HttpPost("forfeit")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForfeitGame([FromBody] ForfeitRequest forfeitRequest) {
        await gameManager.Forfeit(forfeitRequest.Id, forfeitRequest.FirstTeam);
        return NoContent();
    }

    [HttpPost("abandon")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AbandonGame([FromBody] AbandonRequest forfeitRequest) {
        await gameManager.Abandon(forfeitRequest.Id);
        return NoContent();
    }

    [HttpPost("endTimeout")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EndTimeoutForGame([FromBody] EndTimeoutRequest endTimeoutRequest) {
        await gameManager.EndTimeout(endTimeoutRequest.Id);
        return NoContent();
    }

    [HttpPost("substitute")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SubstituteForGame([FromBody] SubstituteRequest substituteRequest) {
        if (!string.IsNullOrEmpty(substituteRequest.PlayerSearchable))
            await gameManager.Substitute(substituteRequest.Id, substituteRequest.FirstTeam,
                substituteRequest.PlayerSearchable);
        else if (substituteRequest.LeftPlayer.HasValue)
            await gameManager.Substitute(substituteRequest.Id, substituteRequest.FirstTeam,
                substituteRequest.LeftPlayer.Value);
        else
            return BadRequest(new MustProvideArgument(nameof(substituteRequest.LeftPlayer),
                nameof(substituteRequest.PlayerSearchable)));

        return NoContent();
    }

    [HttpPost("undo")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UndoForGame([FromBody] UndoRequest undoRequest) {
        await gameManager.Undo(undoRequest.Id);
        return NoContent();
    }

    [HttpPost("delete")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteGame([FromBody] DeleteRequest deleteRequest) {
        await gameManager.Delete(deleteRequest.Id);
        return NoContent();
    }

    [HttpPost("end")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EndGame([FromBody] EndGameRequest endGameRequest) {
        await gameManager.End(
            endGameRequest.Id,
            endGameRequest.Votes,
            endGameRequest.TeamOneRating,
            endGameRequest.TeamTwoRating,
            endGameRequest.Notes,
            endGameRequest.ProtestReasonTeamOne,
            endGameRequest.ProtestReasonTeamTwo,
            endGameRequest.NotesTeamOne,
            endGameRequest.NotesTeamTwo,
            endGameRequest.MarkedForReview
        );

        return NoContent();
    }

    [HttpPost("alert")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [Authorize(Policy = Policies.IsUmpireManager)]
    public IActionResult AlertGame([FromBody] AlertRequest alertRequest) {
        var game = db.Games.IncludeRelevant().First(g => alertRequest.Id == g.GameNumber);
        _ = TextHelper.TextPeopleForGame(game);
        return NoContent();
    }

    [HttpPost("resolve")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [Authorize(Policy = Policies.IsUmpireManager)]
    public async Task<IActionResult> ResolveGame([FromBody] ResolveRequest resolveRequest) {
        await gameManager.Resolve(resolveRequest.Id);
        return NoContent();
    }

    [HttpPost("replay")]
    [TournamentSpecific("Id", true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReplayForGame([FromBody] ReplayRequest replayRequest) {
        await gameManager.Replay(replayRequest.Id);
        return NoContent();
    }

    public class CreateRequest {
        public required string Tournament { get; init; }
        public string? TeamOne { get; set; } = null;
        public string? TeamTwo { get; set; } = null;
        public string[]? PlayersOne { get; set; } = null;
        public string[]? PlayersTwo { get; set; } = null;
        public required string Official { get; set; }
        public string? Scorer { get; set; } = null;

        public bool BlitzGame { get; set; } = false;
    }

    public class CreateResponse {
        public required GameData Game { get; set; }
    }

    public class StartRequest {
        public required int Id { get; set; }
        public required bool SwapService { get; set; }
        public required string[] TeamOne { get; set; }
        public required string[] TeamTwo { get; set; }
        public required bool TeamOneIga { get; set; }
        public string? TeamOneLibero { get; set; }
        public string? TeamTwoLibero { get; set; }

        public string? Official { get; set; } = null;
        public string? Scorer { get; set; } = null;
    }
    public class ScorePointRequest {
        public required int Id { get; set; }
        public required bool FirstTeam { get; set; }
        public bool? LeftPlayer { get; set; }
        public string? PlayerSearchable { get; set; }
        public string? Method { get; set; }
        public string[]? Location { get; set; }
    }

    public class MeritRequest {
        public required int Id { get; set; }
        public required bool FirstTeam { get; set; }
        public bool? LeftPlayer { get; set; }
        public string? PlayerSearchable { get; set; }
        public string? Reason { get; set; }
    }

    public class DemeritRequest {
        public required int Id { get; set; }
        public required bool FirstTeam { get; set; }
        public bool? LeftPlayer { get; set; }
        public string? PlayerSearchable { get; set; }
        public string? Reason { get; set; }
    }

    public class CardRequest {
        public required int Id { get; set; }
        public required bool FirstTeam { get; set; }
        public bool? LeftPlayer { get; set; }
        public string? PlayerSearchable { get; set; }
        public required string Color { get; set; }
        public string? Reason { get; set; }
        public int Duration { get; set; }
    }

    public class AceRequest {
        public required int Id { get; set; }
        public required string[]? Location { get; set; }
    }

    public class FaultRequest {
        public required int Id { get; set; }
        public string? Method { get; set; }
    }

    public class TimeoutRequest {
        public required int Id { get; set; }
        public required bool FirstTeam { get; set; }
    }

    public class ForfeitRequest {
        public required int Id { get; set; }
        public required bool FirstTeam { get; set; }
    }

    public class AbandonRequest {
        public required int Id { get; set; }
    }

    public class EndTimeoutRequest {
        public required int Id { get; set; }
    }

    public class SubstituteRequest {
        public required int Id { get; set; }
        public required bool FirstTeam { get; set; }
        public string? PlayerSearchable { get; set; }
        public bool? LeftPlayer { get; set; }
    }

    public class UndoRequest {
        public required int Id { get; set; }
    }

    public class DeleteRequest {
        public required int Id { get; set; }
    }

    public class EndGameRequest {
        public int Id { get; set; }
        public required List<string> Votes { get; set; }
        public int TeamOneRating { get; set; }
        public int TeamTwoRating { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string? ProtestReasonTeamOne { get; set; }
        public string? ProtestReasonTeamTwo { get; set; }
        public string NotesTeamOne { get; set; } = string.Empty;
        public string NotesTeamTwo { get; set; } = string.Empty;
        public bool MarkedForReview { get; set; }
    }

    public class AlertRequest {
        public required int Id { get; set; }
    }

    public class ResolveRequest {
        public required int Id { get; set; }
    }

    public class ReplayRequest {
        public required int Id { get; set; }
    }
}