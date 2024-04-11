using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Requests;

namespace EasyBot;

public abstract partial class EasyBot    // A fun way to code Telegram Bots, by Wizou
{
    public ITelegramBotClient Telegram { get; private set; }
    public User Itself { get; private set; }

    private int _lastUpdateId = -1;
    private readonly CancellationTokenSource _cancel = new();
    private readonly IDictionary<long, TaskInfo> _tasks;
    private readonly IDictionary<long, Keeper> _keepers;

    private readonly MethodInfo? _onUserEnter;
    private readonly MethodInfo? _onUserExit;
    private readonly MethodInfo? _onException;
    private readonly MethodInfo? _onUnhandledUpdate;

    private readonly ICollection<MethodInfo> _handlers;

    public EasyBot(string botToken)
    {
        Telegram = new TelegramBotClient(botToken);
        Itself = Task.Run(() => Telegram.MakeRequestAsync(new GetMeRequest(), _cancel.Token)).Result;
    }

    public void Run() => RunAsync().Wait();
    public async Task RunAsync()
    {
        while (true)
        {
            var updates = await Telegram.GetUpdatesAsync(_lastUpdateId + 1, timeout: 2);
            foreach (var update in updates)
                HandleUpdate(update);
            if (Console.KeyAvailable)
                if (Console.ReadKey().Key == ConsoleKey.Escape)
                    break;
        }
        _cancel.Cancel();
    }

    public async Task<string> CheckWebhook(string url)
    {
        var webhookInfo = await Telegram.GetWebhookInfoAsync();
        string result = $"{BotName} is running";
        if (webhookInfo.Url != url)
        {
            await Telegram.SetWebhookAsync(url);
            result += " and now registered as Webhook";
        }
        return $"{result}\n\nLast webhook error: {webhookInfo.LastErrorDate} {webhookInfo.LastErrorMessage}";
    }

    /// <summary>Use this method in your WebHook controller</summary>
    public void HandleUpdate(Update update)
    {
        if (update.Id <= _lastUpdateId) return;
        _lastUpdateId = update.Id;
        switch (update.Type)
        {
            case UpdateType.Message: HandleUpdate(update, UpdateKind.NewMessage, update.Message); break;
            case UpdateType.EditedMessage: HandleUpdate(update, UpdateKind.EditedMessage, update.EditedMessage); break;
            case UpdateType.ChannelPost: HandleUpdate(update, UpdateKind.NewMessage, update.ChannelPost); break;
            case UpdateType.EditedChannelPost: HandleUpdate(update, UpdateKind.EditedMessage, update.EditedChannelPost); break;
            case UpdateType.CallbackQuery: HandleUpdate(update, UpdateKind.CallbackQuery, update.CallbackQuery.Message); break;
            case UpdateType.MyChatMember: HandleUpdate(update, UpdateKind.OtherUpdate, chat: update.MyChatMember.Chat); break;
            case UpdateType.ChatMember: HandleUpdate(update, UpdateKind.OtherUpdate, chat: update.ChatMember.Chat); break;
            default: HandleUpdate(update, UpdateKind.OtherUpdate); break;
        }
    }

    private void HandleUpdate(Update update, UpdateKind updateKind, Message message = null, Chat chat = null)
    {
        TaskInfo taskInfo;
        chat ??= message?.Chat;
        long chatId = chat?.Id ?? 0;
        lock (_tasks)
            if (!_tasks.TryGetValue(chatId, out taskInfo))
                _tasks[chatId] = taskInfo = new TaskInfo();
        var updateInfo = new UpdateInfo(taskInfo) { UpdateKind = updateKind, Update = update, Message = message };
        if (update.Type is UpdateType.CallbackQuery)
            updateInfo.CallbackData = update.CallbackQuery.Data;
        lock (taskInfo)
            if (taskInfo.Task != null)
            {
                taskInfo.Updates.Enqueue(updateInfo);
                taskInfo.Semaphore.Release();
                return;
            }
        RunTask(taskInfo, updateInfo, chat);
    }

    private void RunTask(TaskInfo taskInfo, UpdateInfo updateInfo, Chat chat)
    {
        Func<Task> taskStarter = (chat?.Type) switch
        {
            ChatType.Private => () => OnPrivateChat(chat, updateInfo.Message?.From, updateInfo),
            ChatType.Group or ChatType.Supergroup => () => OnGroupChat(chat, updateInfo),
            ChatType.Channel => () => OnChannel(chat, updateInfo),
            _ => () => OnOtherEvents(updateInfo),
        };
        taskInfo.Task = Task.Run(taskStarter).ContinueWith(async t =>
        {
            lock (taskInfo)
                if (taskInfo.Semaphore.CurrentCount == 0)
                {
                    taskInfo.Task = null;
                    return;
                }
            var newUpdate = await ((IGetNext)updateInfo).NextUpdate(_cancel.Token);
            RunTask(taskInfo, newUpdate, chat);
        });
    }
}