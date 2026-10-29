namespace OpenUsage.Windows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var instance = new Mutex(true, @"Local\OpenUsage.Windows", out var first);
        if (!first)
        {
            MessageBox.Show("OpenUsage Is Already Running. Open It From The Icon Near The Clock.", "OpenUsage");
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Report(e.Exception);
        try
        {
            using var api = new Api();
            Application.Run(new MainForm(new AccountStore(), api, args.Contains("--background")));
        }
        catch (Exception error) { Report(error); }
    }
    private static void Report(Exception error)
    {
        AppLog.Error("Application", error);
        MessageBox.Show(AppLog.Friendly(error) + "\n\nYour Saved Account File Has Been Preserved.", "OpenUsage", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
