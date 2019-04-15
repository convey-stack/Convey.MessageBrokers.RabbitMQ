using System;
using System.Threading.Tasks;

namespace Convey.MessageBrokers.RabbitMQ
{
    public interface IBusSubscriber
    {
        IBusSubscriber SubscribeMessage<TMessage>(Func<IServiceProvider, Task> handle, string @namespace = null, 
            string queueName = null, Func<TMessage, ConveyException, IMessage> onError = null) 
            where TMessage : IMessage;
    }
}
