using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Web.Helpers;
using HandballBackend.Database.Models;
using HandballBackend.EndpointHelpers;
using HandballBackend.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HandballBackend.Authentication;

public class TokenAuthenticator : AuthenticationHandler<AuthenticationSchemeOptions> {
    private readonly HandballContext _db;
    private readonly ICustomPermissionService _permissions;

    public TokenAuthenticator(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, HandballContext db, ICustomPermissionService permissions) : base(options, logger, encoder) {
        _db = db;
        _permissions = permissions;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {
        if (!Request.Headers.ContainsKey("Authorization")) {
            return AuthenticateResult.NoResult();
        }

        var tournamentLocation = Request.HttpContext.GetEndpoint()?.Metadata.GetMetadata<TournamentSpecificAttribute>();
        var token = Request.Headers.Authorization.ToString().Split(" ")[1];
        var person = _permissions.PersonByToken(token);

        if (person == null || person.TokenTimeout < Utilities.GetUnixSeconds()) {
            return AuthenticateResult.Fail("Invalid Token");
        }

        var myPermission = PermissionType.None;

        if (tournamentLocation == null || person.PermissionLevel == PermissionType.Admin) {
            myPermission = person.PermissionLevel;
        } else if (tournamentLocation.IsGame) {
            int? gameId = null;
            if (Request.Query[tournamentLocation.ParameterName].FirstOrDefault() is { } stringId) {
                gameId = int.Parse(stringId);
            } else if (Request.RouteValues.TryGetValue(tournamentLocation.ParameterName, out var rawId) && rawId is int id) {
                gameId = id;
            } else {
                // only check the id if the request is smaller than 5kb (so we don't decode entire images)
                Request.EnableBuffering();
                Request.Body.Seek(0, SeekOrigin.Begin);
                StringBuilder sb = new();
                using (var reader = new HttpRequestStreamReader(Request.Body, Encoding.Default)) {
                    sb.Append(await reader.ReadToEndAsync());
                }

                var json = Json.Decode(sb.ToString());
                if (json.Id != null) {
                    gameId = json.Id! as int?;
                }
                Request.Body.Seek(0, SeekOrigin.Begin);
            }

            if (gameId.HasValue) {
                var game = await _db.Games.SingleAsync(g => g.GameNumber == gameId.Value);
                myPermission = person.Official!.TournamentOfficials.FirstOrDefault(to =>
                    to.TournamentId == game.TournamentId)?.Role.ToPermissionType() ?? PermissionType.None;
            }
        } else {
            var tournamentSearch = Request.Query[tournamentLocation.ParameterName].FirstOrDefault();
            if (tournamentSearch is null) {
                if (Request.RouteValues.TryGetValue(tournamentLocation.ParameterName, out var rawSearchable) &&
                    rawSearchable is string searchable) {
                    tournamentSearch = searchable;
                }
            }

            if (tournamentSearch != null) {
                var tournament = await _db.Tournaments.SingleAsync(t => t.SearchableName == tournamentSearch);
                myPermission = person.Official!.TournamentOfficials.FirstOrDefault(to =>
                    to.TournamentId == tournament.Id)?.Role.ToPermissionType() ?? PermissionType.None;
            }
        }


        List<Claim> claims = [
            new(CustomClaimTypes.SearchableName, person.SearchableName),
            new(ClaimTypes.Name, person.Name),
            new(CustomClaimTypes.UserId, person.Id.ToString()),
            new(CustomClaimTypes.Token, person.SessionToken!)
        ];
        claims.AddRange(Enum.GetValues<PermissionType>()
            .Where(permission => permission <= myPermission)
            .Select(permission => new Claim(ClaimTypes.Role, permission.ToString())));


        var claimsIdentity = new ClaimsIdentity(claims, Scheme.Name);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
        var ticket = new AuthenticationTicket(claimsPrincipal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}