using ServerManager.Core;
using ServerManager.UI;
using Xunit;
namespace ServerManager.Core.Tests;

public sealed class RecoveryTests
{
    private static Connection Valid => new("100.100.1.2", 8022, "u0_a252", "private-password", "SHA256:" + new string('a', 43), "/data/data/com.termux/files/home/server/apps/Server-management");
    [Fact] public void CommandsQuotePathsAndRejectArbitraryActions()
    {
        var command = SshManagementClient.Command(Valid with { ManagerRoot = "/home/a'b;touch /tmp/unwanted" }, "status");
        Assert.Contains("'\"'\"'", command);
        Assert.EndsWith("backend.server.mobile_bridge status", command);
        Assert.DoesNotContain("private-password", command);
        Assert.Throws<ArgumentException>(() => SshManagementClient.Command(Valid, "status; reboot"));
    }
    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("100.128.0.1")]
    public void RecoveryRequiresATailscaleAddress(string host) => Assert.Throws<ArgumentException>(() => SshManagementClient.Validate(Valid with { Host = host }));
    [Fact] public void FingerprintIsRequired() => Assert.Throws<ArgumentException>(() => SshManagementClient.Validate(Valid with { Fingerprint = "" }));
    [Fact] public async Task DestructiveActionsRequireConfirmationAndDoNotReplay()
    {
        var client = new FakeClient(); var store = new Store(); var state = new ManagementState(client, store);
        await state.SaveAsync(Valid, true);
        await state.RunAsync("api-stop");
        Assert.Empty(client.Calls);
        state.Confirm = _ => Task.FromResult(true);
        await state.RunAsync("api-stop");
        Assert.Equal(new[] { "api-stop" }, client.Calls);
        client.Fail = true;
        await state.RunAsync("recover");
        Assert.Equal(new[] { "api-stop", "recover" }, client.Calls);
        Assert.False(state.Reachable); Assert.NotNull(state.Error);
    }
    [Fact] public async Task ForgetClearsCredentialsAndLocalLogContent()
    {
        var store = new Store(); var state = new ManagementState(new FakeClient(), store);
        await state.SaveAsync(Valid, true); await state.RunAsync("logs-api");
        state.Confirm = _ => Task.FromResult(true); await state.ForgetAsync();
        Assert.Null(store.Value); Assert.Null(state.Connection); Assert.Equal("No connection.", state.Logs);
    }
    private sealed class Store : IConnectionStore
    {
        public Connection? Value;
        public Task<Connection?> LoadAsync() => Task.FromResult(Value);
        public Task SaveAsync(Connection connection) { Value = connection; return Task.CompletedTask; }
        public void Clear() => Value = null;
    }
    private sealed class FakeClient : IManagementClient
    {
        public List<string> Calls = []; public bool Fail;
        public Task<BridgeResult> ExecuteAsync(Connection connection, string action)
        {
            Calls.Add(action);
            if (Fail) throw new IOException("interrupted");
            return Task.FromResult(new BridgeResult(true, null, null, "example log"));
        }
    }
}
