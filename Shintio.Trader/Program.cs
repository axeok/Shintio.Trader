using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Configuration;
using Shintio.Trader.Database.Contexts;
using Shintio.Trader.Services;

#region Builder

var appBuilder = WebApplication.CreateBuilder();

appBuilder.Services.Configure<TelegramSecrets>(appBuilder.Configuration.GetSection("Telegram"));

appBuilder.Services.AddControllers();

appBuilder.Services
    .AddPooledDbContextFactory<AppDbContext>((serviceProvider, builder) =>
    {
        var connection = serviceProvider.GetRequiredService<IConfiguration>()
            .GetConnectionString("AppDbContext");

        builder.UseMySql(connection, ServerVersion.AutoDetect(connection))
            .UseLazyLoadingProxies()
            .ConfigureWarnings(b => b.Log(
                (RelationalEventId.CommandExecuted, LogLevel.Debug),
                (RelationalEventId.ConnectionOpened, LogLevel.Debug),
                (RelationalEventId.ConnectionClosed, LogLevel.Debug)));
    });

appBuilder.Services.AddMessagePipe();

appBuilder.Services.AddHostedService<AppService>();
appBuilder.Services.AddHostedService<TelegramUserBotService>();

#endregion

#region App

var app = appBuilder.Build();

app.UseHttpsRedirection();
// app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
    
app.MapGet("/", () => Results.Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow }));

await using (var context = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
{
    await context.Database.MigrateAsync();
}

await app.RunAsync();

#endregion