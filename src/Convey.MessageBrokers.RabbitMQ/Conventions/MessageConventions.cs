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
        public bool DeclareExchange { get; }
        public bool DurableExchange { get; }
        public bool AutoDeleteExchange { get; }

        public MessageConventions(Type type, string routingKey, string exchange, string queue, string exchangeType,
            bool declareExchange, bool durableExchange, bool autoDeleteExchange)
        {
            Type = type;
            RoutingKey = routingKey;
            Exchange = exchange;
            Queue = queue;
            ExchangeType = exchangeType;
            DeclareExchange = declareExchange;
            DurableExchange = durableExchange;
            AutoDeleteExchange = autoDeleteExchange;
        }
    }
}