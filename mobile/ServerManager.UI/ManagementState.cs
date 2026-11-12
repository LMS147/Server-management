using ServerManager.Core;
namespace ServerManager.UI;

public sealed class ManagementState(IManagementClient client, IConnectionStore store, IAppPreferences? preferences = null)
{
    public event Action? Changed;
    private bool statusAlerts = preferences?.StatusAlerts ?? true;
    public bool StatusAlerts => statusAlerts;
    public bool IsActive { get; set; } = true;
    public string AppVersion { get; set; } = "1.1.0 (2)";
    public Func<string, Task<bool>> OpenLink { get; set; } = _ => Task.FromResult(false);
    public Func<string, Task>? CopyText { get; set; }

    public void ToggleStatusAlerts()
    {
        try { var next = !statusAlerts; if(preferences is not null) preferences.StatusAlerts = next; statusAlerts = next; }
        catch { Error = "The alert preference could not be saved."; }
        Changed?.Invoke();
    }
    public async Task CopyLogsAsync()
    {
        try { if(CopyText is null) throw new InvalidOperationException(); await CopyText(Logs); Notice = "Displayed logs copied."; }
        catch { Error = "Clipboard is unavailable in this host."; }
        Changed?.Invoke();
    }
    public Task OpenDocumentationAsync() => OpenAsync("https://github.com/LMS147/Server-management/blob/main/mobile/README.md");
    public Task OpenSourceAsync() => OpenAsync("https://github.com/LMS147/Server-management");
    public Task OpenUpdatesAsync() => OpenAsync("https://github.com/LMS147/Server-management/releases");
    private async Task OpenAsync(string url)
    {
        try { if(!await OpenLink(url)) Error = "This link could not be opened."; }
        catch { Error = "This link could not be opened."; }
        Changed?.Invoke();
    }
    public string Page { get; private set; } = "Dashboard";
    public Connection? Connection { get; private set; }
    public ServerStatus? Status { get; private set; }
    public bool Busy { get; private set; }
    public bool Reachable { get; private set; }
    public string? Error { get; private set; }
    public string? Notice { get; private set; }
    public string Logs { get; private set; } = "Select a log and tap Load logs.";
    public string LogSource { get; private set; } = "";
    public DateTimeOffset? LastChecked { get; private set; }
    public Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(false);
    public void Navigate(string page) { if (!Busy) { Page = page; Changed?.Invoke(); } }

    public async Task InitializeAsync()
    {
        try { Connection = await store.LoadAsync(); }
        catch { Error = "Saved connection could not be restored. Enter it again on Connections."; }
        Changed?.Invoke();
    }

    public async Task SaveAsync(Connection connection, bool remember)
    {
        if (Busy) return;
        Busy = true;
        try
        {
            SshManagementClient.Validate(connection);
            if (remember) await store.SaveAsync(connection); else store.Clear();
            Connection = connection; Status = null; Reachable = false; LastChecked = null; LogSource = ""; Logs = "Select a log and tap Load logs.";
            Error = null; Notice = "Connection saved. Check the server from Dashboard.";
        }
        catch (Exception error) { Error = error is ArgumentException ? error.Message : "The connection could not be saved securely."; }
        finally { Busy = false; Changed?.Invoke(); }
    }

    public async Task RunAsync(string action)
    {
        if (Busy) return;
        if (Connection is null) { Navigate("Connections"); Error = "Configure the Tailscale SSH connection first."; Changed?.Invoke(); return; }
        if (action is "api-stop" or "api-restart" or "recover" or "api-start")
        {
            var message = action == "api-stop" ? "Stop the Atlas API? Active chat requests may be interrupted."
                : action == "api-restart" ? "Restart the Atlas API? Active chat requests may be interrupted."
                : action == "recover" ? "Start Atlas if unavailable and start the tunnel if no connector is running?"
                : "Start the Atlas API?";
            if (!await Confirm(message)) return;
        }
        if (Busy) return;
        Busy = true; Error = null; Notice = null; Changed?.Invoke();
        try
        {
            var result = await client.ExecuteAsync(Connection, action);
            if (!result.Ok) throw new InvalidOperationException(result.Error ?? "The operation failed.");
            Reachable = true;
            if (result.Status is not null)
            {
                if(statusAlerts && Status is { } previous &&
                   (previous.ApiHealthy != result.Status.ApiHealthy || previous.TunnelRunning != result.Status.TunnelRunning))
                    Notice = "Service status changed. Review the latest server result.";
                Status = result.Status; LastChecked = DateTimeOffset.Now;
            }
            if (result.LogText is not null) { Logs = result.LogText; LogSource = action.StartsWith("logs-") ? action[5..] : ""; }
            if (action != "status" && !action.StartsWith("logs-")) Notice = "Action completed. Check the reported API and tunnel state.";
        }
        catch (Exception error)
        {
            Reachable = false;
            Error = error is InvalidOperationException or ArgumentException ? error.Message : "The connection was interrupted. Check status before repeating an action.";
        }
        finally { Busy = false; Changed?.Invoke(); }
    }

    public async Task ForgetAsync()
    {
        if (Busy || !await Confirm("Remove the saved SSH connection from this device?")) return;
        try { store.Clear(); Connection = null; Status = null; Reachable = false; LastChecked = null; LogSource = ""; Logs = "No connection."; Notice = "Connection removed."; }
        catch { Error = "The saved connection could not be removed."; }
        Changed?.Invoke();
    }
}
