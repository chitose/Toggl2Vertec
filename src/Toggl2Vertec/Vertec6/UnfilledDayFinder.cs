using System;
using System.Collections.Generic;
using System.Linq;
using Toggl2Vertec.Logging;
using Toggl2Vertec.Vertec6.Requests;

namespace Toggl2Vertec.Vertec6;

/// <summary>
/// Finds the scheduled days (target time above zero) of the current month up to today without any recorded time.
/// </summary>
public class UnfilledDayFinder
{
    private readonly ICliLogger _logger;
    private readonly CredentialStore _credStore;
    private readonly XmlApiClient _xmlApiClient;

    public UnfilledDayFinder(ICliLogger logger, CredentialStore credStore, XmlApiClient xmlApiClient)
    {
        _logger = logger;
        _credStore = credStore;
        _xmlApiClient = xmlApiClient;
    }

    public IList<DateTime> Find(DateTime today)
    {
        _xmlApiClient.Authenticate().Wait();
        var ownerId = new GetUserId(_credStore.VertecCredentials.UserName).Execute(_xmlApiClient);
        _logger.LogInfo($"Projektbearbeiter ID: {ownerId}");

        // ponytail: two queries per day (at most ~60 per month), a single range query if this ever gets slow
        var days = new List<(DateTime Date, long TargetMinutes, long WorkMinutes)>();
        for (var date = new DateTime(today.Year, today.Month, 1); date <= today.Date; date = date.AddDays(1))
        {
            days.Add((date,
                new GetTargetTime(date, date, ownerId).Execute(_xmlApiClient),
                new GetWorkTime(date, date, ownerId).Execute(_xmlApiClient)));
        }

        return SelectUnfilled(days);
    }

    public static IList<DateTime> SelectUnfilled(IEnumerable<(DateTime Date, long TargetMinutes, long WorkMinutes)> days)
    {
        return days.Where(day => day.TargetMinutes > 0 && day.WorkMinutes == 0).Select(day => day.Date).ToList();
    }
}
