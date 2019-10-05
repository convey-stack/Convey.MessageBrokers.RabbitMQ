using System;

namespace Convey.MessageBrokers.RabbitMQ
{
    public interface IConventions
    {
        Type Type { get; }
        string RoutingKey { get; }
        string Exchange { get; }
        string Queue { get; }
        string ExchangeType { get; }
        bool DeclareExchange { get; }
        bool DurableExchange { get; }
        bool AutoDeleteExchange { get; }
    }
}