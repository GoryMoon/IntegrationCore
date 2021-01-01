using System;

namespace IntegrationCore
{
    public static class ChatManager
    {
        public static Action<string, string> ChatAction { get; set; } = (s, s1) => { };

        public static void SendColored(string message, string color)
        {
            ChatAction.Invoke(message, color);
        }
    }
}