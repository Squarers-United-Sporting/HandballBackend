using System.Text;
using HandballBackend.Database.Models;
using HandballBackend.Utils;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.EndpointHelpers;

using BCrypt.Net;

public enum PermissionType {
    None,
    LoggedIn,
    Umpire,
    UmpireManager,
    Admin,
}

public static class PermissionHelper {
    public static int ToInt(this OfficialRole officialRole) {
        return officialRole switch {
            OfficialRole.Scorer or OfficialRole.Umpire => 2,
            OfficialRole.TeamLiaison or OfficialRole.UmpireManager => 3,
            OfficialRole.TournamentDirector => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(officialRole), officialRole, null)
        };
    }

    public static PermissionType ToPermissionType(this OfficialRole permissionType) {
        return IntToPermissionType(permissionType.ToInt());
    }

    public static int ToInt(this PermissionType permissionType) {
        return permissionType switch {
            PermissionType.None => 0,
            PermissionType.LoggedIn => 1,
            PermissionType.Umpire => 2,
            PermissionType.UmpireManager => 3,
            PermissionType.Admin => 5,
            _ => throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null)
        };
    }

    public static bool IsAdmin() {
        var person = PersonByToken(GetToken());
        if (person == null) return false;
        return person.PermissionLevel.ToInt() >= PermissionType.Admin.ToInt();
    }

    public static bool IsUmpireManager(Tournament? tournament) {
        var person = PersonByToken(GetToken());
        if (person == null) return false;
        if (IsAdmin()) return true;
        if (tournament == null) return person.PermissionLevel.ToInt() >= PermissionType.UmpireManager.ToInt();
        return person.Official!.TournamentOfficials.Any(to =>
            to.TournamentId == tournament.Id &&
            to.Role.ToInt() >= PermissionType.UmpireManager.ToInt());
    }

    public static bool IsUmpireManager(Game g) {
        var person = PersonByToken(GetToken());
        if (person == null) return false;
        if (IsAdmin()) return true;
        return person.Official!.TournamentOfficials.Any(to =>
            to.TournamentId == g.TournamentId &&
            to.Role.ToInt() >= PermissionType.UmpireManager.ToInt());
    }

    public static bool IsUmpire(Tournament? tournament) {
        var person = PersonByToken(GetToken());
        if (person == null) return false;
        if (IsAdmin()) return true;
        if (tournament == null) return person.PermissionLevel.ToInt() >= PermissionType.Umpire.ToInt();
        return person.Official!.TournamentOfficials.Any(to =>
            to.TournamentId == tournament.Id &&
            to.Role.ToInt() >= PermissionType.Umpire.ToInt());
    }

    public static bool IsUmpire(Game g) {
        var person = PersonByToken(GetToken());
        if (person == null) return false;
        if (IsAdmin()) return true;
        return person.Official!.TournamentOfficials.Any(to =>
            to.TournamentId == g.TournamentId &&
            to.Role.ToInt() >= PermissionType.Umpire.ToInt());
    }

    public static PermissionType IntToPermissionType(int permissionType) {
        return permissionType switch {
            0 => PermissionType.None,
            1 => PermissionType.LoggedIn,
            2 => PermissionType.Umpire,
            3 => PermissionType.UmpireManager,
            5 => PermissionType.Admin,
            _ => throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null)
        };
    }

    public static PermissionType GetRequestPermissions(Tournament? tournament) {
        var person = PersonByToken(GetToken());
        if (person == null) return PermissionType.None;
        if (IsAdmin()) return PermissionType.Admin;
        if (tournament == null) return person.PermissionLevel;
        return person.Official!.TournamentOfficials.First(to =>
            to.TournamentId == tournament.Id).Role.ToPermissionType();
    }

    private static bool PersonOrElse(HandballContext db, int personId, out Person person) {
        person = db.People.Include(p => p.Official.TournamentOfficials)!
            .ThenInclude(to => to.Tournament)!
            .First(p => p.Id == personId);
        return person is not null;
    }

    private static int Time() {
        return Utilities.GetUnixSeconds();
    }

    private static string GenerateToken() {
        return Guid.NewGuid().ToString("N");
    }

    private static string Encrypt(string password) {
        var salt = BCrypt.GenerateSalt(12);
        var pwd = BCrypt.HashPassword(password, salt);
        return pwd;
    }


    private static bool CheckPassword(int personId, string checkPassword) {
        var db = new HandballContext();
        if (!PersonOrElse(db, personId, out var person)) {
            return false;
        }

        var realPassword = person.Password;
        if (realPassword == null) {
            throw new ArgumentNullException(nameof(personId), "The given person has no password.");
        }

        return BCrypt.Verify(checkPassword, realPassword);
    }


    private static bool CheckToken(int personId, string token) {
        var db = new HandballContext();
        if (!PersonOrElse(db, personId, out var person)) {
            throw new KeyNotFoundException($"Person with id {personId} not found");
        }

        return person.SessionToken == token;
    }

    private static void ResetTokenForPerson(int personId) {
        var db = new HandballContext();
        if (!PersonOrElse(db, personId, out var person)) {
            throw new KeyNotFoundException($"Person with id {personId} not found");
        }

        person.SessionToken = null;
        person.TokenTimeout = null;
        db.SaveChanges();
    }


    private static string? GetToken() {
        // Access the current HTTP context
        var httpContext = new HttpContextAccessor().HttpContext;
        if (httpContext == null) {
            return null;
        }

        // Get the Authorization header
        var authHeader = httpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ")) {
            return null;
        }

        // Extract the token from the header
        return authHeader["Bearer ".Length..].Trim();
    }


    public static void SetPassword(int personId, string password) {
        var db = new HandballContext();
        if (!PersonOrElse(db, personId, out var person)) {
            throw new KeyNotFoundException($"Person with id {personId} not found");
        }

        person.Password = Encrypt(password);
        db.SaveChanges();
    }

    public static Person? Login(int personId, string password, bool longSession = false) {
        var pwCheck = CheckPassword(personId, password);
        if (!pwCheck) {
            return null;
        }

        var db = new HandballContext();
        if (!PersonOrElse(db, personId, out var person)) {
            return null;
        }

        if (person.SessionToken is null || person.TokenTimeout < Time() + 60 * 60) //an hour
        {
            //Our old token either didn't exist, wasn't valid or was about to expire.  New One!!

            var ret = GenerateToken();
            person.SessionToken = ret;
            if (longSession) {
                person.TokenTimeout = Time() + 60 * 60 * 24 * 7; //One week long token
            } else {
                person.TokenTimeout = Time() + 60 * 60 * 12; //Twelve hour token
            }

            db.SaveChanges();
        }

        return person;
    }

    public static Person? PersonByToken(string? token) {
        var db = new HandballContext();
        if (token == null) {
            return null;
        }

        var person = db.People.Include(p => p.Official).ThenInclude(o => o != null ? o.TournamentOfficials : null)
            .FirstOrDefault(p => p.SessionToken == token);

        if (person == null) return null;

        if (person.TokenTimeout < Time()) {
            ResetTokenForPerson(person.Id);
            return null;
        }

        return person;
    }

    public static void Logout(int id) {
        ResetTokenForPerson(id);
    }
}