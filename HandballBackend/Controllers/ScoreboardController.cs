using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HandballBackend.Database;
using HandballBackend.Database.Models;
using HandballBackend.EndpointHelpers;
using HandballBackend.ErrorTypes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HandballBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScoreboardController(ISocketService socketManager) : ControllerBase {
    [HttpGet]
    public async Task<IActionResult> GetScoreboardSocket(int gameId) {
        if (!HttpContext.WebSockets.IsWebSocketRequest) {
            return BadRequest(new ActionNotAllowed("This is not a WebSocket request"));
        }

        using var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();

        socketManager.AddSocket(gameId, ws);

        await socketManager.ManageReceive(ws, gameId);
        return new EmptyResult();
    }
}