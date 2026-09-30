namespace Zadatak17CocktailWordCloud.Utils;

public static class Logger
{
    private static readonly object _consoleLock = new();

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message)
    {
        Write("ERROR", message);
    }

    private static void Write(string level, string message)
    {
        lock (_consoleLock)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] [{level}] [Thread {Environment.CurrentManagedThreadId}] {message}");
        }
    }
}
