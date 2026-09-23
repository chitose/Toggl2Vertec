namespace Toggl2Vertec.Configuration;

public class TogglSettings
{
    public string BaseUrl { get; set; }
    public string CredentialsKey { get; set; }
    // optional - defaults to the user's current workspace
    public long? WorkspaceId { get; set; }
}
