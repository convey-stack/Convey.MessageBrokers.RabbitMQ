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

        public Task PublishAsync<T>(T message, object context = null) where T : class
        {
            var conventions = _conventionsProvider.Get<T>();
            _client.Send(message, conventions.RoutingKey, conventions.Exchange, context);
            
            return Task.CompletedTask;
        }
    }
}