using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using ToDo.Application.Interfaces.Services;
using ToDo.Application.Services;
using ToDo.Infrastructure.Data;
using ToDo.Maui.Android.Services;
using ToDo.RazorLib.Services;

namespace ToDo.Maui.Android;

public static class MauiProgram {
    public static MauiApp CreateMauiApp() {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMudServices();
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
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
        builder.Services.AddSingleton<ISettingsService, AndroidSettingsService>();

        return builder.Build();
    }
}
