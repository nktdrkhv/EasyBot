using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using EasyBot.Exceptions;

namespace EasyBot;

public abstract partial class EasyBot
{
    public async Task<UpdateKind> NextEvent(UpdateInfo update, CancellationToken ct = default)
    {
        using var bothCT = CancellationTokenSource.CreateLinkedTokenSource(ct, _cancel.Token);
        var newUpdate = await ((IGetNext)update).NextUpdate(bothCT.Token);
        update.Message = newUpdate.Message;
        update.CallbackData = newUpdate.CallbackData;
        update.Update = newUpdate.Update;
        return update.UpdateKind = newUpdate.UpdateKind;
    }

    public async Task<string> ButtonClicked(UpdateInfo update, Message msg = null, CancellationToken ct = default)
    {
        while (true)
        {
            switch (await NextEvent(update, ct))
            {
                case UpdateKind.CallbackQuery:
                    if (msg != null && update.Message.MessageId != msg.MessageId)
                        _ = Telegram.AnswerCallbackQueryAsync(update.Update.CallbackQuery.Id, null, cancellationToken: ct);
                    else
                        return update.CallbackData;
                    continue;
                case UpdateKind.OtherUpdate
                    when update.Update.MyChatMember is ChatMemberUpdated
                    { NewChatMember: { Status: ChatMemberStatus.Left or ChatMemberStatus.Kicked } }:
                    throw new LeftTheChatException(); // abort the calling method
            }
        }
    }

    public async Task<MsgCategory> NewMessage(UpdateInfo update, CancellationToken ct = default)
    {
        while (true)
        {
            switch (await NextEvent(update, ct))
            {
                case UpdateKind.NewMessage
                    when update.MsgCategory is MsgCategory.Text or MsgCategory.MediaOrDoc or MsgCategory.StickerOrDice:
                    return update.MsgCategory; // NewMessage only returns for messages from these 3 categories
                case UpdateKind.CallbackQuery:
                    _ = Telegram.AnswerCallbackQueryAsync(update.Update.CallbackQuery.Id, null, cancellationToken: ct);
                    continue;
                case UpdateKind.OtherUpdate
                    when update.Update.MyChatMember is ChatMemberUpdated
                    { NewChatMember: { Status: ChatMemberStatus.Left or ChatMemberStatus.Kicked } }:
                    throw new LeftTheChatException(); // abort the calling method
            }
        }
    }

    public async Task<string> NewTextMessage(UpdateInfo update, CancellationToken ct = default)
    {
        while (await NewMessage(update, ct) != MsgCategory.Text) { }
        return update.Message.Text;
    }

    public void ReplyCallback(UpdateInfo update, string text = null, bool showAlert = false, string url = null)
    {
        if (update.Update.Type != UpdateType.CallbackQuery)
            throw new InvalidOperationException("This method can be called only for CallbackQuery updates");
        _ = Telegram.AnswerCallbackQueryAsync(update.Update.CallbackQuery.Id, text, showAlert, url);
    }
}