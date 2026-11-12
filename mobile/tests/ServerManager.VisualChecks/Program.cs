using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerManager.Core;
using ServerManager.UI;
using ServerManager.UI.Components;

var output = Path.GetFullPath(args.FirstOrDefault() ?? "visual-results");
Directory.CreateDirectory(output);
var state = new ManagementState(new FixtureClient(), new FixtureStore());
using var services = new ServiceCollection().AddLogging().AddSingleton(state).BuildServiceProvider();
await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<ManagementRoot>());
await renderer.Dispatcher.InvokeAsync(() => state.RunAsync("status"));
foreach (var page in new[] { "Dashboard", "Services", "Connections", "Logs", "Settings" })
{
    await renderer.Dispatcher.InvokeAsync(() => state.Navigate(page));
    if(page == "Logs") await renderer.Dispatcher.InvokeAsync(() => state.RunAsync("logs-api"));
    var html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
    var heading = page == "Dashboard" ? "Server overview" : page == "Logs" ? "Recent logs" : page;
    if (!html.Contains($"<h1>{heading}</h1>")) throw new InvalidOperationException($"Missing page {page}");
    await File.WriteAllTextAsync(Path.Combine(output, page + ".html"),
        "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><link rel=\"stylesheet\" href=\"manager.css\"></head><body>" + html + "</body></html>");
}
Console.WriteLine("Rendered all five management pages with fixture data.");

sealed class FixtureStore : IConnectionStore
{
    public Task<Connection?> LoadAsync() => Task.FromResult<Connection?>(new("100.100.1.2", 8022, "u0_a252", "fixture-only", "SHA256:" + new string('a', 43), "/data/data/com.termux/files/home/server/apps/Server-management") { Name = "Atlas production" });
    public Task SaveAsync(Connection connection) => Task.CompletedTask;
    public void Clear() { }
}
sealed class FixtureClient : IManagementClient
{
    public Task<BridgeResult> ExecuteAsync(Connection connection, string action) => action.StartsWith("logs-")
        ? Task.FromResult(new BridgeResult(true, null, null, "api.log\n21:42:08  INFO   GET /api/v1/health 200\n21:41:52  INFO   API ready\n21:39:17  WARN   Connection interrupted\n21:38:10  INFO   Connection restored"))
        : Task.FromResult(new BridgeResult(true, null, new ServerStatus(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), true, 1234, true, true,
            new HostMetrics(new Storage(4L << 30, 1L << 30), new Storage(64L << 30, 24L << 30), 180000), Hostname: "samsung-a12", ApiVersion: "0.10.0", ApiUptimeSeconds: 180000, ApiMemoryBytes: 123731968, ApiCpuPercent: 2.4, ApiPort: 8000), null));
}
