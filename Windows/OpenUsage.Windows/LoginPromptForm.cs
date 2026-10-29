using W = System.Windows;
using C = System.Windows.Controls;

namespace OpenUsage.Windows;

internal sealed class LoginPromptForm : Form
{
    private readonly TaskCompletionSource<string?> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration registration;
    public Task<string?> Result => result.Task;
    public LoginPromptForm(LoginPrompt prompt, CancellationToken ct, DisplayPreferences? display = null)
    {
        Text = "Complete Sign-In"; AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(440, prompt.AcceptsPastedCode ? 375 : 320); FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
        var content = new C.StackPanel { Margin = new W.Thickness(24) };
        var title = DialogSurface.Text("Complete Your Sign-In", 20); title.FontWeight = W.FontWeights.SemiBold; content.Children.Add(title);
        content.Children.Add(DialogSurface.Text(prompt.Message, 12, true));
        if (prompt.Code is not null)
        {
            var code = new C.TextBox { Text = prompt.Code, IsReadOnly = true, FontSize = 24, FontWeight = W.FontWeights.SemiBold, Margin = new W.Thickness(0, 8, 0, 10) };
            System.Windows.Automation.AutomationProperties.SetName(code, "Device Code"); content.Children.Add(code);
        }
        var actions = new C.WrapPanel { Margin = new W.Thickness(0, 8, 0, 16) };
        if (prompt.Url is not null)
        {
            actions.Children.Add(DialogSurface.Button("Open Browser", () => Open(prompt.Url), true));
            actions.Children.Add(DialogSurface.Button("Copy Link", () => System.Windows.Clipboard.SetText(prompt.Url)));
            if (prompt.Code is not null) actions.Children.Add(DialogSurface.Button("Copy Code", () => System.Windows.Clipboard.SetText(prompt.Code)));
        }
        content.Children.Add(actions);
        if (prompt.AcceptsPastedCode)
        {
            content.Children.Add(DialogSurface.Text("Authorization Code", 12));
            var code = new C.TextBox { MaxLength = 16384, Margin = new W.Thickness(0, 0, 0, 12) }; System.Windows.Automation.AutomationProperties.SetName(code, "Authorization Code"); content.Children.Add(code);
            var buttons = new C.WrapPanel();
            buttons.Children.Add(DialogSurface.Button("Continue", () => { if (string.IsNullOrWhiteSpace(code.Text)) return; result.TrySetResult(code.Text.Trim()); Close(); }, true));
            buttons.Children.Add(DialogSurface.Button("Cancel", Close)); content.Children.Add(buttons);
        }
        else
        {
            content.Children.Add(DialogSurface.Text("Waiting for your browser. This window closes when sign-in is complete.", 12, true));
            content.Children.Add(DialogSurface.Button("Cancel", Close));
        }
        DialogSurface.Host(this, new C.ScrollViewer { Content = content, VerticalScrollBarVisibility = C.ScrollBarVisibility.Auto }, display ?? new());
        FormClosed += (_, _) => result.TrySetResult(null);
        Shown += (_, _) => { if (prompt.Url is not null && !ct.IsCancellationRequested) Open(prompt.Url); };
        registration = ct.Register(() => { if (IsHandleCreated && !IsDisposed) BeginInvoke(() => Close()); result.TrySetCanceled(ct); });
    }
    private void Open(string url)
    {
        try { WindowsIntegration.OpenUrl(url); }
        catch (Exception error) { AppLog.Error("OpenBrowser", error); MessageBox.Show(this, "Could not open your browser. Use Copy Link to open the sign-in page.", "Sign-In"); }
    }
    protected override void Dispose(bool disposing) { if (disposing) registration.Dispose(); base.Dispose(disposing); }
}
