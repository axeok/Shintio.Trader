using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shintio.Trader.Common;
using Shintio.Trader.Configuration;
using Shintio.Trader.Database.Contexts;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Shintio.Trader.Services;

public class TelegramUserBotService : BackgroundService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<TelegramUserBotService> _logger;
    private readonly ILogger<UserBotClient> _userBotLogger;
    private readonly TelegramSecrets _telegramSecrets;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    private UserBotClient _userBot = null!;
    private ITelegramBotClient _bot = null!;

    public TelegramUserBotService(
        IHostApplicationLifetime lifetime,
        ILogger<TelegramUserBotService> logger,
        IOptions<TelegramSecrets> telegramSecrets,
        ILogger<UserBotClient> userBotLogger,
        IDbContextFactory<AppDbContext> dbContextFactory
    )
    {
        _lifetime = lifetime;
        _logger = logger;
        _telegramSecrets = telegramSecrets.Value;
        _userBotLogger = userBotLogger;
        _dbContextFactory = dbContextFactory;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting bot...");

        if (string.IsNullOrEmpty(_telegramSecrets.AccessToken))
        {
            _logger.LogError("Access token is not found");

            _lifetime.StopApplication();

            return Task.CompletedTask;
        }

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _bot = new TelegramBotClient(_telegramSecrets.AccessToken);

        var receiverOptions = new ReceiverOptions()
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );

        var phoneNumber = _telegramSecrets.Phone;

        _userBot = new UserBotClient(_telegramSecrets, phoneNumber, "", _userBotLogger);
        _logger.LogInformation("Starting bot for {PhoneNumber}...", phoneNumber);

        if (!await _userBot.AuthenticateAsync())
        {
            _logger.LogError("Failed to authenticate user bot");

            _lifetime.StopApplication();
        }
        else
        {
            _logger.LogInformation("Bot started for {PhoneNumber}!", phoneNumber);
        }

        _userBot.MessageReceived += UserBotOnMessageReceived;

        await _userBot.SendMessage(384118725, "Yoba, eto ti?");
    }

    private async void UserBotOnMessageReceived(long chatId, long? senderId, string message, string title)
    {
        _logger.LogInformation("[UserBot] Message from {senderId} in {chatId}: {message}", senderId, chatId, message);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping bot...");

        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken
    )
    {
        await (update.Type switch
        {
            UpdateType.Message => OnMessageReceived(botClient, update.Message!, cancellationToken),
            _ => Task.CompletedTask
        });
    }

    private async Task OnMessageReceived(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken
    )
    {
        var from = message.From?.Id;
        _logger.LogInformation("[Bot] Message from {senderId} in {chatId} with album {album}: {message}", from,
            message.Chat.Id, message.MediaGroupId, message.Text);

        var text = message.Text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (_userBot.IsAuthenticating)
            {
                if (_userBot.TrySetCode(text))
                {
                    await botClient.SendMessage(message.Chat.Id, "Вошёл!",
                        cancellationToken: cancellationToken);
                }

                return;
            }
        }
    }

    private Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(errorMessage);

        return Task.CompletedTask;
    }
}