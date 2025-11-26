using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HandballBackend.Database;
using HandballBackend.Database.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HandballBackend.EndpointHelpers;

public interface ISocketService {
    void AddSocket(int gameId, WebSocket socket);

    Task SendGame(int gameId);

    Task SendGameUpdate(int gameId, GameEvent e);

    Task ManageReceive(WebSocket socket, int gameId);
}

public class SocketService(IOptions<JsonOptions> jsonOptions, HandballContext db) : ISocketService {

    public static readonly Dictionary<int, List<WebSocket>> Sockets = new();

    public void AddSocket(int gameId, WebSocket socket) {
        if (Sockets.TryGetValue(gameId, out var list)) {
            list.Add(socket);
        } else {
            Sockets.Add(gameId, [socket]);
        }
    }

    private async Task SendAsync(WebSocket socket, object message) {
        var serializedMessage = JsonSerializer.Serialize(message, options: jsonOptions.Value.JsonSerializerOptions);
        await SendAsync(socket, serializedMessage);
    }

    private async Task SendAsync(WebSocket socket, string message) {
        var bytes = Encoding.UTF8.GetBytes(message);
        var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
        await socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task ManageReceive(WebSocket socket, int gameId) {
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

    private async Task SocketSendEvent(WebSocket socket, GameEvent e) {
        await SendAsync(socket, new { type = "event", Event = e.ToSendableData() });
    }

    private async Task SocketSendUpdate(WebSocket socket, int gameId) {
        var game = db.Games
            .IncludeRelevant()
            .Include(g => g.Events)
            .Include(g => g.Players)
            .ThenInclude(pgs => pgs.Player).Single(g => g.GameNumber == gameId);
        await SendAsync(socket,
            new { type = "update", game = game.ToSendableData(true, true, formatData: true) });
    }

    public async Task SendGame(int gameId) {
        if (!Sockets.TryGetValue(gameId, out var sockets)) return;
        var tasks = sockets.Select(ws => SocketSendUpdate(ws, gameId)).ToList();
        await Task.WhenAll(tasks);
    }

    public async Task SendGameUpdate(int gameId, GameEvent e) {
        if (!Sockets.TryGetValue(gameId, out var sockets)) return;
        var tasks = sockets.Select(ws => SocketSendEvent(ws, e)).ToList();
        await Task.WhenAll(tasks);
    }

}