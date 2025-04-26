using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Configuration;
using TdLib;
using TdLib.Bindings;

namespace Shintio.Trader.Common;

public class UserBotClient
{
	public event Action<long, long?, string, string>? MessageReceived;
	
	private readonly string _filesPath;
	private readonly ILogger<UserBotClient> _logger;

	private readonly TelegramSecrets _secrets;
	private readonly string _phoneNumber;
	private readonly TdClient _client;

	private bool _authNeeded = false;
	private bool _passwordNeeded = false;

	private readonly ManualResetEventSlim _readyToAuthenticate = new();
	private TaskCompletionSource<string>? _codeSource;
	private TaskCompletionSource<string>? _passwordSource;

	public UserBotClient(TelegramSecrets secrets, string phoneNumber, string filesPath, ILogger<UserBotClient> logger)
	{
		_logger = logger;
		_secrets = secrets;
		_phoneNumber = phoneNumber;
		_filesPath = filesPath;

		_client = new TdClient();
		_client.Bindings.SetLogVerbosityLevel(TdLogLevel.Fatal);

		_client.UpdateReceived += async (_, update) => { await ProcessUpdates(update); };
	}

	public bool IsAuthenticating => _authNeeded || _passwordNeeded;

	public async Task<bool> AuthenticateAsync()
	{
		_readyToAuthenticate.Wait();

		if (_authNeeded && !await HandleAuthentication())
		{
			return false;
		}

		var user = await GetCurrentUser();

		if (!_phoneNumber.Contains(user.PhoneNumber))
		{
			await _client.LogOutAsync();

			return await AuthenticateAsync();
		}

		return true;
	}

	public async Task SendMessage(long chatId, string text)
	{
		try
		{
			await _client.ExecuteAsync(new TdApi.GetChats { Limit = 1 });
			await _client.ExecuteAsync(new TdApi.GetChat { ChatId = chatId });

			await _client.SendMessageAsync(chatId,
				inputMessageContent: new TdApi.InputMessageContent.InputMessageText()
				{
					Text = new TdApi.FormattedText()
					{
						Text = text,
					},
				});
		}
		catch (Exception e)
		{
			_logger.LogError(e, "Error sending message to Telegram: {Message}", e.Message);
		}
	}

	public async Task ForwardMessage(long fromId, long messageId, long toId, long threadId = 0)
	{
		try
		{
			await _client.ExecuteAsync(new TdApi.GetChats { Limit = 1 });
			await _client.ExecuteAsync(new TdApi.GetChat { ChatId = fromId });
			await _client.ExecuteAsync(new TdApi.GetChat { ChatId = toId });

			await _client.ExecuteAsync(new TdApi.GetMessage
			{
				ChatId = fromId,
				MessageId = messageId,
			});

			await _client.ForwardMessagesAsync(
				toId,
				fromChatId: fromId,
				messageIds: [messageId],
				messageThreadId: threadId,
				removeCaption: true
			);
		}
		catch (Exception e)
		{
			_logger.LogError(e, "Error sending message to Telegram: {Message}", e.Message);
		}
	}

	public bool TrySetCode(string code)
	{
		if (_codeSource != null)
		{
			_codeSource.TrySetResult(code);

			return true;
		}
		
		if (_passwordSource != null)
		{
			_passwordSource.TrySetResult(code);

			return true;
		}

		return false;
	}

	private async Task<bool> HandleAuthentication()
	{
		_logger.LogInformation("Authenticating with Telegram API...");
		await _client.ExecuteAsync(new TdApi.SetAuthenticationPhoneNumber
		{
			PhoneNumber = _phoneNumber
		});

		_codeSource = new TaskCompletionSource<string>();
		var code = await _codeSource.Task;
		_codeSource = null;

		try
		{
			await _client.ExecuteAsync(new TdApi.CheckAuthenticationCode
			{
				Code = code
			});

			if (_passwordNeeded)
			{
				_logger.LogInformation("Need password...");
				
				_passwordSource = new TaskCompletionSource<string>();
				var password = await _passwordSource.Task;
				_passwordSource = null;
				
				await _client.ExecuteAsync(new TdApi.CheckAuthenticationPassword
				{
					Password = password
				});
			}
		}
		catch (Exception e)
		{
			_logger.LogError(e, "Error logging in to Telegram API");
			return false;
		}
		
		_logger.LogInformation("Login successful!");

		return true;
	}

	private async Task ProcessUpdates(TdApi.Update update)
	{
		switch (update)
		{
			case TdApi.Update.UpdateAuthorizationState
			{
				AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitTdlibParameters
			}:
				var fileName = $"{string.Join("", Regex.Matches(_phoneNumber, @"\d+").Select(m => m.Value))}-db";

				var filesLocation = Path.Combine(AppContext.BaseDirectory, "telegram", fileName);

				_logger.LogInformation("Using files for {FileName}", filesLocation);

				await _client.ExecuteAsync(new TdApi.SetTdlibParameters
				{
					ApiId = _secrets.ApiId,
					ApiHash = _secrets.ApiHash,
					DeviceModel = "PC",
					SystemLanguageCode = "en",
					ApplicationVersion = "1.0.0",
					DatabaseDirectory = filesLocation,
					FilesDirectory = filesLocation,
				});
				break;

			case TdApi.Update.UpdateAuthorizationState
			{
				AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitPhoneNumber
			}:
			case TdApi.Update.UpdateAuthorizationState
			{
				AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitCode
			}:
				_authNeeded = true;
				_readyToAuthenticate.Set();
				break;

			case TdApi.Update.UpdateAuthorizationState
			{
				AuthorizationState: TdApi.AuthorizationState.AuthorizationStateWaitPassword
			}:
				_authNeeded = true;
				_passwordNeeded = true;
				_readyToAuthenticate.Set();
				break;

			case TdApi.Update.UpdateUser:
				_readyToAuthenticate.Set();
				break;

			case TdApi.Update.UpdateConnectionState { State: TdApi.ConnectionState.ConnectionStateReady }:
				break;

			case TdApi.Update.UpdateNewMessage message:
				if (message.Message.Content is not TdApi.MessageContent.MessageText messageText)
				{
					break;
				}

				var chatId = message.Message.ChatId;
				var senderId = (message.Message.SenderId as TdApi.MessageSender.MessageSenderUser)?.UserId;

				TdApi.Chat chat;

				try
				{
					chat = await _client.GetChatAsync(chatId);
				}
				catch
				{
					await _client.ExecuteAsync(new TdApi.GetChats { Limit = 1 });
					chat = await _client.GetChatAsync(chatId);
				}

				MessageReceived?.Invoke(chatId, senderId, messageText.Text.Text, chat.Title);
				break;

			default:
				break;
		}
	}

	private async Task<TdApi.User> GetCurrentUser()
	{
		return await _client.ExecuteAsync(new TdApi.GetMe());
	}

	private async IAsyncEnumerable<TdApi.Chat> GetChannels(int limit)
	{
		var chats = await _client.ExecuteAsync(new TdApi.GetChats
		{
			Limit = limit
		});

		foreach (var chatId in chats.ChatIds)
		{
			var chat = await _client.ExecuteAsync(new TdApi.GetChat
			{
				ChatId = chatId
			});

			if (chat.Type is TdApi.ChatType.ChatTypeSupergroup or TdApi.ChatType.ChatTypeBasicGroup
			    or TdApi.ChatType.ChatTypePrivate)
			{
				yield return chat;
			}
		}
	}
}