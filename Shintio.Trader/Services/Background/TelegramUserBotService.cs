using Binance.Net.Interfaces.Clients;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shintio.Trader.Common;
using Shintio.Trader.Configuration;
using Shintio.Trader.Utils;
using TdLib;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Shintio.Trader.Services.Background;

public class TelegramUserBotService : BackgroundService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<TelegramUserBotService> _logger;
    private readonly ILogger<UserBotClient> _userBotLogger;
    private readonly TelegramSecrets _telegramSecrets;
    // private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IBinanceRestClient _binanceClient;

    private UserBotClient _userBot = null!;
    public ITelegramBotClient _bot = null!;

    public TelegramUserBotService(
        IHostApplicationLifetime lifetime,
        ILogger<TelegramUserBotService> logger,
        IOptions<TelegramSecrets> telegramSecrets,
        ILogger<UserBotClient> userBotLogger,
        IBinanceRestClient binanceClient
        // IDbContextFactory<AppDbContext> dbContextFactory
    )
    {
        _lifetime = lifetime;
        _logger = logger;
        _telegramSecrets = telegramSecrets.Value;
        _userBotLogger = userBotLogger;
        // _dbContextFactory = dbContextFactory;
        _binanceClient = binanceClient;
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
        // _bot = new TelegramBotClient(_telegramSecrets.AccessToken);
        //
        // var receiverOptions = new ReceiverOptions()
        // {
        //     AllowedUpdates = Array.Empty<UpdateType>()
        // };
        //
        // _bot.StartReceiving(
        //     updateHandler: HandleUpdateAsync,
        //     errorHandler: HandleErrorAsync,
        //     receiverOptions: receiverOptions,
        //     cancellationToken: stoppingToken
        // );

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

        // var messages = await _userBot.GetChatHistory(-1002657875280, 500);
        //
        // messages.Reverse();
        //
        // Console.WriteLine(messages.Count);
        //
        // File.WriteAllText("messages.json", JsonSerializer.Serialize(messages
        //     .Select(FetchText)
        //     .OfType<string>()));
        // await _userBot.StartCall(384118725);
        // _userBot.MessageReceived += UserBotOnMessageReceived;
    }

    private string? FetchText(TdApi.Message message) =>
        message.Content switch
        {
            TdApi.MessageContent.MessageText textMessage => textMessage.Text.Text,
            TdApi.MessageContent.MessagePhoto photoMessage => photoMessage.Caption.Text,
            TdApi.MessageContent.MessageVideo videoMessage => videoMessage.Caption.Text,
            TdApi.MessageContent.MessageDocument documentMessage => documentMessage.Caption.Text,
            _ => null
        };

    private async void UserBotOnMessageReceived(long chatId, long? senderId, string message, string title)
    {
        if (chatId != -1002657875280 && chatId != -4645156321)
        {
            return;
        }

        var parser = new MessageParser(_binanceClient, _bot, _logger);
        
        await parser.Parse(message);
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

        await botClient.SendMessage(message.Chat.Id, "Живой", cancellationToken: cancellationToken);
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