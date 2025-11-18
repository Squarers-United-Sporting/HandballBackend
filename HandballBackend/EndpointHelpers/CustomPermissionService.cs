using System.Security.Claims;
using System.Text;
using HandballBackend.Authentication;
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
    TournamentDirector,
    Admin,
}

public static class PermissionExtensions {
    public static int ToInt(this OfficialRole officialRole) {
        return officialRole switch {
            OfficialRole.Scorer or OfficialRole.Umpire => 2,
            OfficialRole.TeamLiaison or OfficialRole.UmpireManager => 3,
            OfficialRole.TournamentDirector => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(officialRole), officialRole, null)
        };
    }

    public static PermissionType IntToPermissionType(int permissionType) {
        return permissionType switch {
            0 => PermissionType.None,
            1 => PermissionType.LoggedIn,
            2 => PermissionType.Umpire,
            3 => PermissionType.UmpireManager,
            4 => PermissionType.TournamentDirector,
            5 => PermissionType.Admin,
            _ => throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null)
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
            PermissionType.TournamentDirector => 4,
            PermissionType.Admin => 5,
            _ => throw new ArgumentOutOfRangeException(nameof(permissionType), permissionType, null)
        };
    }
}

public interface ICustomPermissionService {
    bool IsAdmin();
    bool IsUmpireManager();
    bool IsUmpire();
    PermissionType GetRequestPermissions();
    Person? PersonByToken(string? token);
    void SetPassword(int personId, string password);
    Person? Login(int personId, string password, bool longSession);
    void Logout(int personId);
}

public class CustomPermissionService(HandballContext db, IHttpContextAccessor contextAccessor)
    : ICustomPermissionService {
    public bool IsAdmin() {
        return GetRequestPermissions() >= PermissionType.Admin;
    }

    public bool IsUmpireManager() {
        return GetRequestPermissions() >= PermissionType.UmpireManager;
    }


    public bool IsUmpire() {
        return GetRequestPermissions() >= PermissionType.Umpire;
    }


    public PermissionType GetRequestPermissions() {
        return contextAccessor.HttpContext?.User.Claims.Select(c =>
                       c.Type == ClaimTypes.Role ? Enum.Parse<PermissionType>(c.Value) : PermissionType.None)
                   .DefaultIfEmpty(PermissionType.None).Max() ??
               PermissionType.None;
    }

    private bool PersonOrElse(HandballContext db, int personId, out Person person) {
        person = db.People.Include(p => p.Official.TournamentOfficials)!
            .ThenInclude(to => to.Tournament)!
            .First(p => p.Id == personId);
        return person is not null;
    }

    private int Time() {
        return Utilities.GetUnixSeconds();
    }

    private string GenerateToken() {
        return Guid.NewGuid().ToString("N");
    }

    private string Encrypt(string password) {
        var salt = BCrypt.GenerateSalt(12);
        var pwd = BCrypt.HashPassword(password, salt);
        return pwd;
    }


    private bool CheckPassword(int personId, string checkPassword) {
        if (!PersonOrElse(db, personId, out var person)) {
            return false;
        }

        var realPassword = person.Password;
        if (realPassword == null) {
            throw new ArgumentNullException(nameof(personId), "The given person has no password.");
        }

        return BCrypt.Verify(checkPassword, realPassword);
    }


    private bool CheckToken(int personId, string token) {
        if (!PersonOrElse(db, personId, out var person)) {
            throw new KeyNotFoundException($"Person with id {personId} not found");
        }

        return person.SessionToken == token;
    }

    private void ResetTokenForPerson(int personId) {
        if (!PersonOrElse(db, personId, out var person)) {
            throw new KeyNotFoundException($"Person with id {personId} not found");
        }

        person.SessionToken = null;
        person.TokenTimeout = null;
        db.SaveChanges();
    }


    private string? GetToken() {
        // Access the current HTTP context
        var httpContext = contextAccessor.HttpContext;
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


    public void SetPassword(int personId, string password) {
        using var db = new HandballContext();
        if (!PersonOrElse(db, personId, out var person)) {
            throw new KeyNotFoundException($"Person with id {personId} not found");
        }

        person.Password = Encrypt(password);
        db.SaveChanges();
    }

    public Person? Login(int personId, string password, bool longSession = false) {
        var pwCheck = CheckPassword(personId, password);
        if (!pwCheck) {
            return null;
        }

        using var db = new HandballContext();
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

    public Person? PersonByToken(string? token) {
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

    public void Logout(int id) {
        ResetTokenForPerson(id);
    }
}