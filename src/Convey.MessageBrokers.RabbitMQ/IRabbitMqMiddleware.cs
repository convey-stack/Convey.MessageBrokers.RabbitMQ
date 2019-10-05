using System;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace Convey.MessageBrokers.RabbitMQ
{
    public interface IRabbitMqMiddleware
    {
        Task HandleAsync(Func<Task> next, object message, ICorrelationContext correlationContext,
            BasicDeliverEventArgs args);
    }
}