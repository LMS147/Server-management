using Microsoft.Extensions.DependencyInjection;
using ServerManager.Core;
using ServerManager.UI;
namespace ServerManager.Mobile;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<IManagementClient, SshManagementClient>();
        builder.Services.AddSingleton<IConnectionStore, DeviceConnectionStore>();
        builder.Services.AddSingleton<IAppPreferences, DeviceAppPreferences>();
        builder.Services.AddSingleton<ManagementState>();
        builder.Services.AddSingleton<MainPage>();
        return builder.Build();
    }
}
