using System.Threading.Tasks;

namespace Convey.MessageBrokers.RabbitMQ.Publishers
{
    internal sealed class Publisher : IPublisher
    {
        private readonly IRabbitMqClient _client;
        private readonly IConventionsProvider _conventionsProvider;

        public Publisher(IRabbitMqClient client, IConventionsProvider conventionsProvider)
        {
            _client = client;
            _conventionsProvider = conventionsProvider;
        }

        public Task PublishAsync<T>(T message, ICorrelationContext context = null) where T : class
        {
            _client.Send(message, _conventionsProvider.Get<T>(), context);

            return Task.CompletedTask;
        }
    }
}