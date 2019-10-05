using System.Threading.Tasks;
using Convey.MessageBrokers.RabbitMQ.Clients;
using Convey.MessageBrokers.RabbitMQ.Contexts;
using Convey.MessageBrokers.RabbitMQ.Conventions;
using Convey.MessageBrokers.RabbitMQ.Middleware;
using Convey.MessageBrokers.RabbitMQ.Processors;
using Convey.MessageBrokers.RabbitMQ.Publishers;
using Convey.MessageBrokers.RabbitMQ.Serializers;
using Convey.MessageBrokers.RabbitMQ.Subscribers;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Convey.MessageBrokers.RabbitMQ
{
    public static class RabbitExtensions
    {
        private const string SectionName = "rabbitmq";
        private const string RegistryName = "messageBrokers.rabbitmq";

        public static IConveyBuilder AddRabbitMq(this IConveyBuilder builder, string sectionName = SectionName)
        {
            var options = builder.GetOptions<RabbitMqOptions>(sectionName);
            builder.Services.AddSingleton(options);
            if (!builder.TryRegister(RegistryName))
            {
                return builder;
            }

            builder.Services.AddSingleton<IContextProvider, ContextProvider>();
            builder.Services.AddSingleton<ICorrelationContextAccessor>(new CorrelationContextAccessor());
            builder.Services.AddSingleton<IConventionsBuilder, ConventionsBuilder>();
            builder.Services.AddSingleton<IConventionsProvider, ConventionsProvider>();
            builder.Services.AddSingleton<IConventionsRegistry, ConventionsRegistry>();
            builder.Services.AddSingleton<IRabbitMqSerializer, NewtonsoftJsonRabbitMqSerializer>();
            builder.Services.AddTransient<IRabbitMqClient, RabbitMqClient>();
            builder.Services.AddTransient<IPublisher, Publisher>();
            builder.Services.AddTransient<ISubscriber, Subscriber>();
            if (options.MessageProcessor?.Enabled == true)
            {
                builder.Services.AddTransient<IRabbitMqMiddleware, UniqueMessagesMiddleware>();
                builder.Services.AddTransient<IUniqueMessagesMiddleware, UniqueMessagesMiddleware>();
                switch (options.MessageProcessor.Type?.ToLowerInvariant())
                {
                    default:
                        builder.Services.AddMemoryCache();
                        builder.Services.AddTransient<IMessageProcessor, InMemoryMessageProcessor>();
                        break;
                }
            }
            else
            {
                builder.Services.AddSingleton<IMessageProcessor, EmptyMessageProcessor>();
            }

            builder.Services.AddSingleton(sp =>
            {
                var connectionFactory = new ConnectionFactory
                {
                    HostName = options.HostName,
                    Port = options.Port,
                    VirtualHost = options.VirtualHost,
                    UserName = options.Username,
                    Password = options.Password,
                    RequestedConnectionTimeout = options.RequestedConnectionTimeout,
                    SocketReadTimeout = options.SocketReadTimeout,
                    SocketWriteTimeout = options.SocketWriteTimeout,
                    RequestedChannelMax = options.RequestedChannelMax,
                    RequestedFrameMax = options.RequestedFrameMax,
                    RequestedHeartbeat = options.RequestedHeartbeat,
                    UseBackgroundThreadsForIO = options.UseBackgroundThreadsForIO,
                    Ssl = options.Ssl is null
                        ? new SslOption()
                        : new SslOption(options.Ssl.ServerName, options.Ssl.CertificatePath, options.Ssl.Enabled)
                };

                return connectionFactory.CreateConnection();
            });

            return builder;
        }

        public static IConveyBuilder AddExceptionToMessageMapper<T>(this IConveyBuilder builder)
            where T : class, IExceptionToMessageMapper
        {
            builder.Services.AddTransient<IExceptionToMessageMapper, T>();

            return builder;
        }

        private class EmptyMessageProcessor : IMessageProcessor
        {
            public Task<bool> TryProcessAsync(string id) => Task.FromResult(true);

            public Task RemoveAsync(string id) => Task.CompletedTask;
        }
    }
}