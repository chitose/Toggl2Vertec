using System;
using System.Collections.Generic;
using System.Linq;
using Toggl2Vertec.Toggl;

namespace Toggl2Vertec.Tracking;

public class WorkingDay
{
    public DateTime Date { get; private set; }
    public IEnumerable<LogEntry> Entries { get; set; } = Enumerable.Empty<LogEntry>();
    public IEnumerable<SummaryGroup> Summaries { get; set; } = Enumerable.Empty<SummaryGroup>();
    public WorkingDayAttendance Attendance { get; set; } = new WorkingDayAttendance();
    public bool IsEmpty => !Entries.Any();

    public WorkingDay(DateTime date)
    {
        Date = date;
    }

    public void SetTargetDate(DateTime date)
    {
        Date = date;
    }

    /// <summary>
    /// One working day per date from <paramref name="from"/> to <paramref name="to"/>; entries belong to the day they started on.
    /// </summary>
    public static IList<WorkingDay> FromTimeEntries(DateTime from, DateTime to, IEnumerable<TimeEntry> entries)
    {
        var byDate = entries.ToLookup(entry => entry.Start.Date);
        var days = new List<WorkingDay>();

        for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
        {
            var dayEntries = byDate[date].ToList();
            days.Add(new WorkingDay(date)
            {
                Entries = dayEntries.Select(entry => new LogEntry(entry.Start, entry.End, entry.Text)).ToList(),
                Summaries = dayEntries
                    .GroupBy(entry => entry.Project)
                    .Select(group => new SummaryGroup(
                        group.Key,
                        TimeSpan.FromTicks(group.Sum(entry => (entry.End - entry.Start).Ticks)),
                        group.Select(entry => entry.Text).Where(text => text != null).Distinct().ToList()))
                    .ToList()
            });
        }

        return days;
    }
}
