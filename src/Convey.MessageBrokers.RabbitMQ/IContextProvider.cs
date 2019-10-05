using System.Collections.Generic;

namespace Convey.MessageBrokers.RabbitMQ
{
    public interface IContextProvider
    {
        string HeaderName { get; }
        ICorrelationContext Get(IDictionary<string, object> headers);
    }
}