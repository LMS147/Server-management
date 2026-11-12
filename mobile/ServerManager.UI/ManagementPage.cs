using Microsoft.AspNetCore.Components;
namespace ServerManager.UI;
public abstract class ManagementPage : ComponentBase
{
    [Parameter, EditorRequired] public ManagementState State { get; set; } = default!;
    protected static string Bytes(long bytes) => $"{bytes / 1073741824d:0.0} GB";
}
