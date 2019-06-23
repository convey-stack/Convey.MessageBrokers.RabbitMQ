using System;
using System.Reflection;
using System.Threading.Tasks;
using Convey.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using RawRabbit;
using RawRabbit.Common;
using RawRabbit.Enrichers.MessageContext;

namespace Convey.MessageBrokers.RabbitMQ.Subscribers
{
    internal sealed class BusSubscriber : IBusSubscriber
    {
        private readonly ILogger _logger;
        private readonly IBusClient _busClient;
        private readonly IServiceProvider _serviceProvider;
        private readonly int _retries;
        private readonly int _retryInterval;

        public BusSubscriber(IApplicationBuilder app)
        {
            _logger = app.ApplicationServices.GetService<ILogger<BusSubscriber>>();
            _serviceProvider = app.ApplicationServices.GetService<IServiceProvider>();
            _busClient = _serviceProvider.GetService<IBusClient>();
            var options = _serviceProvider.GetService<RabbitMqOptions>();
            _retries = options.Retries >= 0 ? options.Retries : 3;
            _retryInterval = options.RetryInterval > 0 ? options.RetryInterval : 2;
        }

        public IBusSubscriber Subscribe<TMessage>(
            Func<IServiceProvider, TMessage, ICorrelationContext, Task> handle,
            Func<TMessage, Exception, object> onError = null)
            where TMessage : class
        {
            _busClient.SubscribeAsync<TMessage, CorrelationContext>(async (message, correlationContext) =>
            {
                try
                {
                    var accessor = _serviceProvider.GetService<ICorrelationContextAccessor>();
                    accessor.CorrelationContext = correlationContext;

                    return await TryHandleAsync(message, correlationContext, handle, onError);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            });

            return this;
        }

        private async Task<Acknowledgement> TryHandleAsync<TMessage>(TMessage message,
            ICorrelationContext correlationContext, Func<IServiceProvider, TMessage, ICorrelationContext, Task> handle,
            Func<TMessage, Exception, object> onError = null)
        {
            var currentRetry = 0;
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(_retries, i => TimeSpan.FromSeconds(_retryInterval));

            var messageName = message.GetType().Name;

            return await retryPolicy.ExecuteAsync<Acknowledgement>(async () =>
            {
                try
                {
                    var retryMessage = currentRetry == 0
                        ? string.Empty
                        : $"Retry: {currentRetry}'.";

                    var preLogMessage = $"Handling a message: '{messageName}' " +
                                        $"with correlation id: '{correlationContext.Id}'. {retryMessage}";

                    _logger.LogInformation(preLogMessage);

                    await handle(_serviceProvider, message, correlationContext);

                    var postLogMessage = $"Handled a message: '{messageName}' " +
                                         $"with correlation id: '{correlationContext.Id}'. {retryMessage}";
                    _logger.LogInformation(postLogMessage);

                    return new Ack();
                }
                catch (Exception exception)
                {
                    currentRetry++;
                    _logger.LogError(exception, exception.Message);

                    if (exception is ConveyException conveyException && onError != null)
                    {
                        var errorMessage = onError(message, conveyException);
                        await _busClient.PublishAsync(errorMessage, ctx => ctx.UseMessageContext(correlationContext));
                        _logger.LogInformation($"Published a rejected event: '{errorMessage.GetType().Name}' " +
                                               $"for the message: '{messageName}' with correlation id: '{correlationContext.Id}'.");

                        return new Ack();
                    }

                    throw new Exception($"Unable to handle a message: '{messageName}' " +
                                        $"with correlation id: '{correlationContext.Id}', " +
                                        $"retry {currentRetry - 1}/{_retries}...");
                }

            });
        }
    }
}