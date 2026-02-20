using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ToDo.Infrastructure.Data;
using ToDo.Maui.Services;

namespace ToDo.Maui;

public static class MauiProgram {
    public static MauiApp CreateMauiApp() {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "todo.db3");

        builder.Services.AddDbContext<TodoDbContext>(options =>
             options.UseSqlite($"Data Source={dbPath}"));

        builder.Services.AddSingleton<DatabaseInitializer>();

        //builder.Services.AddScoped<ITaskService, TaskService>();

        return builder.Build();
    }
}
