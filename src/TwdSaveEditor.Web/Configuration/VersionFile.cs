using System.Net.Http.Json;

namespace TwdSaveEditor.Web.Configuration;

public sealed class VersionFile(HttpClient http)
{
    public const string FileName = "version.json";
    public const string DevelopmentVersion = "dev";

    public async Task<string> ReadVersionAsync()
    {
        try
        {
            using var response = await http.GetAsync(FileName);
            if (!response.IsSuccessStatusCode)
            {
                return DevelopmentVersion;
            }

            var content = await response.Content.ReadFromJsonAsync<VersionFileContent>();
            return string.IsNullOrWhiteSpace(content?.Version) ? DevelopmentVersion : content.Version;
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException)
        {
            return DevelopmentVersion;
        }
    }
}
