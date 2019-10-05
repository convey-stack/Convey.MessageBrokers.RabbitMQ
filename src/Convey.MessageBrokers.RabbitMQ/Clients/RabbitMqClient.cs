using System;
using System.Collections.Generic;
using System.Text;
using RabbitMQ.Client;

namespace Convey.MessageBrokers.RabbitMQ.Clients
{
    internal sealed class RabbitMqClient : IRabbitMqClient
    {
        private const string EmptyContext = "{}";
        private readonly IConnection _connection;
        private readonly IContextProvider _contextProvider;
        private readonly IRabbitMqSerializer _serializer;
        private readonly bool _contextEnabled;
        private readonly bool _includeCorrelationId;

        public RabbitMqClient(IConnection connection, IContextProvider contextProvider, IRabbitMqSerializer serializer,
            RabbitMqOptions options)
        {
            _connection = connection;
            _contextProvider = contextProvider;
            _serializer = serializer;
            _contextEnabled = options.Context?.Enabled == true;
            _includeCorrelationId = options.Context?.IncludeCorrelationId == true;
        }

        public void Send(object message, IConventions conventions, ICorrelationContext context = null)
        {
            using (var channel = _connection.CreateModel())
            {
                var json = _serializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);
                var properties = channel.CreateBasicProperties();
                properties.MessageId = Guid.NewGuid().ToString("N");
                properties.CorrelationId = Guid.NewGuid().ToString("N");
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                properties.Headers = new Dictionary<string, object>();
                if (_contextEnabled)
                {
                    IncludeContext(context, properties);
                }

                if (conventions.DeclareExchange)
                {
                    channel.ExchangeDeclare(conventions.Exchange, conventions.ExchangeType, conventions.DurableExchange,
                        conventions.AutoDeleteExchange);
                }

                channel.BasicPublish(conventions.Exchange, conventions.RoutingKey, properties, body);
            }
        }

        private void IncludeContext(object context, IBasicProperties properties)
        {
            if (!(context is null))
            {
                properties.Headers.Add(_contextProvider.HeaderName, _serializer.Serialize(context));
                return;
            }

            if (_includeCorrelationId)
            {
                properties.Headers.Add(_contextProvider.HeaderName,
                    _serializer.Serialize(new Context(properties.CorrelationId)));
                return;
            }

            properties.Headers.Add(_contextProvider.HeaderName, EmptyContext);
        }

        private class Context : ICorrelationContext
        {
            public string CorrelationId { get; set; }
            public string SpanContext { get; set; }

            public Context(string correlationId)
            {
                CorrelationId = correlationId;
            }
        }
    }
}