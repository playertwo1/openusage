using W = System.Windows;
using C = System.Windows.Controls;
using A = System.Windows.Automation.AutomationProperties;

namespace OpenUsage.Windows;

internal sealed class AddAccountForm : Form
{
    private readonly C.ComboBox provider = new() { MinHeight = 40 };
    private readonly C.TextBox label = new() { MaxLength = 100 };
    private readonly C.PasswordBox key = new() { MaxLength = 32768 };
    private readonly C.StackPanel keyBlock = new() { Margin = new W.Thickness(0, 16, 0, 0) };
    private readonly C.TextBlock status = DialogSurface.Text("", 11, true);
    private readonly C.Button signIn;
    private readonly ProviderLogin login;
    private readonly AccountStore store;
    private CancellationTokenSource? signingIn;
    private readonly CancellationTokenSource lifetime = new();

    public AddAccountForm(AccountStore store, Api api)
    {
        this.store = store; login = new ProviderLogin(api);
        Text = "Add Account"; AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(430, 470); FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
        var content = new C.StackPanel { Margin = new W.Thickness(24) };
        var heading = DialogSurface.Text("Add An Account", 21); heading.FontWeight = W.FontWeights.SemiBold; content.Children.Add(heading);
        content.Children.Add(DialogSurface.Text("Connect a provider to bring its usage into your dashboard.", 12, true));
        content.Children.Add(DialogSurface.Text("Provider", 12));
        provider.SetResourceReference(W.FrameworkElement.StyleProperty, "Choice"); A.SetName(provider, "Provider");
        foreach (var kind in Enum.GetValues<ProviderKind>())
        {
            provider.Items.Add(new C.ComboBoxItem { Content = kind.Name(), Tag = kind });
        }
        content.Children.Add(provider);
        var nameLabel = DialogSurface.Text("Account Name (Optional)", 12); nameLabel.Margin = new W.Thickness(0, 18, 0, 8); content.Children.Add(nameLabel);
        A.SetName(label, "Account Name"); content.Children.Add(label);
        keyBlock.Children.Add(DialogSurface.Text("API Key", 12)); A.SetName(key, "API Key"); keyBlock.Children.Add(key);
        var keyPage = DialogSurface.Button("Create An API Key", () =>
        {
            try { WindowsIntegration.OpenUrl(Selected.KeyPage()!); }
            catch (Exception error) { AppLog.Error("KeyPage", error); status.Text = AppLog.Friendly(error); }
        });
        keyPage.HorizontalAlignment = W.HorizontalAlignment.Left; keyPage.Margin = new W.Thickness(0, 5, 0, 0); keyBlock.Children.Add(keyPage); content.Children.Add(keyBlock);
        status.Margin = new W.Thickness(0, 16, 0, 16); content.Children.Add(status);
        var buttons = new C.WrapPanel(); signIn = DialogSurface.Button("Sign In", () => _ = SignInAsync(), true); buttons.Children.Add(signIn); buttons.Children.Add(DialogSurface.Button("Cancel", Close)); content.Children.Add(buttons);
        DialogSurface.Host(this, new C.ScrollViewer { Content = content, VerticalScrollBarVisibility = C.ScrollBarVisibility.Auto }, store.State.Display);
        provider.SelectionChanged += (_, _) =>
        {
            keyBlock.Visibility = Selected.UsesApiKey() ? W.Visibility.Visible : W.Visibility.Collapsed;
            signIn.Content = Selected.UsesApiKey() ? "Add Account" : "Sign In";
            status.Text = Selected.UsesApiKey() ? "Your key is encrypted for your Windows account." : "Continue in your browser. OpenUsage reads no credentials from other apps.";
        };
        provider.SelectedIndex = 0;
        FormClosing += (_, _) => lifetime.Cancel();
    }
    private ProviderKind Selected => (ProviderKind)((C.ComboBoxItem)provider.SelectedItem).Tag;
    private async Task SignInAsync()
    {
        if (signingIn is not null) return;
        var kind = Selected;
        if (kind.UsesApiKey() && !AccountStore.ValidSecret(key.Password.Trim())) { status.Text = "Enter a valid API key without line breaks."; return; }
        signingIn = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); signingIn.CancelAfter(TimeSpan.FromMinutes(15));
        provider.IsEnabled = signIn.IsEnabled = key.IsEnabled = label.IsEnabled = false; status.Text = "Connecting your account…";
        try
        {
            var result = kind.UsesApiKey() ? new LoginResult(kind.Name() + " Account", new Credential(key.Password.Trim())) : await login.SignInAsync(kind, PromptAsync, signingIn.Token);
            signingIn.Token.ThrowIfCancellationRequested();
            var name = string.IsNullOrWhiteSpace(label.Text) ? result.Label : label.Text.Trim();
            store.Add(new Account(Guid.NewGuid(), kind, name, result.Credential)); DialogResult = DialogResult.OK; Close();
        }
        catch (OperationCanceledException) { if (!IsDisposed) status.Text = "Sign-in cancelled. You can try again."; }
        catch (Exception error) { AppLog.Error("SignIn", error); if (!IsDisposed) status.Text = AppLog.Friendly(error); }
        finally { signingIn.Dispose(); signingIn = null; if (!IsDisposed) provider.IsEnabled = signIn.IsEnabled = key.IsEnabled = label.IsEnabled = true; }
    }
    private Task<string?> PromptAsync(LoginPrompt prompt, CancellationToken ct)
    {
        if (ct.IsCancellationRequested || IsDisposed) return Task.FromCanceled<string?>(ct.IsCancellationRequested ? ct : new CancellationToken(true));
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        void ShowPrompt()
        {
            if (IsDisposed || ct.IsCancellationRequested) { completion.TrySetCanceled(ct); return; }
            var form = new LoginPromptForm(prompt, ct, store.State.Display); form.Show(this); _ = FinishAsync();
            async Task FinishAsync()
            {
                try { completion.TrySetResult(await form.Result); }
                catch (OperationCanceledException) { completion.TrySetCanceled(ct); }
                finally { if (!form.IsDisposed) form.Close(); form.Dispose(); }
            }
        }
        if (InvokeRequired) BeginInvoke(ShowPrompt); else ShowPrompt(); return completion.Task;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { lifetime.Cancel(); lifetime.Dispose(); }
        base.Dispose(disposing);
    }
}
