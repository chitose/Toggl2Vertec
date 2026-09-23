using System;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;
using Toggl2Vertec.Logging;
using Toggl2Vertec.Ninject;
using Toggl2Vertec.Vertec6;

namespace Toggl2Vertec.Commands.Auto;

public class AutoCommand : CustomCommand<DefaultArgs>
{
    public AutoCommand()
        : base("auto", "updates every day of the current month that has no data in Vertec yet with the data retrieved from Toggl", typeof(DefaultHandler))
    {
    }

    public class DefaultHandler : ICommandHandler<DefaultArgs>
    {
        private readonly ICliLogger _logger;
        private readonly Toggl2VertecConverter _converter;
        private readonly UnfilledDayFinder _finder;

        public DefaultHandler(ICliLogger logger, Toggl2VertecConverter converter, UnfilledDayFinder finder)
        {
            _logger = logger;
            _converter = converter;
            _finder = finder;
        }

        public Task<int> InvokeAsync(InvocationContext context, DefaultArgs args)
        {
            if (!_converter.SupportsClear)
            {
                _logger.LogError("auto is only supported for Vertec 6.5.");
                return Task.FromResult(ResultCodes.Failed);
            }

            var today = DateTime.Today;
            _logger.LogContent("Looking for days without data in Vertec ...");
            var unfilled = _finder.Find(today);

            if (!unfilled.Any())
            {
                _logger.LogContent("Vertec is up to date.");
                return Task.FromResult(ResultCodes.Ok);
            }

            var from = unfilled.First();
            _logger.LogWarning($"Catch-up {from.ToDateString()} -> {today.ToDateString()}: {unfilled.Count} unfilled days ({string.Join(", ", unfilled.Select(d => d.Day))})? (y/n)");
            if (Console.ReadKey().KeyChar != 'y')
            {
                return Task.FromResult(ResultCodes.Cancelled);
            }
            Console.WriteLine();

            // fetch and process everything first so that Toggl / processing errors abort before anything is written
            var days = _converter.GetAndProcessWorkingDays(from, today);

            return Task.FromResult(_converter.UpdateDaysInVertec(days, false, unfilled.ToHashSet()) ? ResultCodes.Ok : ResultCodes.Failed);
        }
    }
}
