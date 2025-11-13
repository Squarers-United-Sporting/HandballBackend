using HandballBackend;
using HandballBackend.Authentication;
using HandballBackend.EndpointHelpers;
using HandballBackend.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class TournamentAuthorizeAttribute : Attribute, IAuthorizationFilter {
    private readonly PermissionType _requiredRole;
    private readonly string _routeKey;

    public TournamentAuthorizeAttribute(PermissionType requiredRole,
        string routeKey = "searchable") {
        _requiredRole = requiredRole;
        _routeKey = routeKey;
    }

    public void OnAuthorization(AuthorizationFilterContext context) {
        using var db = new HandballContext();
        var permission = ServiceLocator.Get<ICustomPermissionService>();
        var token = context.HttpContext.User.Claims.First(c => c.Type == CustomClaimTypes.Token).Value;
        var person = permission.PersonByToken(token);

        if (person is null) {
            context.Result = new UnauthorizedResult();
            return;
        }

        var tournamentSearch = context.HttpContext.Request.Query["tournament"].FirstOrDefault();
        if (tournamentSearch is null) {
            if (!context.RouteData.Values.TryGetValue(_routeKey, out var rawSearchable) ||
                rawSearchable is not string searchable) {
                //this is not a tournament-specific request
                if (person.PermissionLevel.ToInt() < _requiredRole.ToInt()) {
                    context.Result = new ForbidResult();
                }

                return;
            } else {
                tournamentSearch = searchable;
            }
        }

        if (!Utilities.TournamentOrElse(db, tournamentSearch, out var tournament)) {
            context.Result = new BadRequestObjectResult("Invalid tournament");
            return;
        }

        if (person.PermissionLevel >= PermissionType.Admin) {
            return;
        }

        var to = db.TournamentOfficials.First(to =>
            to.TournamentId == tournament!.Id &&
            to.Official.PersonId == person.Id);

        if (to.Role.ToInt() < _requiredRole.ToInt()) {
            context.Result = new ForbidResult();
        }
    }
}