using System;
using System.Threading.Tasks;
using Convey.Types;

namespace Convey.MessageBrokers.RabbitMQ
{
    public interface IBusSubscriber
    {
        IBusSubscriber SubscribeMessage<TMessage>(Func<IServiceProvider, TMessage, ICorrelationContext, Task> handle, 
            string @namespace = null, string queueName = null, Func<TMessage, ConveyException, IMessage> onError = null) 
            where TMessage : IMessage;
    }
}
