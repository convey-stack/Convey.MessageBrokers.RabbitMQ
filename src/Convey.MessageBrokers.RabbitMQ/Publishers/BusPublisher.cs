using System.Reflection;
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

        public async Task PublishAsync<TMessage>(TMessage message, ICorrelationContext context)
            where TMessage : class
            => await _busClient.PublishAsync(message, ctx => ctx.UseMessageContext(context));
    }
}