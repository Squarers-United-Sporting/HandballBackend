using HandballBackend.Database.SendableTypes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HandballBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase {
    // GET api/values

    public class IndexResponse {
        public required List<TournamentDocumentList> TournamentDocuments { get; set; }
        public required List<DocumentData> OtherDocuments { get; set; }
    }

    public class TournamentDocumentList {
        public required List<DocumentData> Documents { get; set; }
        public required TournamentData Tournament { get; set; }
    }

    [HttpGet("index")]
    public async Task<ActionResult<IndexResponse>> GetDocuments() {
        var db = new HandballContext();
        var tournamentDocuments = (await db.Documents
                .Where(d => d.TournamentId != null)
                .Include(d => d.Tournament)
                .GroupBy(d => d.TournamentId)
                .ToListAsync())
            .OrderBy(tdl => tdl.Key)
            .Select(d => new TournamentDocumentList {
                Documents = d.Select(d2 => d2.ToSendableData(false)).OrderBy(d2 => d2.Type).ThenBy(d2 => d2.Name)
                        .ToList(),
                Tournament = d.First().Tournament!.ToSendableData()
            }
            ).ToList();
        var documents = (await db.Documents
                .Where(d => d.TournamentId == null)
                .Select(d => d.ToSendableData(false)).ToListAsync())
            .OrderBy(d => d.Type).ThenBy(d => d.Name).ToList();

        return Ok(new IndexResponse {
            TournamentDocuments = tournamentDocuments,
            OtherDocuments = documents
        });
    }

    [HttpGet("simplified_rules")]
    public IActionResult GetSimplifiedRulesFile() {
        return File(System.IO.File.OpenRead(Config.RESOURCES_FOLDER + "/documents/pdf/rules_simple.pdf"),
            "application/pdf");
    }

    [HttpGet("code_of_conduct")]
    public IActionResult GetCodeOfConductFile() {
        return File(System.IO.File.OpenRead(Config.RESOURCES_FOLDER + "/documents/pdf/code_of_conduct.pdf"),
            "application/pdf");
    }

    [HttpGet("tournament_regulations")]
    public IActionResult GetTournamentRegulationsFile() {
        return File(System.IO.File.OpenRead(Config.RESOURCES_FOLDER + "/documents/pdf/tournament_regulations.pdf"),
            "application/pdf");
    }

    [HttpGet("rules")]
    public IActionResult GetRulesFile() {
        return File(System.IO.File.OpenRead(Config.RESOURCES_FOLDER + "/documents/pdf/rules.pdf"), "application/pdf");
    }

    [HttpGet("rules_solo")]
    public IActionResult GetSoloRulesFile() {
        return File(System.IO.File.OpenRead(Config.RESOURCES_FOLDER + "/documents/pdf/rules_solo.pdf"), "application/pdf");
    }

    [HttpGet("rules/{tournament}")]
    public IActionResult GetOldRulesFile(string tournament) {
        var fileName = Uri.EscapeDataString(tournament);
        var path = Config.RESOURCES_FOLDER + "/documents/pdf/old_documents/" + fileName + "/rules.pdf";
        return File(System.IO.File.OpenRead(path), "application/pdf");
    }

    [HttpGet("tournament_regulations/{tournament}")]
    public IActionResult GetOldTournamentRegulationsFile(string tournament) {
        var fileName = Uri.EscapeDataString(tournament);
        var path = Config.RESOURCES_FOLDER + "/documents/pdf/old_documents/" + fileName + "/tournament_regulations.pdf";
        return File(System.IO.File.OpenRead(path), "application/pdf");
    }
}