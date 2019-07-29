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
            if (context is null || string.IsNullOrWhiteSpace(context.CorrelationId))
            {
                context = new CorrelationContext();
            }

            return _busClient.PublishAsync(message, ctx => ctx.UseMessageContext(context));
        }

        private class CorrelationContext : ICorrelationContext
        {
            public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
            public string SpanContext { get; set; }
            public int Retries { get; set; }
        }
    }
}