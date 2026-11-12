using System.Globalization;
using Microsoft.AspNetCore.Components;
using ServerManager.Core;
namespace ServerManager.UI;
public abstract class ManagementPage : ComponentBase
{
    [Parameter, EditorRequired] public ManagementState State { get; set; } = default!;
    protected static string Bytes(long bytes) => bytes < 1073741824L ? $"{bytes / 1048576d:0} MB" : $"{bytes / 1073741824d:0.#} GB";
    protected static double Used(Storage? storage) => storage is null || storage.TotalBytes <= 0 ? 0 : Math.Clamp(100d * (storage.TotalBytes - storage.FreeBytes) / storage.TotalBytes, 0, 100);
    protected static string Bar(Storage? storage) => Used(storage).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    protected static string Duration(long? seconds) => seconds is null ? "Unavailable" : $"{seconds / 86400}d  {seconds / 3600 % 24}h  {seconds / 60 % 60}m";
    protected string Checked => State.LastChecked is null ? "Not checked" : State.Reachable ? State.LastChecked.Value.ToString("HH:mm") : "Previous result";
}
