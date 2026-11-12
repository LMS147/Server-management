using ServerManager.Core;
namespace ServerManager.UI;

public sealed class ManagementState(IManagementClient client, IConnectionStore store)
{
    public event Action? Changed;
    public string Page { get; private set; } = "Dashboard";
    public Connection? Connection { get; private set; }
    public ServerStatus? Status { get; private set; }
    public bool Busy { get; private set; }
    public bool Reachable { get; private set; }
    public string? Error { get; private set; }
    public string? Notice { get; private set; }
    public string Logs { get; private set; } = "Select a log and tap Load logs.";
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
            Connection = connection; Status = null; Reachable = false; Logs = "Select a log and tap Load logs.";
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
            if (result.Status is not null) { Status = result.Status; LastChecked = DateTimeOffset.Now; }
            if (result.LogText is not null) Logs = result.LogText;
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
        try { store.Clear(); Connection = null; Status = null; Reachable = false; LastChecked = null; Logs = "No connection."; Notice = "Connection removed."; }
        catch { Error = "The saved connection could not be removed."; }
        Changed?.Invoke();
    }
}
