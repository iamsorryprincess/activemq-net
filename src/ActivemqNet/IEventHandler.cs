namespace ActivemqNet
{
    public interface IEventHandler
    {
        void HandleError(string error);

        void HandleEvent(string @event);
    }
}