using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using System.Drawing;
using ToDo.RazorLib.Services;
using WinUiControls = Microsoft.UI.Xaml.Controls;

namespace ToDo.Maui.Windows.Platforms.Windows;

internal static class TrayWindowService {
    private static Microsoft.UI.Xaml.Window? window;
    private static AppWindow? appWindow;
    private static TaskbarIcon? taskbarIcon;
    private static Icon? icon;
    private static bool isExiting;

    public static void Initialize(Microsoft.UI.Xaml.Window createdWindow, AppWindow createdAppWindow, IntPtr createdHwnd, string iconPath) {
        window = createdWindow;
        appWindow = createdAppWindow;

        taskbarIcon?.Dispose();
        icon?.Dispose();

        icon = new Icon(iconPath);

        taskbarIcon = new TaskbarIcon {
            ToolTipText = "To Do",
            Icon = icon,
            NoLeftClickDelay = true,
            ContextFlyout = CreateTrayMenu(),
            DoubleClickCommand = new Command(RestoreWindow)
        };

        taskbarIcon.ForceCreate();
    }

    public static void HideWindow() {
        if (!isExiting) {
            appWindow?.Hide();
        }
    }

    public static void RestoreWindow() {
        appWindow?.Show();
        window?.Activate();
    }

    private static WinUiControls.MenuFlyout CreateTrayMenu() {
        var menu = new WinUiControls.MenuFlyout();

        menu.Items.Add(new WinUiControls.MenuFlyoutItem {
            Text = "Show",
            Command = new Command(RestoreWindow)
        });
        menu.Items.Add(new WinUiControls.MenuFlyoutItem {
            Text = "Sync",
            Command = new Command(Sync)
        });
        menu.Items.Add(new WinUiControls.MenuFlyoutSeparator());
        menu.Items.Add(new WinUiControls.MenuFlyoutItem {
            Text = "Exit",
            Command = new Command(ExitApplication)
        });

        return menu;
    }

    private static void Sync() {
        RestoreWindow();

        var services = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services;
        services?.GetService<SyncModalRequestService>()?.RequestOpen();
    }

    public static void ExitApplication() {
        isExiting = true;

        if (window?.DispatcherQueue is { } dispatcherQueue) {
            dispatcherQueue.TryEnqueue(FinishExit);
            return;
        }

        FinishExit();
    }

    private static void FinishExit() {
        taskbarIcon?.Dispose();
        icon?.Dispose();

        taskbarIcon = null;
        icon = null;

        appWindow?.Destroy();
        Microsoft.Maui.Controls.Application.Current?.Quit();
        Microsoft.UI.Xaml.Application.Current.Exit();
    }
}
