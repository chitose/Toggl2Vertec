using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Toggl2Vertec.Configuration;
using Toggl2Vertec.Logging;

namespace Toggl2Vertec.Toggl;

public record TimeEntry(DateTime Start, DateTime End, string Project, string Text);

/// <summary>
/// Client for the Toggl 2.0 (Focus) API - see https://engineering.toggl.com/docs/focus/
/// </summary>
public class TogglClient
{
    private readonly string _baseUrl;
    private readonly TogglSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ICliLogger _logger;
    private long? _workspaceId;

    public TogglClient(Settings settings, CredentialStore credStore, ICliLogger logger)
    {
        _httpClient = new HttpClient();
        _settings = settings.Toggl;
        _baseUrl = _settings.BaseUrl;
        _workspaceId = _settings.WorkspaceId;

        var credentials = credStore.TogglCredentials;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Password);
        _logger = logger;
    }

    public JsonElement FetchUserSettings()
    {
        return Fetch("/users/me/settings");
    }

    /// <summary>
    /// Fetches all time entries from the start of <paramref name="from"/> until the end of <paramref name="to"/> (local time)
    /// with a single request to be gentle on the API quota.
    /// </summary>
    public IList<TimeEntry> FetchTimeEntries(DateTime from, DateTime to)
    {
        if (!_settings.OrganizationId.HasValue)
        {
            throw new ToggleClientException("Toggl.OrganizationId is not configured - re-run 't2v config' to install a Toggl 2.0 configuration");
        }

        var dateFrom = Uri.EscapeDataString(new DateTimeOffset(from.Date).ToString("yyyy-MM-ddTHH:mm:sszzz"));
        var dateTo = Uri.EscapeDataString(new DateTimeOffset(to.Date.AddDays(1)).ToString("yyyy-MM-ddTHH:mm:sszzz"));
        var data = Fetch($"/organizations/{_settings.OrganizationId}/workspaces/{GetWorkspace()}/time-entries/stream?date_from={dateFrom}&date_to={dateTo}&include_taskless=true");

        return ParseTimeEntries(data);
    }

    public static IList<TimeEntry> ParseTimeEntries(JsonElement data)
    {
        var items = data.EnumerateArray()
            // only tracked activities count - breaks and planned-only entries are ignored
            .Where(item => item.Get("type").GetStringSafe() == "activity" && item.Get("start").HasValue() && item.Get("duration").HasValue())
            .Where(item => item.Get("duration").GetInt64() > 0)
            .ToList();

        var users = items.Select(item => item.Get("toggl_user_id").HasValue() ? item.Get("toggl_user_id").GetRawText() : null).Distinct().Count();
        if (users > 1)
        {
            throw new ToggleClientException($"Received time entries of {users} different users - refusing to write someone else's time into Vertec");
        }

        return items
            .Select(item =>
            {
                var start = item.Get("start").GetDateTimeOffset().LocalDateTime;
                var text = item.Get("description").GetStringSafe();
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = item.Get("task.name").GetStringSafe();
                }

                return new TimeEntry(start, start.AddSeconds(item.Get("duration").GetInt64()), item.Get("project.name").GetStringSafe(), text);
            })
            .OrderBy(entry => entry.Start)
            .ToList();
    }

    public static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.PaymentRequired)
        {
            var resetsIn = response.Headers.TryGetValues("X-Toggl-Quota-Resets-In", out var values) ? values.First() : "?";
            throw new ToggleClientException($"Toggl API quota exceeded, resets in {resetsIn} seconds");
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new ToggleClientException($"Unexpected response from the server: {response.StatusCode}");
        }
    }

    private long GetWorkspace()
    {
        if (!_workspaceId.HasValue)
        {
            _workspaceId = FetchUserSettings().GetProperty("current_workspace_id").GetInt64();
            _logger.LogInfo($"Toggl Workspace ID: {_workspaceId.Value}");
        }

        return _workspaceId.Value;
    }

    private JsonElement Fetch(string path)
    {
        var url = $"{_baseUrl}{path}";
        _logger.LogInfo($"GET {url}");
        var result = _httpClient.Send(new HttpRequestMessage(HttpMethod.Get, url));
        EnsureSuccess(result);

        var json = result.Content.ReadAsStringAsync().Result;
        _logger.LogDebug(new DebugContent("Response", () => json));
        var data = (JsonElement?)JsonSerializer.Deserialize(json, typeof(object));

        return data ?? throw new ToggleClientException("No data");
    }
}
