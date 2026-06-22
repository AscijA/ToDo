using ToDo.Maui.Android.Services;
using ToDo.RazorLib.Services;

namespace ToDo.Maui.Android;

public partial class App : Microsoft.Maui.Controls.Application {
    private readonly SyncStartupService syncStartupService;

    public App(DatabaseInitializer initializer, SyncStartupService syncStartupService) {
        this.syncStartupService = syncStartupService;
        InitializeComponent();

        try {
            initializer.InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine($"Database Init Failed: {ex.Message}");
        }

        try {
            syncStartupService.InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine($"Sync Startup Failed: {ex.Message}");
        }
    }

    protected override Window CreateWindow(IActivationState? activationState) {
        return new Window(new MainPage()) {
            Title = "To Do"
        };
    }

    protected override void OnResume() {
        _ = Task.Run(async () => {
            try {
                await syncStartupService.InitializeAsync();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Sync Resume Failed: {ex.Message}");
            }
        });
    }
}
