using HandballBackend.Utils;
using HtmlAgilityPack;

namespace HandballBackend.EndpointHelpers;

public class GoogleImagesHelper {
    public static async Task<string> GetImageUrl(string searchTerm) {
        var url = "https://www.google.com/search?tbm=isch&q=" + Utilities.ToSearchable(searchTerm);

        var web = new HtmlWeb {
            UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:23.0) Gecko/20100101 Firefox/23.0",
        };

        var htmlDoc = await web.LoadFromWebAsync(url);

        var node = htmlDoc
            .DocumentNode.Descendants("img")
            .Where(tag => tag.GetAttributeValue("src", "").Contains("gstatic.com"));
        return node.FirstOrDefault()?.GetAttributeValue("src", "/api/images?name=blank")
            ?? "/api/images?name=blank";
    }

    public static async Task<bool> SetTeamImageUrl(string searchableName) {
        await using var db = new HandballContext();
        var team = db.Teams.FirstOrDefault(t => t.SearchableName == searchableName);
        if (team == null)
            return false;
        var url = await GetImageUrl(searchableName);
        team.ImageUrl = url;
        await db.SaveChangesAsync();
        return true;
    }
}