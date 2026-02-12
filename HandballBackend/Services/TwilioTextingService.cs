using HandballBackend.Database;
using HandballBackend.Database.Models;
using Microsoft.EntityFrameworkCore;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace HandballBackend.EndpointHelpers;

public interface ITextingService {
    Task<bool> TextPeopleForGame(Game game);
    Task<bool> TextTournamentStaff(Game game);

    Task<bool> Text(Person target, string msg);
}

public class TwilioTextingService(HandballContext db) : ITextingService {
    private string UserName() {
        return Environment.GetEnvironmentVariable("TWILIO_ACCOUNT")!;
    }

    private string Key() {
        return Environment.GetEnvironmentVariable("TWILIO_KEY")!;
    }

    private bool _hasBeenSetup = false;

    private void Setup() {
        if (_hasBeenSetup) return;
        _hasBeenSetup = true;
        TwilioClient.Init(UserName(), Key());
    }

    public async Task<bool> TextPeopleForGame(Game game) {
        var tasks = new List<Task<bool>>();
        tasks.Add(Text(game.Official!.Person,
            $"You are umpiring the game between {game.TeamOne.Name} and {game.TeamTwo.Name} on court {game.Court + 1}. https://squarers.club/games/{game.GameNumber}"
        ));
        if (game.ScorerId != null && game.ScorerId != game.OfficialId) {
            tasks.Add(Text(game.Official.Person,
                $"You are scoring the game between {game.TeamOne.Name} and {game.TeamTwo.Name} on court {game.Court + 1}."));
        }

        var teams = new[] { game.TeamOne, game.TeamTwo };
        for (var j = 0; j < teams.Length; j++) {
            var team = teams[j];
            var oppTeam = teams[1 - j];
            tasks.Add(Text(team.Captain!,
                $"Your game against {oppTeam.Name} is beginning soon on court {game.Court + 1}."));
        }

        await Task.WhenAll(tasks);
        return tasks.All(t => t.Result);
    }

    public async Task<bool> TextTournamentStaff(Game game) {
        var tasks = new List<Task<bool>>();
        var tournamentOfficials =
            await db.TournamentOfficials.Where(to => to.TournamentId == game.TournamentId).IncludeRelevant()
                .ToListAsync();

        var tournamentStaff = tournamentOfficials.Where(to => to.Role >= OfficialRole.UmpireManager)
            .Select(to => to.Official.Person!).ToList();


        foreach (var tournamentDirector in tournamentStaff) {
            tasks.Add(Text(tournamentDirector,
                $"Game #{game.GameNumber} between {game.TeamOne.Name} and {game.TeamTwo.Name} has been marked for review. Status: {game.NoteableStatus}, Umpire: {game.Official?.Person.Name ?? "None"}, Scorer: {game.Scorer?.Person.Name ?? "None"}"
            ));
        }

        await Task.WhenAll(tasks);
        return tasks.All(t => t.Result);
    }


    public async Task<bool> Text(Person target, string msg) {
        Setup();
        var targetPhoneNumber = target.PhoneNumber;
        if (targetPhoneNumber == null) return false;
        var m = await MessageResource.CreateAsync(
            new PhoneNumber(targetPhoneNumber),
            from: new PhoneNumber("+14093592698"),
            body: msg
        );
        return m.Status == MessageResource.StatusEnum.Sent;
    }
}