using System;
using System.Linq;
using System.Reflection;

namespace Convey.MessageBrokers.RabbitMQ.Conventions
{
    public class ConventionsBuilder : IConventionsBuilder
    {
        private readonly RabbitMqOptions _options;
        private readonly bool _underscore;

        public ConventionsBuilder(RabbitMqOptions options)
        {
            _options = options;
            _underscore = options.ConventionsCasing?.Equals("underscore",
                              StringComparison.InvariantCultureIgnoreCase) == true;
        }

        public string GetRoutingKey(Type type)
        {
            var attribute = GeAttribute(type);
            var routingKey = string.IsNullOrWhiteSpace(attribute?.RoutingKey) ? type.Name : attribute.RoutingKey;

            return WithCasing(routingKey);
        }

        public string GetExchange(Type type)
        {
            var attribute = GeAttribute(type);
            var exchange = string.IsNullOrWhiteSpace(attribute?.Exchange)
                ? string.IsNullOrWhiteSpace(_options.Exchange?.Name) ? type.Namespace : _options.Exchange.Name
                : attribute.Exchange;

            return WithCasing(exchange);
        }

        public string GetQueue(Type type)
        {
            var attribute = GeAttribute(type);
            var queue = string.IsNullOrWhiteSpace(attribute?.Queue) ? $"{type.Namespace}/{type.Name}" : attribute.Queue;

            return WithCasing(queue);
        }

        public string GetExchangeType(Type type)
        {
            var attribute = GeAttribute(type);

            return string.IsNullOrWhiteSpace(attribute?.ExchangeType)
                ? string.IsNullOrWhiteSpace(_options.Exchange?.Type) ? "topic" : _options.Exchange.Type
                : attribute.ExchangeType;
        }

        public bool GetDeclareExchange(Type type)
        {
            var attribute = GeAttribute(type);

            return attribute?.DeclareExchange ?? (_options.Exchange?.Declare ?? false);
        }

        public bool GetDurableExchange(Type type)
        {
            var attribute = GeAttribute(type);

            return attribute?.DurableExchange ?? (_options.Exchange?.Durable ?? false);
        }

        public bool GetAutoDeleteExchange(Type type)
        {
            var attribute = GeAttribute(type);

            return attribute?.AutoDeleteExchange ?? (_options.Exchange?.AutoDelete ?? false);
        }

        private string WithCasing(string value) => _underscore ? Underscore(value) : value;

        private static string Underscore(string value)
            => string.Concat(value.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString()));

        private static MessageAttribute GeAttribute(MemberInfo type) => type.GetCustomAttribute<MessageAttribute>();
    }
}