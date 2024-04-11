using Telegram.Bot.Types.Enums;

namespace EasyBot.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class MessageAttribute : Attribute
{
    public MessageAttribute() { }

    public MessageAttribute(MessageType msgType) { }
}