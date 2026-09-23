using System;
using Toggl2Vertec.Commands.Check;
using Toggl2Vertec.Configuration;
using Toggl2Vertec.Logging;

namespace Toggl2Vertec.Toggl;

public class TogglAccessCheck : BaseCheckStep
{
    private readonly TogglClient _client;
    private readonly Settings _settings;
    private readonly CredentialStore _credentialStore;

    public TogglAccessCheck(TogglClient client, Settings settings, CredentialStore credentialStore)
    {
        _client = client;
        _settings = settings;
        _credentialStore = credentialStore;
    }

    public override bool Check(ICliLogger logger)
    {
        logger.LogPartial(logger.CreateText($"Checking Toggl API access ({_settings.Toggl.BaseUrl}/users/me/settings): "));
        try
        {
            if (!_credentialStore.TogglOrganizationId.HasValue)
            {
                throw new Exception(TogglClient.MissingOrganizationMessage);
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
