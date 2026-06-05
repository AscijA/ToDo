using ToDo.Maui.Windows.Services;

namespace ToDo.Maui.Windows;

public partial class App : Microsoft.Maui.Controls.Application {
    public App(DatabaseInitializer initializer) {
        InitializeComponent();

        try {
            initializer.InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) {
            Console.WriteLine($"Database Init Failed: {ex.Message}");
        }
    }

    protected override Window CreateWindow(IActivationState? activationState) {
        return new Window(new MainPage()) {
            Title = "To Do",
            Width = 1366,
            Height = 768,
            TitleBar = new TitleBar {
                Title = "To Do",
                Icon = "Resources/AppIcon/appicon.ico"
            }
        };
    }
}
