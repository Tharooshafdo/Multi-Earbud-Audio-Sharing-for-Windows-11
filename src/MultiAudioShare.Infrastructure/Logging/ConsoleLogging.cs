using Microsoft.Extensions.Logging;

namespace MultiAudioShare.Infrastructure.Logging;

public static class ConsoleLogging
{
    public static ILoggerFactory CreateLoggerFactory(LogLevel minimumLevel = LogLevel.Information)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
        });
    }
}
