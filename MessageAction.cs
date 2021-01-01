namespace IntegrationCore
{
    public class MessageAction: BaseAction
    {
        private readonly string _message;

        public MessageAction(string message)
        {
            _message = message;
        }

        public override ActionResponse Handle()
        {
            ChatManager.SendColored(_message, "#0984e3");
            return ActionResponse.Done;
        }
    }
}