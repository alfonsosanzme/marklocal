using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MarkLocal.Core;

public class UpdateInfo
{
    [JsonPropertyName("latestVersion")]    public string? LatestVersion { get; set; }
    [JsonPropertyName("minSupportedVersion")] public string? MinSupportedVersion { get; set; }
    [JsonPropertyName("releaseDate")]      public string? ReleaseDate { get; set; }
    [JsonPropertyName("downloadPageUrl")]  public string? DownloadPageUrl { get; set; }
    [JsonPropertyName("releaseNotesText")] public string? ReleaseNotesText { get; set; }
    [JsonPropertyName("releaseNotesUrl")]  public string? ReleaseNotesUrl { get; set; }
}

public enum UpdateCheckKind
{
    NotConfigured,
    UpToDate,
    NewAvailable,
    InvalidFeed,
    Error
}

public class UpdateCheckResult
{
    public UpdateCheckKind Kind { get; init; }
    public UpdateInfo? Info { get; init; }
    public Version? RemoteVersion { get; init; }
    public string? Message { get; init; }

    public static UpdateCheckResult NotConfigured() => new() { Kind = UpdateCheckKind.NotConfigured };
    public static UpdateCheckResult UpToDate() => new() { Kind = UpdateCheckKind.UpToDate };
    public static UpdateCheckResult NewAvailable(UpdateInfo info, Version v) => new() { Kind = UpdateCheckKind.NewAvailable, Info = info, RemoteVersion = v };
    public static UpdateCheckResult InvalidFeed(string msg) => new() { Kind = UpdateCheckKind.InvalidFeed, Message = msg };
    public static UpdateCheckResult Error(string msg) => new() { Kind = UpdateCheckKind.Error, Message = msg };
}

public class UpdateService
{
    private readonly SettingsService _settings;
    private static readonly HttpClient HttpClient = CreateClient();

    public UpdateService(SettingsService settings) { _settings = settings; }

    public async Task<UpdateCheckResult> CheckAsync(Version currentVersion)
    {
        string? url = _settings.Settings.UpdateFeedUrl;
        if (string.IsNullOrWhiteSpace(url)) return UpdateCheckResult.NotConfigured();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var feedUri) ||
            (feedUri.Scheme != Uri.UriSchemeHttp && feedUri.Scheme != Uri.UriSchemeHttps))
        {
            return UpdateCheckResult.InvalidFeed("La URL del feed no es válida.");
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, feedUri);
            req.Headers.UserAgent.ParseAdd("MarkLocal/" + currentVersion);
            req.Headers.Accept.ParseAdd("application/json");
            using var resp = await HttpClient.SendAsync(req, HttpCompletionOption.ResponseContentRead);
            if (!resp.IsSuccessStatusCode)
            {
                return UpdateCheckResult.InvalidFeed($"El servidor devolvió HTTP {(int)resp.StatusCode}.");
            }
            string json = await resp.Content.ReadAsStringAsync();
            var info = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (info == null || string.IsNullOrWhiteSpace(info.LatestVersion))
            {
                return UpdateCheckResult.InvalidFeed("El feed no incluye latestVersion.");
            }
            if (!Version.TryParse(NormalizeVersionString(info.LatestVersion), out var remote))
            {
                return UpdateCheckResult.InvalidFeed("Versión malformada: " + info.LatestVersion);
            }
            return remote > currentVersion
                ? UpdateCheckResult.NewAvailable(info, remote)
                : UpdateCheckResult.UpToDate();
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Error(ex.Message);
        }
    }

    private static string NormalizeVersionString(string raw)
    {
        // Acepta "1.2", "1.2.3", "1.2.3.4". System.Version requiere al menos M.m.
        if (string.IsNullOrEmpty(raw)) return "0.0";
        var parts = raw.Split('.');
        if (parts.Length == 1) return raw + ".0";
        return raw;
    }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        return c;
    }
}
