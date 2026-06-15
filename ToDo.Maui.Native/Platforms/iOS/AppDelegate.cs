using Foundation;

namespace ToDo.Maui.Native;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate {
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
