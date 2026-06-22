using ToDo.Maui.Windows.Services;
using ToDo.RazorLib.Services;

namespace ToDo.Maui.Windows;

public partial class App : Microsoft.Maui.Controls.Application {
    private readonly SyncStartupService syncStartupService;

    public App(DatabaseInitializer initializer, SyncStartupService syncStartupService) {
        this.syncStartupService = syncStartupService;
        InitializeComponent();

        try {
            initializer.InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) {
            Console.WriteLine($"Database Init Failed: {ex.Message}");
        }

        RunSyncStartupInBackground("Sync Startup Failed");
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

    protected override void OnResume() {
        RunSyncStartupInBackground("Sync Resume Failed");
    }

    private void RunSyncStartupInBackground(string errorPrefix) {
        _ = Task.Run(async () => {
            try {
                await syncStartupService.InitializeAsync();
            }
            catch (Exception ex) {
                Console.WriteLine($"{errorPrefix}: {ex.Message}");
            }
        });
    }
}
