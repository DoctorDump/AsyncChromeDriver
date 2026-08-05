namespace Zu.ChromeDevTools
{
    public interface ILogger
    {
        void LogTrace(string message, params object[] args);
        void LogError(string message, params object[] args);
    }
}