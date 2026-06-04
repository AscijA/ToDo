using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using ToDo.Application.Interfaces.Services;
using ToDo.Application.Services;
using ToDo.Infrastructure.Data;
using ToDo.Maui.Services;
using ToDo.RazorLib.Services;

namespace ToDo.Maui;

public static class MauiProgram {
    public static MauiApp CreateMauiApp() {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });
        builder.Services.AddMudServices();
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "todo.db3");

        builder.Services.AddDbContextFactory<TodoDbContext>(options =>
             options.UseSqlite($"Data Source={dbPath}"));
        //System.Diagnostics.Debug.WriteLine($"Database path: {dbPath}");
        builder.Services.AddSingleton<DatabaseInitializer>();
        builder.Services.AddTransient<IDailyPlanService, DailyPlanService>();
        builder.Services.AddTransient<IDailyOccurrenceService, DailyOccurrenceService>();
        builder.Services.AddTransient<IWeeklyOccurrenceService, WeeklyOccurrenceService>();
        builder.Services.AddTransient<IMonthlyOccurrenceService, MonthlyOccurrenceService>();
        builder.Services.AddSingleton<ISettingsService, MauiSettingsService>();
        return builder.Build();
    }
}
