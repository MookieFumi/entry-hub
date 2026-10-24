namespace GracefulShutdown;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "SIGTERM received: the host is stopping")]
    public static partial void Stopping(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request {Id} accepted while stopping")]
    public static partial void AcceptedWhileStopping(ILogger logger, Guid id);
}