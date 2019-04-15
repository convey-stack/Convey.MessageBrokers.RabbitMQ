using System.Threading.Tasks;

namespace Convey.MessageBrokers.RabbitMQ
{
    public interface IBusPublisher
    {
        Task PublishAsync<TMessage>(TMessage message, ICorrelationContext context)
            where TMessage : IMessage;
    }
}