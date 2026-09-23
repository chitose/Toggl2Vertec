using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;
using Toggl2Vertec.Logging;
using Toggl2Vertec.Ninject;

namespace Toggl2Vertec.Commands.Batch;

public class BatchArgs : ICommonArgs
{
    public bool Verbose { get; set; }
    public bool Debug { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public bool Force { get; set; }
}

public class BatchCommand : CustomCommand<BatchArgs>
{
    public BatchCommand()
        : base("batch", "updates Vertec for every day in a date range with the data retrieved from Toggl", typeof(DefaultHandler))
    {
        AddArgument(new Argument<DateTime>("from", "First day of the range"));
        AddArgument(new Argument<DateTime>("to", () => DateTime.Today, "Last day of the range. Default is today."));
        AddOption(new Option<bool>("--force", "Clears each day in Vertec before updating it"));
    }

    public class DefaultHandler : ICommandHandler<BatchArgs>
    {
        private readonly ICliLogger _logger;
        private readonly Toggl2VertecConverter _converter;

        public DefaultHandler(ICliLogger logger, Toggl2VertecConverter converter)
        {
            _logger = logger;
            _converter = converter;
        }

        public Task<int> InvokeAsync(InvocationContext context, BatchArgs args)
        {
            var from = args.From.Date;
            var to = args.To.Date;

            if (to > DateTime.Today)
            {
                _logger.LogContent($"Limiting range to today ({DateTime.Today.ToDateString()})");
                to = DateTime.Today;
            }

            if (from > to)
            {
                _logger.LogError($"Range start {from.ToDateString()} is after its end {to.ToDateString()}.");
                return Task.FromResult(ResultCodes.InvalidDate);
            }

            if (from.IsInPastMonth(DateTime.Today))
            {
                _logger.LogError("Range cannot start in the past month (already validated in Vertec).");
                return Task.FromResult(ResultCodes.InvalidDate);
            }

            if (args.Force && !_converter.SupportsClear)
            {
                _logger.LogError("--force is only supported for Vertec 6.5.");
                return Task.FromResult(ResultCodes.Failed);
            }

            // fetch and process everything first so that Toggl / processing errors abort before anything is written
            _logger.LogContent($"Collecting data from {from.ToDateString()} to {to.ToDateString()}");
            var days = _converter.GetAndProcessWorkingDays(from, to);

            var report = new List<(DateTime Date, string Result)>();
            var failed = false;
            foreach (var day in days)
            {
                if (failed)
                {
                    report.Add((day.Date, "not attempted"));
                    continue;
                }

                if (day.IsEmpty)
                {
                    report.Add((day.Date, "skipped (empty)"));
                    continue;
                }

                try
                {
                    _logger.LogContent($"Updating {day.Date.ToDateString()} ...");
                    _converter.UpdateDayInVertec(day, args.Force);
                    report.Add((day.Date, args.Force ? "forced" : "updated"));
                }
                catch (Exception e)
                {
                    _logger.LogError($"Updating {day.Date.ToDateString()} failed: {e.Message}");
                    report.Add((day.Date, "failed"));
                    failed = true;
                }
            }

            _logger.LogContent("Summary:");
            foreach (var (date, result) in report)
            {
                _logger.LogContent($"  {date.ToDateString()} {date.DayOfWeek,-9} {result}");
            }

            return Task.FromResult(failed ? ResultCodes.Failed : ResultCodes.Ok);
        }
    }
}
