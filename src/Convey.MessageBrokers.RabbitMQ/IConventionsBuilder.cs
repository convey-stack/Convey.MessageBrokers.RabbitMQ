using System;

namespace Convey.MessageBrokers.RabbitMQ
{
    public interface IConventionsBuilder
    {
        string GetRoutingKey(Type type);
        string GetExchange(Type type);
        string GetQueue(Type type);
        string GetExchangeType(Type type);
        bool GetDeclareExchange(Type type);
        bool GetDurableExchange(Type type);
        bool GetAutoDeleteExchange(Type type);
    }
}