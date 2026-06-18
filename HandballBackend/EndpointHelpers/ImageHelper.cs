using HandballBackend.Utils;
using HtmlAgilityPack;
using ImageMagick;
using ImageMagick.Drawing;

namespace HandballBackend.EndpointHelpers;

public static class ImageHelper {
    private static readonly FileInfo CircleOutline = new(
        Config.RESOURCES_FOLDER + "/images/circle_outline.png"
    );

    public static string CreatePlayerImageWithCircle(Stream image, string searchableName = "test") {
        using (var bigImage = AddCircleToImage(image, true)) {
            bigImage.Write(Config.RESOURCES_FOLDER + $"/images/big/users/{searchableName}.png");
        }

        image.Seek(0, SeekOrigin.Begin);
        using (var smallImage = AddCircleToImage(image, false)) {
            smallImage.Write(Config.RESOURCES_FOLDER + $"/images/users/{searchableName}.png");
        }

        _ = ServerManagementHelper.SaveResourcesToGit();

        return $"/api/people/image?name={searchableName}";
    }

    public static string CreateTeamImageWithCircle(Stream image, string searchableName = "test") {
        using (var bigImage = AddCircleToImage(image, true)) {
            bigImage.Write(Config.RESOURCES_FOLDER + $"/images/big/teams/{searchableName}.png");
        }

        image.Seek(0, SeekOrigin.Begin);
        using (var smallImage = AddCircleToImage(image, false)) {
            smallImage.Write(Config.RESOURCES_FOLDER + $"/images/teams/{searchableName}.png");
        }

        _ = ServerManagementHelper.SaveResourcesToGit();

        return $"/api/teams/image?name={searchableName}";
    }

    public static string CreateTeamImage(Stream imageIn, string searchableName) {
        using (var image = new MagickImage(imageIn)) {
            var circleOutline = new MagickImage(CircleOutline);
            image.Resize(circleOutline.Width, circleOutline.Height);
            image.Write(Config.RESOURCES_FOLDER + $"/images/big/teams/{searchableName}.png");

            image.Resize(200, 200);
            image.Write(Config.RESOURCES_FOLDER + $"/images/teams/{searchableName}.png");
        }

        _ = ServerManagementHelper.SaveResourcesToGit();


        return $"/api/teams/image?name={searchableName}";
    }


    public static string CreateTournamentImage(Stream imageIn, string searchableName) {
        using (var image = new MagickImage(imageIn)) {
            var circleOutline = new MagickImage(CircleOutline);
            image.Resize(circleOutline.Width, circleOutline.Height);
            image.Write(Config.RESOURCES_FOLDER + $"/images/big/tournaments/{searchableName}.png");

            image.Resize(200, 200);
            image.Write(Config.RESOURCES_FOLDER + $"/images/tournaments/{searchableName}.png");
        }

        _ = ServerManagementHelper.SaveResourcesToGit();


        return $"/api/tournaments/image?name={searchableName}";
    }

    public static async Task SetGoogleImageForTeam(HandballContext db, int teamId) {
        var team = (await db.Teams.FindAsync(teamId))!;
        var imageLink = await GetGoogleImageUrl(team.Name);
        if (imageLink == null)
            return;
        var stream = await GetImageFromLink(imageLink);
        var localLink = CreateTeamImageWithCircle(stream, team.SearchableName);
        team.ImageUrl = localLink;
        team.BigImageUrl = localLink + "?big=true";
        await db.SaveChangesAsync();
    }

    private static IMagickImage AddCircleToImage(Stream image, bool bigImage = false) {
        MagickImageCollection? images = null;
        var circleOutline = new MagickImage(CircleOutline);
        using var imageIn = new MagickImage(image);
        var w = imageIn.Width;
        var h = imageIn.Height;
        if (!bigImage) {
            circleOutline.Resize(200, 200);
        }

        var finalSize = circleOutline.Height;
        // we want to resize so that the smaller of the two sizes is correct (otherwise we will get gaps)

        if (w > h) {
            imageIn.Resize(0, finalSize);
        } else {
            imageIn.Resize(finalSize, 0);
        }

        imageIn.Crop(circleOutline.Width, finalSize, Gravity.Center);

        imageIn.ResetPage();

        //use a mask to eliminate the corner bits
        using var mask = new MagickImage(MagickColors.Transparent, finalSize, finalSize);

        //luciferian magic to make the circle look nice (I don't know how it works)
        new Drawables()
            .FillColor(MagickColors.White)
            .Circle(finalSize / 2.0, finalSize / 2.0, (finalSize - 70) / 2.0, 10)
            .Draw(mask);
        mask.Alpha(AlphaOption.Extract);
        imageIn.Composite(mask, CompositeOperator.CopyAlpha);

        // Make the background transparent
        imageIn.Alpha(AlphaOption.Set);
        imageIn.BackgroundColor = MagickColors.Transparent;
        imageIn.Evaluate(Channels.Alpha, EvaluateOperator.Multiply, 1);

        images = new MagickImageCollection();
        images.Add(imageIn);
        images.Add(circleOutline);
        var result = images.Mosaic();
        return result;
    }

    private static async Task<Stream> GetImageFromLink(string? url) {
        using var client = new HttpClient();
        using var response = await client.GetAsync(url);
        var webStream = await response.Content.ReadAsStreamAsync();
        var memoryStream = new MemoryStream();
        await webStream.CopyToAsync(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }

    private static async Task<string?> GetGoogleImageUrl(string searchTerm) {
        var url = "https://www.google.com/search?tbm=isch&q=" + Utilities.ToSearchable(searchTerm);

        var web = new HtmlWeb {
            UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko",
            PreRequest = request => {
                request.Accept = "text/html, application/xhtml+xml, */*";
                return true;
            },
        };

        var htmlDoc = await web.LoadFromWebAsync(url);
        var scriptNodes = htmlDoc.DocumentNode.SelectNodes("//script[contains(., '_setImgSrc')]");
        var imgNodes = htmlDoc.DocumentNode.SelectNodes("//img[contains(@src, 'http')]");
        if (imgNodes != null) {
            foreach (var imgNode in imgNodes) {
                var src = imgNode.GetAttributeValue("src", "");
                if (!string.IsNullOrEmpty(src) && !src.StartsWith("data:")) {
                    return src;
                }
            }
        }

        if (scriptNodes != null) {
            foreach (var scriptNode in scriptNodes) {
                var scriptText = scriptNode.InnerText;
                var startIndex = scriptText.IndexOf("_setImgSrc('", StringComparison.Ordinal);
                if (startIndex < 0)
                    continue;
                startIndex += 12; // Length of "_setImgSrc('"
                var endIndex = scriptText.IndexOf($"'", startIndex, StringComparison.Ordinal);
                if (endIndex > startIndex) {
                    return scriptText.Substring(startIndex, endIndex - startIndex);
                }
            }
        }

        Console.WriteLine("Could not find Google Image URL");
        return null;
    }
}