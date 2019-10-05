namespace Convey.MessageBrokers.RabbitMQ
{
    public interface IRabbitMqClient
    {
        void Send(object message, string routingKey, string exchange, object context = null);
    }
}