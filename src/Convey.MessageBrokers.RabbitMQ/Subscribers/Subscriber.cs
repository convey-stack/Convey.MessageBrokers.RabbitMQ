using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Convey.MessageBrokers.RabbitMQ.Subscribers
{
    internal sealed class Subscriber : ISubscriber
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPublisher _publisher;
        private readonly IConnection _connection;
        private readonly IRabbitMqSerializer _rabbitMqSerializer;
        private readonly IConventionsProvider _conventionsProvider;
        private readonly IContextProvider _contextProvider;
        private readonly ILogger _logger;
        private readonly IEnumerable<IRabbitMqMiddleware> _middlewares;
        private readonly IExceptionToMessageMapper _exceptionToMessageMapper;
        private readonly int _retries;
        private readonly int _retryInterval;
        private readonly bool _hasMiddlewares;

        public Subscriber(IApplicationBuilder app)
        {
            _serviceProvider = app.ApplicationServices.GetRequiredService<IServiceProvider>();
            _connection = app.ApplicationServices.GetRequiredService<IConnection>();
            _publisher = app.ApplicationServices.GetRequiredService<IPublisher>();
            _rabbitMqSerializer = app.ApplicationServices.GetRequiredService<IRabbitMqSerializer>();;
            _conventionsProvider = app.ApplicationServices.GetRequiredService<IConventionsProvider>();;
            _contextProvider = app.ApplicationServices.GetRequiredService<IContextProvider>();
            _logger = app.ApplicationServices.GetService<ILogger<Subscriber>>();
            _exceptionToMessageMapper = _serviceProvider.GetService<IExceptionToMessageMapper>() ??
                                        new EmptyExceptionToMessageMapper();
            _middlewares = _serviceProvider.GetServices<IRabbitMqMiddleware>();
            _hasMiddlewares = _middlewares.Any();
            var options = _serviceProvider.GetService<RabbitMqOptions>();
            _retries = options.Retries >= 0 ? options.Retries : 3;
            _retryInterval = options.RetryInterval > 0 ? options.RetryInterval : 2;
        }

        public ISubscriber Subscribe<T>(Func<IServiceProvider, T, ICorrelationContext, Task> handle)
            where T : class
        {
            var conventions = _conventionsProvider.Get<T>();
            var channel = _connection.CreateModel();
            channel.ExchangeDeclare(conventions.Exchange, conventions.ExchangeType);
            channel.QueueDeclare(conventions.Queue);
            channel.QueueBind(conventions.Queue, conventions.Exchange, conventions.RoutingKey);
            channel.BasicQos(0, 1, false);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (model, args) =>
            {
                try
                {
                    var body = args.Body;
                    var payload = Encoding.UTF8.GetString(body);
                    var accessor = _serviceProvider.GetService<ICorrelationContextAccessor>();
                    var correlationContext = _contextProvider.Get(args.BasicProperties.Headers);
                    accessor.CorrelationContext = correlationContext;
                    var message = _rabbitMqSerializer.Deserialize<T>(payload);
                    Task<Exception> Next() => TryHandleAsync(message, correlationContext, handle);
                    if (_hasMiddlewares)
                    {
                        foreach (var middleware in _middlewares)
                        {
                            await middleware.HandleAsync(Next, message, correlationContext, args);
                        }

                        return;
                    }

                    var exception = await Next();
                    if (exception is null)
                    {
                        channel.BasicAck(args.DeliveryTag, false);
                        return;
                    }

                    throw exception;

                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, ex.Message);
                    throw;
                }


            };
            channel.BasicConsume(conventions.Queue, false, consumer);

            return this;
        }

        private Task<Exception> TryHandleAsync<TMessage>(TMessage message, ICorrelationContext correlationContext,
            Func<IServiceProvider, TMessage, ICorrelationContext, Task> handle)
        {
            var currentRetry = 0;
            var messageName = message.GetType().Name;
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(_retries, i => TimeSpan.FromSeconds(_retryInterval));

            return retryPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    var retryMessage = currentRetry == 0
                        ? string.Empty
                        : $"Retry: {currentRetry}'.";

                    var preLogMessage = $"Handling a message: '{messageName}' " +
                                        $"with correlation id: '{correlationContext.CorrelationId}'. {retryMessage}";

                    _logger?.LogInformation(preLogMessage);

                    await handle(_serviceProvider, message, correlationContext);

                    var postLogMessage = $"Handled a message: '{messageName}' " +
                                         $"with correlation id: '{correlationContext.CorrelationId}'. {retryMessage}";
                    _logger?.LogInformation(postLogMessage);

                    return null;
                }
                catch (Exception ex)
                {
                    currentRetry++;
                    _logger?.LogError(ex, ex.Message);
                    var rejectedEvent = _exceptionToMessageMapper.Map(ex, message);
                    if (rejectedEvent is null)
                    {
                        throw new Exception($"Unable to handle a message: '{messageName}' " +
                                            $"with correlation id: '{correlationContext.CorrelationId}', " +
                                            $"retry {currentRetry - 1}/{_retries}...", ex);
                    }

                    var rejectedEventName = rejectedEvent.GetType().Name;
                    await _publisher.PublishAsync(rejectedEvent, correlationContext);
                    _logger?.LogWarning($"Published a rejected event: '{rejectedEventName}' " +
                                       $"for the message: '{messageName}' with correlation id: '{correlationContext.CorrelationId}'.");

                    return new Exception($"Handling a message: '{messageName}' failed and rejected event: " +
                                         $"'{rejectedEventName}' was published.", ex);
                }
            });
        }

        private class EmptyExceptionToMessageMapper : IExceptionToMessageMapper
        {
            public object Map(Exception exception, object message) => null;
        }
    }
}