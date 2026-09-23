using System;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using Toggl2Vertec.Configuration;
using Toggl2Vertec.Logging;
using Toggl2Vertec.Ninject;

namespace Toggl2Vertec.Commands.Config;

public class ResetCommand : CustomCommand<DefaultArgs>
{
    public ResetCommand()
        : base("reset", "Resets the configuration file in the user's home directory to an empty one, so the built-in defaults apply", typeof(DefaultHandler))
    {
    }

    public class DefaultHandler : ICommandHandler<DefaultArgs>
    {
        private readonly ICliLogger _logger;

        public DefaultHandler(ICliLogger logger)
        {
            _logger = logger;
        }

        public Task<int> InvokeAsync(InvocationContext context, DefaultArgs args)
        {
            var absolutePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ConfigurationModule.SettingsFileName);
            if (File.Exists(absolutePath))
            {
                _logger.LogWarning($"Do you want to reset {absolutePath}? A backup is kept as {absolutePath}.bak (y/n)");
                if (Console.ReadKey().KeyChar != 'y')
                {
                    return Task.FromResult(ResultCodes.Cancelled);
                }

                Console.WriteLine();
                File.Copy(absolutePath, absolutePath + ".bak", true);
            }

            File.WriteAllText(absolutePath, "{}");
            _logger.LogContent($"Reset {absolutePath}");

            return Task.FromResult(ResultCodes.Ok);
        }
    }
}
