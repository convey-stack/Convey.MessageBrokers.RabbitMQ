using System;
using System.Reflection;
using System.Threading.Tasks;
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
        private readonly string _defaultNamespace;
        private readonly int _retries;
        private readonly int _retryInterval;

        public BusSubscriber(IApplicationBuilder app)
        {
            _logger = app.ApplicationServices.GetService<ILogger<BusSubscriber>>();
            _serviceProvider = app.ApplicationServices.GetService<IServiceProvider>();
            _busClient = _serviceProvider.GetService<IBusClient>();
            var options = _serviceProvider.GetService<RabbitMqOptions>();
            _defaultNamespace = options.Namespace;
            _retries = options.Retries >= 0 ? options.Retries : 3;
            _retryInterval = options.RetryInterval > 0 ? options.RetryInterval : 2;
        }

        public IBusSubscriber SubscribeMessage<TMessage>(Func<IServiceProvider, Task> handle, string @namespace = null, 
            string queueName = null, Func<TMessage, ConveyException, IMessage> onError = null)
            where TMessage : IMessage
        {
            _busClient.SubscribeAsync<TMessage, CorrelationContext>(async (command, correlationContext) =>
                {
                    return await TryHandleAsync(command, correlationContext, () => handle(_serviceProvider), onError);
                },
                ctx => ctx.UseSubscribeConfiguration(cfg =>
                    cfg.FromDeclaredQueue(q => q.WithName(GetQueueName<TMessage>(@namespace, queueName)))));

            return this;
        }

        private async Task<Acknowledgement> TryHandleAsync<TMessage>(TMessage message,
            CorrelationContext correlationContext,
            Func<Task> handle, Func<TMessage, ConveyException, IMessage> onError = null)
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

                    await handle();

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

        private string GetQueueName<T>(string @namespace = null, string name = null)
        {
            @namespace = string.IsNullOrWhiteSpace(@namespace)
                ? (string.IsNullOrWhiteSpace(_defaultNamespace) ? string.Empty : _defaultNamespace)
                : @namespace;

            var separatedNamespace = string.IsNullOrWhiteSpace(@namespace) ? string.Empty : $"{@namespace}.";

            return (string.IsNullOrWhiteSpace(name)
                ? $"{Assembly.GetEntryAssembly().GetName().Name}/{separatedNamespace}{typeof(T).Name.Underscore()}"
                : $"{name}/{separatedNamespace}{typeof(T).Name.Underscore()}").ToLowerInvariant();
        }
    }
}