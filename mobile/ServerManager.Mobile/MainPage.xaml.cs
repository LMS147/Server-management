using ServerManager.UI;
using Microsoft.AspNetCore.Components.WebView;
namespace ServerManager.Mobile;
public partial class MainPage : ContentPage
{
    private readonly ManagementState state;
    public MainPage(ManagementState state)
    {
        InitializeComponent(); this.state = state;
        state.Confirm = message => MainThread.InvokeOnMainThreadAsync(() => DisplayAlertAsync("Confirm service action", message, "Continue", "Cancel"));
    }
    protected override bool OnBackButtonPressed()
    {
        if (state.Busy) return true;
        if (state.Page != "Dashboard") { state.Navigate("Dashboard"); return true; }
        return base.OnBackButtonPressed();
    }
    private void OnUrlLoading(object? sender, UrlLoadingEventArgs e)
    {
        if (e.Url.Host != "0.0.0.1") e.UrlLoadingStrategy = UrlLoadingStrategy.CancelLoad;
    }
}
