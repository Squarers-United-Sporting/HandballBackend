using HandballBackend.Authentication;
using HandballBackend.Database;
using HandballBackend.Database.SendableTypes;
using HandballBackend.EndpointHelpers;
using HandballBackend.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.Controllers;

[ApiController]
[Route("api/")]
public class ImageController(HandballContext db) : ControllerBase {
    // GET api/values
    [HttpGet("image")]
    public IActionResult GetImage([BindRequired, FromQuery] string name, [FromQuery] bool big) {
        var fileName = Uri.EscapeDataString(name);
        var path = Config.RESOURCES_FOLDER + "/images/" + (big ? "big/" : "");
        return File(System.IO.File.OpenRead(path + fileName + ".png"), "image/png");
    }

    [HttpGet("people/image")]
    public IActionResult GetPersonImage([BindRequired, FromQuery] string name, [FromQuery] bool big) {
        var fileName = Uri.EscapeDataString(name);
        var path = Config.RESOURCES_FOLDER + "/images/" + (big ? "big/" : "") + "users/";
        return File(System.IO.File.OpenRead(path + fileName + ".png"), "image/png");
    }

    [HttpGet("tournaments/image")]
    public IActionResult GetTournamentImage([BindRequired, FromQuery] string name, [FromQuery] bool big) {
        var fileName = Uri.EscapeDataString(name);
        var path = Config.RESOURCES_FOLDER + "/images/" + (big ? "big/" : "") + "tournaments/";
        return File(System.IO.File.OpenRead(path + fileName + ".png"), "image/png");
    }

    [HttpGet("teams/image")]
    public IActionResult GetTeamImage([BindRequired, FromQuery] string name, [FromQuery] bool big) {
        var fileName = Uri.EscapeDataString(name);
        var path = Config.RESOURCES_FOLDER + "/images/" + (big ? "big/" : "") + "teams/";
        return File(System.IO.File.OpenRead(path + fileName + ".png"), "image/png");
    }


    //Set the method to be a Http POST method (meaning that it has a body)
    [HttpPost("image/people/upload")]
    //Set the method to only be usable as an Admin
    [Authorize(Policy = Policies.IsAdmin)]
    public IActionResult UploadPeopleImage(List<IFormFile> file) {
        // Handball Contexts are used to access the db
        if (file.Count != 1) {
            //when we receive a file it's a list for some reason; we only want 1 file
            return BadRequest("Only one image is allowed");
        }

        var formFile = file.First();
        //do some voodoo shit on the image to make it circle; also saves it.
        var image = ImageHelper.CreatePlayerImageWithCircle(formFile.OpenReadStream(), formFile.FileName);
        // get the person by searchable name
        var person = db.People.Single(p => p.SearchableName == formFile.FileName);
        // set their image paths
        person.ImageUrl = image;
        person.BigImageUrl = $"{image}&big=true";


        // Save the changes (duh.)
        db.SaveChanges();
        return Ok();
    }

    public class UploadTeamImageResponse {
        public required TeamData Team { get; set; }
    }

    //Set the method to be a Http POST method (meaning that it has a body)
    [HttpPost("image/teams/upload")]
    //Set the method to only be usable as an Admin
    [Authorize(Policy = Policies.IsAdmin)]
    public ActionResult<UploadTeamImageResponse> UploadTeamImage([FromForm] List<IFormFile> file,
        [FromForm] string? tournament) {
        // Handball Contexts are used to access the db
        if (file.Count != 1) {
            //when we receive a file it's a list for some reason; we only want 1 file
            return BadRequest("Only one image is allowed");
        }

        var formFile = file.First();
        //do some voodoo shit on the image to make it circle; also saves it.

        // get the team by searchable name
        var team = db.Teams.IncludeRelevant().Include(team => team.TournamentTeams)
            .Single(t => t.SearchableName == formFile.FileName);
        if (tournament == null || team.TournamentTeams.Count(tt => tt.TournamentId != 1) <= 1) {
            var image = ImageHelper.CreateTeamImage(formFile.OpenReadStream(), team.SearchableName);
            team.ImageUrl = image;
            team.BigImageUrl = $"{image}&big=true";
        } else {
            var tournamentObj = db.Tournaments.Single(t => t.SearchableName == tournament);
            var tt = team.TournamentTeams.Single(t => t.TournamentId == tournamentObj.Id);
            var image = ImageHelper.CreateTeamImage(formFile.OpenReadStream(), Utilities.ToSearchable(tt.Name!));
            tt.ImageUrl = image;
            tt.BigImageUrl = $"{image}&big=true";
        }
        // set their image paths

        // Save the changes (duh.)
        db.SaveChanges();
        return Ok(new UploadTeamImageResponse { Team = team.ToSendableData() });
    }

    public class UploadTournamentImageResponse {
        public required TournamentData Tournament { get; set; }
    }

    //Set the method to be a Http POST method (meaning that it has a body)
    [HttpPost("image/tournament/upload")]
    //Set the method to only be usable as an Admin
    [Authorize(Policy = Policies.IsAdmin)]
    public ActionResult<UploadTournamentImageResponse> UploadTournamentImage([FromForm] List<IFormFile> file) {
        // Handball Contexts are used to access the db
        if (file.Count != 1) {
            //when we receive a file it's a list for some reason; we only want 1 file
            return BadRequest("Only one image is allowed");
        }

        var formFile = file.First();
        //do some voodoo shit on the image to make it circle; also saves it.
        var image = ImageHelper.CreateTournamentImage(formFile.OpenReadStream(), formFile.FileName);
        // get the team by searchable name
        var tournament = db.Tournaments.Single(t => t.SearchableName == formFile.FileName);
        // set their image paths
        tournament.ImageUrl = image;

        // Save the changes (duh.)
        db.SaveChanges();
        return Ok(new UploadTournamentImageResponse { Tournament = tournament.ToSendableData() });
    }
}