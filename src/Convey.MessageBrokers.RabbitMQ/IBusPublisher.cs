using System.Threading.Tasks;
using Convey.Types;

namespace Convey.MessageBrokers.RabbitMQ
{
    public interface IBusPublisher
    {
        Task PublishAsync<TMessage>(TMessage message, ICorrelationContext context)
            where TMessage : IMessage;
    }
}