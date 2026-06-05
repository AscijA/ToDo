using global::Android.App;
using global::Android.Runtime;

namespace ToDo.Maui.Android;

[Application]
public class MainApplication : MauiApplication {
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership) {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
