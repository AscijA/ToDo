using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.LifecycleEvents;
using MudBlazor.Services;
using ToDo.Application.Interfaces.Services;
using ToDo.Application.Services;
using ToDo.Infrastructure.Data;
using ToDo.Maui.Windows.Platforms.Windows;
using ToDo.Maui.Windows.Services;
using ToDo.RazorLib.Services;
using Microsoft.Windows.AppLifecycle;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace ToDo.Maui.Windows;

public static class MauiProgram {
    public static MauiApp CreateMauiApp() {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.ConfigureLifecycleEvents(events => {
            events.AddWindows(windows => {
                windows.OnWindowCreated(window => {
                    var hwnd = WindowNative.GetWindowHandle(window);
                    var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                    var appWindow = AppWindow.GetFromWindowId(windowId);

                    const string appTitle = "To Do";
                    window.Title = appTitle;
                    appWindow.Title = appTitle;

                    var iconPath = GetWindowsIconPath();
                    appWindow.SetIcon(iconPath);
                    TrayWindowService.Initialize(window, appWindow, iconPath);

                    if (ShouldStartMinimized()) {
                        window.DispatcherQueue.TryEnqueue(() => MinimizeWindow(appWindow));
                    }

                    appWindow.Closing += (sender, args) => {
                        var mauiApp = Microsoft.Maui.Controls.Application.Current;
                        var settings = mauiApp?.Handler?.MauiContext?.Services.GetService<ISettingsService>();
                        var minimizeToTray = settings?.Get("MinimizeToTray", "false") == "true";

                        if (minimizeToTray) {
                            args.Cancel = true;
                            TrayWindowService.HideWindow();
                        }
                    };
                });
            });
        });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "todo.db3");

        builder.Services.AddDbContextFactory<TodoDbContext>(options =>
             options.UseSqlite($"Data Source={dbPath}"));
        builder.Services.AddSingleton<DatabaseInitializer>();
        builder.Services.AddTransient<IDailyPlanService, DailyPlanService>();
        builder.Services.AddTransient<IDailyOccurrenceService, DailyOccurrenceService>();
        builder.Services.AddTransient<IWeeklyOccurrenceService, WeeklyOccurrenceService>();
        builder.Services.AddTransient<IMonthlyOccurrenceService, MonthlyOccurrenceService>();
        builder.Services.AddTransient<ITaskListService, TaskListService>();
        builder.Services.AddSingleton<ISettingsService, MauiSettingsService>();
        builder.Services.AddSingleton<IStartupSettingsService, WindowsStartupSettingsService>();
        builder.Services.AddSingleton<ISecureSettingsService, MauiSecureSettingsService>();
        builder.Services.AddSingleton<IAppVersionService>(_ => new AppVersionService(AppInfo.Current.Version.ToString()));
        builder.Services.AddSingleton<SyncDataRefreshService>();
        builder.Services.AddSingleton<SyncChangeTracker>();
        builder.Services.AddSingleton<SyncActivityLogService>();
        builder.Services.AddSingleton<ISyncDiscoveryService, ManualAddressSyncDiscoveryService>();
        builder.Services.AddSingleton<ISyncSnapshotService, SyncSnapshotService>();
        builder.Services.AddSingleton<ISyncSnapshotImportService, SyncSnapshotImportService>();
        builder.Services.AddSingleton<ISyncTransportService, LocalHttpSyncTransportService>();
        builder.Services.AddSingleton<ISyncService, LocalSyncService>();
        builder.Services.AddSingleton<SyncStartupService>();
        builder.Services.AddSingleton<AutoSyncChangeNotifier>();
        builder.Services.AddSingleton<IDataChangeNotifier>(provider => provider.GetRequiredService<AutoSyncChangeNotifier>());
        builder.Services.AddSingleton<SyncModalRequestService>();
        builder.Services.AddScoped<ThemeStoreInterop>();

        return builder.Build();
    }

    private static string GetWindowsIconPath() {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[] {
            Path.Combine(baseDirectory, "appicon.ico"),
            Path.Combine(baseDirectory, "Resources", "AppIcon", "appicon.ico"),
            Path.Combine(baseDirectory, "AppX", "appicon.ico")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static bool ShouldStartMinimized() {
        var mauiApp = Microsoft.Maui.Controls.Application.Current;
        var settings = mauiApp?.Handler?.MauiContext?.Services.GetService<ISettingsService>();
        var startupMinimized = settings?.Get("StartupMinimized", "false") == "true";

        return startupMinimized && IsStartupTaskActivation();
    }

    private static bool IsStartupTaskActivation() {
        try {
            return AppInstance.GetCurrent().GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask;
        }
        catch {
            return false;
        }
    }

    private static void MinimizeWindow(AppWindow appWindow) {
        if (appWindow.Presenter is OverlappedPresenter presenter) {
            presenter.Minimize();
        }
    }
}
