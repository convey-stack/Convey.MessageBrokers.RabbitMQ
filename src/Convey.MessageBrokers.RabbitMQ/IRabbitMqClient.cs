namespace Convey.MessageBrokers.RabbitMQ
{
    public interface IRabbitMqClient
    {
        void Send(object message, IConventions conventions, ICorrelationContext context = null);
    }
}