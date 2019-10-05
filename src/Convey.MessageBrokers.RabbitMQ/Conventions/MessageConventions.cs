using System;

namespace Convey.MessageBrokers.RabbitMQ.Conventions
{
    public class MessageConventions : IConventions
    {
        public Type Type { get; }
        public string RoutingKey { get; }
        public string Exchange { get; }
        public string Queue { get; }
        public string ExchangeType { get; }

        public MessageConventions(Type type, string routingKey, string exchange, string queue, string exchangeType)
        {
            Type = type;
            RoutingKey = routingKey;
            Exchange = exchange;
            Queue = queue;
            ExchangeType = exchangeType;
        }
    }
}