namespace OpenUsage.Windows;

internal static class AppLog
{
    public static void Error(string operation, Exception error, Guid? account = null)
    {
        // Log only classifications; exception messages can contain provider secrets or response data.
        var line = $"{DateTimeOffset.UtcNow:O} {operation} account={account} type={error.GetType().Name} status={(error as ProviderException)?.StatusCode}\n";
        try
        {
            Directory.CreateDirectory(AccountStore.DataDirectory);
            var path = Path.Combine(AccountStore.DataDirectory, "errors.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, line);
        }
        catch (Exception loggingError) when (loggingError is IOException or UnauthorizedAccessException)
        { System.Diagnostics.Trace.WriteLine($"OpenUsage Could Not Write Its Error Log ({loggingError.GetType().Name})."); }
    }
    public static string Friendly(Exception error) => error switch
    {
        ProviderException => error.Message,
        HttpRequestException => "Could Not Reach The Provider. Check Your Internet Connection And Try Again.",
        OperationCanceledException => "The Request Timed Out Or Was Cancelled. Try Again.",
        IOException or UnauthorizedAccessException => "Could Not Save Your Accounts. Check Disk Space And Folder Permissions.",
        System.Security.Cryptography.CryptographicException => "Windows Could Not Unlock This App's Credentials. Use The Windows Account That Added Them.",
        _ => "The Operation Failed. See The App's Error Log For The Error Type."
    };
}
