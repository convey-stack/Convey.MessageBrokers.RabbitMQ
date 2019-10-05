using System;
using System.Linq;
using System.Reflection;

namespace Convey.MessageBrokers.RabbitMQ.Conventions
{
    public class ConventionsBuilder : IConventionsBuilder
    {
        private readonly string _defaultExchangeName;
        private readonly string _defaultExchangeType;
        private readonly bool _underscore;

        public ConventionsBuilder(RabbitMqOptions options)
        {
            _defaultExchangeName = options.Exchange?.Name;
            _defaultExchangeType = options.Exchange?.Type;
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
                ? string.IsNullOrWhiteSpace(_defaultExchangeName) ? type.Namespace : _defaultExchangeName
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

            return string.IsNullOrWhiteSpace(attribute?.Exchange)
                ? string.IsNullOrWhiteSpace(_defaultExchangeType) ? "topic" : _defaultExchangeType
                : attribute.ExchangeType;
        }

        private string WithCasing(string value) => _underscore ? Underscore(value) : value;

        private static string Underscore(string value)
            => string.Concat(value.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString()));

        private static MessageAttribute GeAttribute(MemberInfo type) => type.GetCustomAttribute<MessageAttribute>();
    }
}