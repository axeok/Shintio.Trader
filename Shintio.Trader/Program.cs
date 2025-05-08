using Binance.Net.Clients;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shintio.Trader.Configuration;
using Shintio.Trader.Database.Contexts;
using Shintio.Trader.Interfaces;
using Shintio.Trader.Services;
using Shintio.Trader.Services.Background;
using Shintio.Trader.Services.Strategies;

#region Builder

var appBuilder = WebApplication.CreateBuilder();

appBuilder.Services.Configure<TelegramSecrets>(appBuilder.Configuration.GetSection("Telegram"));
appBuilder.Services.Configure<BinanceSecrets>(appBuilder.Configuration.GetSection("Binance"));

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
	var binanceSecrets = p.GetRequiredService<IOptions<BinanceSecrets>>();

	return new BinanceRestClient(options =>
	{
		options.ApiCredentials = new ApiCredentials(
			binanceSecrets.Value.ApiKey,
			binanceSecrets.Value.SecretKey
		);
	});
});

appBuilder.Services.AddSingleton<BinanceService>();
appBuilder.Services.AddSingleton<SandboxService>();

// appBuilder.Services.AddSingleton<IStrategy, TestStrategy>();
// appBuilder.Services.AddSingleton<IStrategy, GlebStrategy>();
// appBuilder.Services.AddSingleton<IStrategy, SkisStrategy>();

// appBuilder.Services.AddSingleton<TelegramUserBotService>();
// appBuilder.Services.AddHostedService(sp => sp.GetRequiredService<TelegramUserBotService>());
//
// appBuilder.Services.AddHostedService<AppService>();
appBuilder.Services.AddHostedService<StrategiesBenchmark>();
// appBuilder.Services.AddHostedService<StrategiesRunner>();

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