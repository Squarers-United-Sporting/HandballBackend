using System.Security.Claims;
using HandballBackend.EndpointHelpers;
using Microsoft.AspNetCore.Authorization;

namespace HandballBackend.Authentication;

public static class Policies {
    public const string IsAdmin = nameof(IsAdmin);
    public const string IsUmpire = nameof(IsAdmin);
    public const string IsUmpireManager = nameof(IsAdmin);
    public const string IsTournamentDirector = nameof(IsAdmin);

    public static void RegisterPolicies(AuthorizationOptions options) {
        options.AddPolicy(IsAdmin, policy => policy
            .RequireAssertion(c =>
                c.User.HasClaim(c =>
                    c is {Type: ClaimTypes.Role, Value: nameof(PermissionType.Admin)})));

        options.AddPolicy(IsUmpireManager, policy => policy
            .RequireAssertion(c =>
                c.User.HasClaim(c =>
                    c is {Type: ClaimTypes.Role, Value: nameof(PermissionType.UmpireManager)})));

        options.AddPolicy(IsTournamentDirector, policy => policy
            .RequireAssertion(c =>
                c.User.HasClaim(c =>
                    c is {Type: ClaimTypes.Role, Value: nameof(PermissionType.TournamentDirector)})));
    }
}