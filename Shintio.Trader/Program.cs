using System.Text.Json;
using System.Text.Json.Nodes;
using Binance.Net.Clients;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shintio.Trader.Configuration;
using Shintio.Trader.Database.Contexts;
using Shintio.Trader.Interfaces;
using Shintio.Trader.Services;
using Shintio.Trader.Services.Background;
using Shintio.Trader.Services.Strategies;
using Telegram.Bot;

#region Builder

// var appBuilder = WebApplication.CreateBuilder();
var appBuilder = Host.CreateApplicationBuilder();

appBuilder.Services.Configure<TelegramSecrets>(appBuilder.Configuration.GetSection("Telegram"));
appBuilder.Services.Configure<BinanceSecrets>(appBuilder.Configuration.GetSection("Binance"));
appBuilder.Services.Configure<TraderConfig>(appBuilder.Configuration.GetSection("Trader"));

// appBuilder.Services.AddControllers();

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

appBuilder.Services.AddSingleton<IBinanceRestClient>(p =>
{
	var secrets = p.GetRequiredService<IOptions<BinanceSecrets>>();

	return new BinanceRestClient(options =>
	{
		options.ApiCredentials = new ApiCredentials(
			secrets.Value.ApiKey,
			secrets.Value.SecretKey
		);
	});
});

appBuilder.Services.AddSingleton<ITelegramBotClient>(p =>
{
	var secrets = p.GetRequiredService<IOptions<TelegramSecrets>>().Value;

	return new TelegramBotClient(secrets.AccessToken);
});

appBuilder.Services.AddSingleton<BinanceService>();
appBuilder.Services.AddSingleton<SandboxService>();
appBuilder.Services.AddSingleton<StrategiesRunner>();

// var json = JsonObject.Parse(File.ReadAllText("benchmark.json"));
//
// var strategies = json["Strategies"].AsArray();
//
// var ordered = strategies.OrderByDescending(s => s["Values"].AsArray().Last().GetValue<decimal>())
// 	.Take(1000);
//
// json["Strategies"] = JsonSerializer.SerializeToNode(ordered);
//
// Console.WriteLine(json["Strategies"].AsArray().Count);
//
// File.WriteAllText("benchmark-clipped.json", json.ToString());
//
// return;

// appBuilder.Services.AddSingleton<IStrategy, TestStrategy>();
// appBuilder.Services.AddSingleton<IStrategy, GlebStrategy>();
// appBuilder.Services.AddSingleton<IStrategy, SkisStrategy>();

// appBuilder.Services.AddSingleton<TelegramUserBotService>();
// appBuilder.Services.AddHostedService(sp => sp.GetRequiredService<TelegramUserBotService>());
//
// appBuilder.Services.AddHostedService<AppService>();
// appBuilder.Services.AddHostedService<StrategiesBenchmark>();
// appBuilder.Services.AddHostedService<StrategiesBenchmark2>();
// appBuilder.Services.AddHostedService<StrategiesBenchmark3>();
// appBuilder.Services.AddHostedService<StrategiesBenchmark4>();
appBuilder.Services.AddHostedService<StrategiesBenchmark5>();
// appBuilder.Services.AddHostedService<StrategiesRunner>();

// appBuilder.Services.AddHostedService<TraderService>();

#endregion

#region App

var app = appBuilder.Build();

// app.UseHttpsRedirection();
// // app.UseStaticFiles();
// app.UseRouting();
// app.UseAuthorization();
// app.MapControllers();
//
// app.MapGet("/", () => Results.Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow }));

// await using (var context = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext())
// {
// 	await context.Database.MigrateAsync();
// }

await app.RunAsync();

#endregion