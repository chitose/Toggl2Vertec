using System.Net;
using System.Text.Json;
using Toggl2Vertec.Toggl;
using Toggl2Vertec.Tracking;

namespace Toggl2Vertec.Tests;

public class TogglFocusTests
{
    private static JsonElement Json(params object[] items) => JsonSerializer.SerializeToElement(items);

    // local-time RFC 3339 timestamp, so the tests don't depend on the machine's time zone
    private static string Local(int day, int hour, int minute = 0) =>
        new DateTimeOffset(new DateTime(2026, 9, day, hour, minute, 0)).ToString("yyyy-MM-ddTHH:mm:sszzz");

    private static object Entry(string start, int seconds, string? description, string project = "P-1-A", string type = "activity", string? task = null, int user = 1) =>
        new { start, duration = seconds, description, type, toggl_user_id = user, project = new { name = project }, task = new { name = task } };

    [Fact]
    public void ParseTimeEntries_KeepsOnlyTrackedActivities()
    {
        var entries = TogglClient.ParseTimeEntries(Json(
            Entry(Local(22, 9), 3600, "coding"),
            Entry(Local(22, 12), 1800, "lunch", type: "break"),
            new { type = "activity", planned_start = Local(22, 14), planned_duration = 600, toggl_user_id = 1 },
            Entry(Local(22, 13), 0, "running timer")));

        var entry = Assert.Single(entries);
        Assert.Equal(new DateTime(2026, 9, 22, 9, 0, 0), entry.Start);
        Assert.Equal(new DateTime(2026, 9, 22, 10, 0, 0), entry.End);
        Assert.Equal("P-1-A", entry.Project);
        Assert.Equal("coding", entry.Text);
    }

    [Fact]
    public void ParseTimeEntries_FallsBackToTaskName()
    {
        var entries = TogglClient.ParseTimeEntries(Json(Entry(Local(22, 9), 60, "", task: "review PR")));

        Assert.Equal("review PR", Assert.Single(entries).Text);
    }

    [Fact]
    public void ParseTimeEntries_RejectsOtherUsersEntries()
    {
        var json = Json(Entry(Local(22, 9), 60, "a"), Entry(Local(22, 10), 60, "b", user: 2));

        Assert.Throws<ToggleClientException>(() => TogglClient.ParseTimeEntries(json));
    }

    [Fact]
    public void FromTimeEntries_SplitsByStartDayAndAggregatesPerProject()
    {
        var entries = TogglClient.ParseTimeEntries(Json(
            Entry(Local(21, 9), 3600, "coding"),
            Entry(Local(21, 11), 1800, "coding"),
            Entry(Local(21, 13), 900, "meeting", project: "P-2-B"),
            Entry(Local(21, 23, 30), 3600, "late night")));

        var days = WorkingDay.FromTimeEntries(new DateTime(2026, 9, 20), new DateTime(2026, 9, 22), entries);

        Assert.Equal(new[] { 20, 21, 22 }, days.Select(d => d.Date.Day));
        Assert.True(days[0].IsEmpty);
        Assert.True(days[2].IsEmpty); // the entry crossing midnight belongs to the day it started
        Assert.Equal(4, days[1].Entries.Count());

        var p1 = days[1].Summaries.Single(s => s.Title == "P-1-A");
        Assert.Equal(TimeSpan.FromMinutes(150), p1.Duration);
        Assert.Equal(new[] { "coding", "late night" }, p1.Text);
        Assert.Equal(TimeSpan.FromMinutes(15), days[1].Summaries.Single(s => s.Title == "P-2-B").Duration);
    }

    [Fact]
    public void EnsureSuccess_ReportsQuotaReset()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PaymentRequired);
        response.Headers.Add("X-Toggl-Quota-Resets-In", "1234");

        var e = Assert.Throws<ToggleClientException>(() => TogglClient.EnsureSuccess(response));
        Assert.Contains("1234", e.Message);
    }

    [Theory]
    [InlineData("2026-09-01", "2026-09-23", false)]
    [InlineData("2026-08-31", "2026-09-23", true)]
    [InlineData("2025-11-15", "2026-09-23", true)]  // earlier year, later month number
    [InlineData("2026-12-15", "2027-01-05", true)]  // across the year boundary
    [InlineData("2026-10-01", "2026-09-23", false)]
    public void IsInPastMonth_ComparesYearAndMonth(string date, string today, bool expected)
    {
        Assert.Equal(expected, DateTime.Parse(date).IsInPastMonth(DateTime.Parse(today)));
    }
}

public class UnfilledDayFinderTests
{
    [Fact]
    public void SelectUnfilled_KeepsScheduledDaysWithoutRecordedTime()
    {
        var unfilled = Vertec6.UnfilledDayFinder.SelectUnfilled(new[]
        {
            (new DateTime(2026, 9, 18), 492L, 492L), // filled
            (new DateTime(2026, 9, 19), 0L, 0L),     // weekend
            (new DateTime(2026, 9, 21), 492L, 0L),   // unfilled
            (new DateTime(2026, 9, 22), 492L, 492L), // vacation counts as recorded time
            (new DateTime(2026, 9, 23), 492L, 0L),   // unfilled
        });

        Assert.Equal(new[] { new DateTime(2026, 9, 21), new DateTime(2026, 9, 23) }, unfilled);
    }
}
