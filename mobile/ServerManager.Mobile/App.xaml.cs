using ServerManager.UI;
namespace ServerManager.Mobile;
public partial class App : Application
{
    private readonly MainPage page;
    private readonly ManagementState state;
    public App(MainPage page, ManagementState state) { InitializeComponent(); this.page = page; this.state = state; }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(page) { Title = "Server management" };
        window.Stopped += (_, _) => state.IsActive = false;
        window.Deactivated += (_, _) => state.IsActive = false;
        window.Activated += (_, _) => state.IsActive = true;
        window.Resumed += (_, _) => state.IsActive = true;
        return window;
    }
}
