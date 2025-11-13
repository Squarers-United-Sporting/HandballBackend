using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HandballBackend.Database;
using HandballBackend.Database.Models;
using HandballBackend.ErrorTypes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HandballBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScoreboardController : ControllerBase {
    public static readonly Dictionary<int, List<WebSocket>> Sockets = new();
    private static IOptions<JsonOptions> _jsonOptions = null!;

    public ScoreboardController(IOptions<JsonOptions> jsonOptions) {
        _jsonOptions = jsonOptions;
    }


    private static async Task SendAsync(WebSocket socket, object message) {
        var serializedMessage = JsonSerializer.Serialize(message, options: _jsonOptions.Value.JsonSerializerOptions);
        await SendAsync(socket, serializedMessage);
    }

    private static async Task SendAsync(WebSocket socket, string message) {
        var bytes = Encoding.UTF8.GetBytes(message);
        var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
        await socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task ManageReceive(WebSocket socket, int gameId) {
        var buffer = new ArraySegment<byte>(new byte[4 * 1024]);
        while (socket.State == WebSocketState.Open) {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType != WebSocketMessageType.Text) continue;
            var message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
            switch (message) {
                case "update":
                    await SocketSendUpdate(socket, gameId);
                    break;
            }
        }

        Sockets[gameId].Remove(socket);
    }

    private static async Task SocketSendEvent(WebSocket socket, GameEvent e) {
        await SendAsync(socket, new { type = "event", Event = e.ToSendableData() });
    }

    private static async Task SocketSendUpdate(WebSocket socket, int gameId) {
        await using var db = new HandballContext();
        var game = db.Games
            .IncludeRelevant()
            .Include(g => g.Events)
            .Include(g => g.Players)
            .ThenInclude(pgs => pgs.Player).Single(g => g.GameNumber == gameId);
        await SendAsync(socket,
            new { type = "update", game = game.ToSendableData(true, true, formatData: true) });
    }

    public static async Task SendGame(int gameId) {
        if (!Sockets.TryGetValue(gameId, out var sockets)) return;
        var tasks = sockets.Select(ws => SocketSendUpdate(ws, gameId)).ToList();
        await Task.WhenAll(tasks);
    }

    public static async Task SendGameUpdate(int gameId, GameEvent e) {
        if (!Sockets.TryGetValue(gameId, out var sockets)) return;
        var tasks = sockets.Select(ws => SocketSendEvent(ws, e)).ToList();
        await Task.WhenAll(tasks);
    }

    [HttpGet]
    public async Task<IActionResult> GetScoreboardSocket(int gameId) {
        if (!HttpContext.WebSockets.IsWebSocketRequest) {
            return BadRequest(new ActionNotAllowed("This is not a WebSocket request"));
        }

        using var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();
        if (Sockets.TryGetValue(gameId, out var list)) {
            list.Add(ws);
        } else {
            Sockets.Add(gameId, [ws]);
        }

        await ManageReceive(ws, gameId);
        return new EmptyResult();
    }
}