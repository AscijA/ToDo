using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace ToDo.Maui.Native;

public static class MauiProgram {
    public static MauiApp CreateMauiApp() {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        string? syncfusionKey = builder.Configuration["SyncfusionLicenseKey"];

        if (!string.IsNullOrEmpty(syncfusionKey)) {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
        }
        else {
            // Optional: Warn your dev team if they forgot to set their User Secrets
            Console.WriteLine("Warning: Syncfusion License Key is missing from configuration.");
        }
#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
