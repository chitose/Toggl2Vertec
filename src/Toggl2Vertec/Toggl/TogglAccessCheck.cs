using System;
using Toggl2Vertec.Commands.Check;
using Toggl2Vertec.Configuration;
using Toggl2Vertec.Logging;

namespace Toggl2Vertec.Toggl;

public class TogglAccessCheck : BaseCheckStep
{
    private readonly TogglClient _client;
    private readonly Settings _settings;

    public TogglAccessCheck(TogglClient client, Settings settings)
    {
        _client = client;
        _settings = settings;
    }

    public override bool Check(ICliLogger logger)
    {
        logger.LogPartial(logger.CreateText($"Checking Toggl API access ({_settings.Toggl.BaseUrl}/users/me/settings): "));
        try
        {
            if (!_settings.Toggl.OrganizationId.HasValue)
            {
                throw new Exception("Toggl.OrganizationId is not configured - re-run 't2v config' to install a Toggl 2.0 configuration");
            }

            _client.FetchUserSettings().GetProperty("current_workspace_id");
            return Ok(logger);
        }
        catch (Exception e)
        {
            return Fail(logger, e.Message);
        }
    }
}
