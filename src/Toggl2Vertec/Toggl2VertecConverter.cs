using Ninject;
using Ninject.Parameters;
using Ninject.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using Toggl2Vertec.Configuration;
using Toggl2Vertec.Logging;
using Toggl2Vertec.Toggl;
using Toggl2Vertec.Tracking;
using Toggl2Vertec.Vertec6;

namespace Toggl2Vertec;

public class Toggl2VertecConverter
{
    private readonly IResolutionRoot _resolutionRoot;
    private readonly ICliLogger _logger;
    private readonly Settings _settings;
    private readonly TogglClient _togglClient;
    private readonly IVertecUpdateProcessor _updateProcess;
    private readonly ClearProcessor _clearProcessor;


    public Toggl2VertecConverter(
        IResolutionRoot resolutionRoot,
        ICliLogger logger,
        Settings settings,
        TogglClient togglClient,
        IVertecUpdateProcessor updateProcess,
        ClearProcessor clearProcessor
    ) {
            _resolutionRoot = resolutionRoot;
            _logger = logger;
            _settings = settings;
            _togglClient = togglClient;
            _updateProcess = updateProcess;
            _clearProcessor = clearProcessor;
        }

    // clearing a day is only implemented for the Vertec 6 XML API
    public bool SupportsClear => _settings.Vertec.Version == "6.5";

    public WorkingDay GetAndProcessWorkingDay(DateTime date)
    {
            return GetAndProcessWorkingDays(date, date).Single();
        }

    /// <summary>
    /// Fetches and processes all days of the range up front. Empty days are returned unprocessed.
    /// </summary>
    public IList<WorkingDay> GetAndProcessWorkingDays(DateTime from, DateTime to)
    {
            var days = WorkingDay.FromTimeEntries(from, to, _togglClient.FetchTimeEntries(from, to));
            return days.Select(day => day.IsEmpty ? day : Process(day)).ToList();
        }

    private WorkingDay Process(WorkingDay day)
    {
            foreach (var processorDef in _settings.GetProcessors())
            {
                var processor = _resolutionRoot.Get<IWorkingDayProcessor>(processorDef.Name, new TypeMatchingConstructorArgument(typeof(ProcessorDefinition), (ctx, target) => processorDef, true));
                day = processor.Process(day);
            }

            return day;
        }

    public void UpdateDayInVertec(WorkingDay workingDay, bool force = false)
    {
            if (force)
            {
                _logger.LogContent($"Clearing {workingDay.Date.ToDateString()} ...");
                _clearProcessor.Process(workingDay.Date);
            }

            _updateProcess.Process(workingDay);
        }

    public void ClearDayInVertec(DateTime date)
    {
            _clearProcessor.Process(date);
        }
}
