using ToDo.Maui.Services;

namespace ToDo.Maui;

public partial class App : Microsoft.Maui.Controls.Application {
    public App(DatabaseInitializer initializer) {
        InitializeComponent();
        Task.Run(async () => {
            try {
                await initializer.InitializeAsync();
            }
            catch (Exception ex) {
                Console.WriteLine($"Database Init Failed: {ex.Message}");
            }
        });
    }

    protected override Window CreateWindow(IActivationState? activationState) {
        return new Window(new MainPage()) {
            Title = "ToDo",
            Width = 1366,
            Height = 768
        };
    }
}
