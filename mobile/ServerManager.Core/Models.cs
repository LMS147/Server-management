namespace ServerManager.Core;

public sealed record Connection(string Host, int Port, string Username, string Password, string Fingerprint, string ManagerRoot);
public sealed record Storage(long TotalBytes, long FreeBytes);
public sealed record HostMetrics(Storage? Ram, Storage Disk, long? UptimeSeconds);
public sealed record ServerStatus(long SampledAt, bool ApiHealthy, int? ApiPid, bool TunnelRunning, bool SshRunning, HostMetrics Host);
public sealed record BridgeResult(bool Ok, string? Error, ServerStatus? Status, string? LogText);
public interface IConnectionStore
{
    Task<Connection?> LoadAsync();
    Task SaveAsync(Connection connection);
    void Clear();
}
public interface IManagementClient
{
    Task<BridgeResult> ExecuteAsync(Connection connection, string action);
}
