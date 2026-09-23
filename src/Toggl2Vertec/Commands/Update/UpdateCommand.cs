using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Toggl2Vertec.Logging;
using Toggl2Vertec.Ninject;

namespace Toggl2Vertec.Commands.Update;

public class UpdateCommand : CustomCommand<SyncArgs>
{
    public UpdateCommand()
        : base("update", "updates Vertec with the data retrieved from Toggl", typeof(DefaultHandler))
    {
        AddArgument(new Argument<DateTime>("date", () => DateTime.Today));
        AddOption(new Option<DateTime?>("--targetDate"));
        AddOption(new Option<bool>("--force", "Clears the day in Vertec before updating it"));
    }

    public class DefaultHandler : ICommandHandler<SyncArgs>
    {
        private readonly ICliLogger _logger;
        private readonly Toggl2VertecConverter _converter;

        public DefaultHandler(ICliLogger logger, Toggl2VertecConverter converter)
        {
            _logger = logger;
            _converter = converter;
        }

        public Task<int> InvokeAsync(InvocationContext context, SyncArgs args)
        {
            if (args.Date.IsInPastMonth(DateTime.Today) && (!args.TargetDate.HasValue || args.TargetDate.Value.IsInPastMonth(DateTime.Today)))
            {
                _logger.LogError("Date cannot be in the past month (already validated in Vertec).");
                return Task.FromResult(ResultCodes.InvalidDate);
            }

            if (args.Force && !_converter.SupportsClear)
            {
                _logger.LogError("--force is only supported for Vertec 6.5.");
                return Task.FromResult(ResultCodes.Failed);
            }

            _logger.LogContent($"Updating data for {args.Date.ToDateString()}");

            var workingDay = _converter.GetAndProcessWorkingDay(args.Date);

            if (workingDay.IsEmpty)
            {
                _logger.LogWarning($"No Toggl data for {args.Date.ToDateString()} - Vertec is left untouched.");
                return Task.FromResult(ResultCodes.Ok);
            }

            if (args.TargetDate.HasValue)
            {
                _logger.LogContent($"Retargeting work to {args.TargetDate.Value.ToDateString()}");
                workingDay.SetTargetDate(args.TargetDate.Value);
            }

            _logger.LogContent($"Updating ...");
            _converter.UpdateDayInVertec(workingDay, args.Force);

            return Task.FromResult(ResultCodes.Ok);
        }
    }
}
