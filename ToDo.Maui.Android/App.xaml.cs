using ToDo.Maui.Android.Services;

namespace ToDo.Maui.Android;

public partial class App : Microsoft.Maui.Controls.Application {
    public App(DatabaseInitializer initializer) {
        InitializeComponent();

        try {
            initializer.InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine($"Database Init Failed: {ex.Message}");
        }
    }

    protected override Window CreateWindow(IActivationState? activationState) {
        return new Window(new MainPage()) {
            Title = "To Do"
        };
    }
}
