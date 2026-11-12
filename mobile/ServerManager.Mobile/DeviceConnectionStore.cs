using System.Text.Json;
using ServerManager.Core;
namespace ServerManager.Mobile;
public sealed class DeviceConnectionStore : IConnectionStore
{
    private const string Key = "server-manager.ssh.v1";
    public async Task<Connection?> LoadAsync()
    {
        var text = await SecureStorage.Default.GetAsync(Key);
        return text is null ? null : JsonSerializer.Deserialize<Connection>(text);
    }
    public Task SaveAsync(Connection connection) => SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(connection));
    public void Clear() => SecureStorage.Default.Remove(Key);
}
