using HandballBackend.Database.Models;
using HandballBackend.Utils;

namespace HandballBackend.Database.SendableTypes;

public class DocumentData(Document document, bool includeTournament = false) {
    public string Name { get; set; } = document.Name;

    public string Description { get; set; } = document.Description;

    public TournamentData? Tournament { get; set; } = includeTournament ? document.Tournament?.ToSendableData() : null;

    public string Link { get; set; } = Utilities.FixHandballUrl(document.Link);

    public Document.DocumentType Type { get; set; } = document.Type;

    public string ImageUrl { get; set; } = Utilities.FixHandballUrl(document.ImageUrl ?? ImageOf(document.Type));

    private static string ImageOf(Document.DocumentType documentType) {
        return documentType switch {
            Document.DocumentType.Rules => "/api/image?name=umpire&big=true",
            Document.DocumentType.UmpireQualificationProgram => "/api/image?name=uqp&big=true",
            Document.DocumentType.TournamentRegulations => "/api/image?name=tournament_regulations&big=true",
            Document.DocumentType.Other => "/api/image?name=SUSS_dark",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}