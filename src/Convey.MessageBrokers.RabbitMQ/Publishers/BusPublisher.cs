using System;
using System.Threading.Tasks;
using RawRabbit;
using RawRabbit.Enrichers.MessageContext;

namespace Convey.MessageBrokers.RabbitMQ.Publishers
{
    internal sealed class BusPublisher : IBusPublisher
    {
        private readonly IBusClient _busClient;

        public BusPublisher(IBusClient busClient)
        {
            _busClient = busClient;
        }

        public Task PublishAsync<TMessage>(TMessage message, ICorrelationContext context)
            where TMessage : class
        {
            if (context.Id == Guid.Empty)
            {
                context = CorrelationContext.FromId(Guid.NewGuid());
            }

            return _busClient.PublishAsync(message, ctx => ctx.UseMessageContext(context));
        }
    }
}