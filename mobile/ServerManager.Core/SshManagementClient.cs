using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace ServerManager.Core;

public sealed class SshManagementClient : IManagementClient
{
    public static readonly string[] Actions = ["status", "recover", "api-start", "api-stop", "api-restart", "logs-api", "logs-tunnel", "logs-startup"];
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };

    public static void Validate(Connection connection)
    {
        if (!IPAddress.TryParse(connection.Host, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("Enter the server phone's Tailscale IPv4 address.");
        var bytes = ip.GetAddressBytes();
        if (bytes[0] != 100 || bytes[1] < 64 || bytes[1] > 127)
            throw new ArgumentException("Use the server's Tailscale IP in the 100.64.0.0/10 range.");
        if (connection.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(connection.Username) || string.IsNullOrEmpty(connection.Password))
            throw new ArgumentException("Enter a valid SSH port, Termux username and SSH password.");
        if (!connection.Fingerprint.StartsWith("SHA256:", StringComparison.Ordinal) || connection.Fingerprint.Length != 50)
            throw new ArgumentException("Enter the complete SHA256 SSH fingerprint shown on the server phone.");
        if (!connection.ManagerRoot.StartsWith('/') || connection.ManagerRoot.Any(char.IsControl))
            throw new ArgumentException("Enter the absolute Server-management checkout path on the server phone.");
    }

    public static string Command(Connection connection, string action)
    {
        if (!Actions.Contains(action)) throw new ArgumentException("Unsupported management action.");
        static string Quote(string text) => "'" + text.Replace("'", "'\"'\"'") + "'";
        var root = connection.ManagerRoot.TrimEnd('/');
        return $"cd {Quote(root)} && {Quote(root + "/.venv/bin/python")} -m backend.server.mobile_bridge {action}";
    }

    public Task<BridgeResult> ExecuteAsync(Connection connection, string action)
    {
        Validate(connection);
        var command = Command(connection, action);
        return Task.Run(() => Execute(connection, command));
    }

    private static BridgeResult Execute(Connection connection, string command)
    {
        var info = new PasswordConnectionInfo(connection.Host, connection.Port, connection.Username, connection.Password)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        // Pin the same Ed25519 key shown by the setup command on the phone.
        foreach (var algorithm in info.HostKeyAlgorithms.Keys.Where(name => name != "ssh-ed25519").ToArray())
            info.HostKeyAlgorithms.Remove(algorithm);
        using var client = new SshClient(info);
        string? mismatch = null;
        client.HostKeyReceived += (_, args) =>
        {
            var actual = "SHA256:" + Convert.ToBase64String(SHA256.HashData(args.HostKey)).TrimEnd('=');
            args.CanTrust = actual == connection.Fingerprint.Trim();
            if (!args.CanTrust) mismatch = actual;
        };
        try
        {
            client.Connect();
            using var remote = client.CreateCommand(command);
            remote.CommandTimeout = TimeSpan.FromSeconds(95);
            var output = remote.Execute();
            BridgeResult? result;
            try { result = JsonSerializer.Deserialize<BridgeResult>(output, Json); }
            catch (JsonException) { throw new InvalidOperationException("The management bridge did not return valid data. Update the server checkout and install its dependencies."); }
            if (result is null || !result.Ok)
                throw new InvalidOperationException(result?.Error ?? "The management command failed.");
            if (remote.ExitStatus != 0) throw new InvalidOperationException("The server command did not finish successfully.");
            return result;
        }
        catch (SshException) when (mismatch is not null)
        {
            throw new InvalidOperationException($"SSH fingerprint mismatch. The server presented {mismatch}. Compare it on the server phone before changing the saved fingerprint.");
        }
        catch (SshAuthenticationException)
        {
            throw new InvalidOperationException("SSH authentication failed. Check the Termux username and password.");
        }
        catch (Exception error) when (error is SshException or System.Net.Sockets.SocketException)
        {
            throw new InvalidOperationException("Cannot reach Termux SSH. Check Tailscale on both devices, internet access and the SSH service.");
        }
    }
}
