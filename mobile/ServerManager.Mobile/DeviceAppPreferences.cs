using ServerManager.Core;
namespace ServerManager.Mobile;
public sealed class DeviceAppPreferences : IAppPreferences
{
    public bool StatusAlerts
    {
        get => Preferences.Default.Get("server-manager.status-alerts", true);
        set => Preferences.Default.Set("server-manager.status-alerts", value);
    }
}
