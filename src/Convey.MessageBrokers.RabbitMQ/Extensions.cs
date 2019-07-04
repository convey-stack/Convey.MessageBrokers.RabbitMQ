using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Convey.MessageBrokers.RabbitMQ.Builders;
using Convey.MessageBrokers.RabbitMQ.Publishers;
using Convey.MessageBrokers.RabbitMQ.Registers;
using Convey.MessageBrokers.RabbitMQ.Subscribers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using RawRabbit;
using RawRabbit.Common;
using RawRabbit.Configuration;
using RawRabbit.Enrichers.MessageContext;
using RawRabbit.Instantiation;
using RawRabbit.Pipe;
using RawRabbit.Pipe.Middleware;

namespace Convey.MessageBrokers.RabbitMQ
{
    public static class Extensions
    {
        private const string SectionName = "rabbitMq";
        private const string RegistryName = "messageBrokers.rabbitMq";

        internal static string GetMessageName(this object message)
            => message.GetType().Name.Underscore().ToLowerInvariant();
        
        public static IBusSubscriber UseRabbitMq(this IApplicationBuilder app)
            => new BusSubscriber(app);

        public static IConveyBuilder AddExceptionToMessageMapper<T>(this IConveyBuilder builder)
            where T : class, IExceptionToMessageMapper
        {
            builder.Services.AddTransient<IExceptionToMessageMapper, T>();

            return builder;
        }

        public static IConveyBuilder AddRabbitMq(this IConveyBuilder builder, string sectionName = SectionName,
            Func<IRabbitMqPluginRegister, IRabbitMqPluginRegister> registerPlugins = null)
        {
            var options = builder.GetOptions<RabbitMqOptions>(sectionName);
            return builder.AddRabbitMq(options, registerPlugins);
        }
        
        public static IConveyBuilder AddRabbitMq(this IConveyBuilder builder, Func<IRabbitMqOptionsBuilder, IRabbitMqOptionsBuilder> buildOptions, 
            Func<IRabbitMqPluginRegister, IRabbitMqPluginRegister> registerPlugins = null)
        {
            var options = buildOptions(new RabbitMqOptionsBuilder()).Build();
            return builder.AddRabbitMq(options, registerPlugins);
        }

        public static IConveyBuilder AddRabbitMq(this IConveyBuilder builder, RabbitMqOptions options, 
            Func<IRabbitMqPluginRegister, IRabbitMqPluginRegister> registerPlugins = null)
        {
            if (!builder.TryRegister(RegistryName))
            {
                return builder;
            }
            
            builder.Services.AddSingleton(options);
            builder.Services.AddSingleton<RawRabbitConfiguration>(options);
            builder.Services.AddTransient<IBusPublisher, BusPublisher>();
            builder.Services.AddSingleton<ICorrelationContextAccessor>(new CorrelationContextAccessor());

            ConfigureBus(builder, registerPlugins);

            return builder;
        }

        private static void ConfigureBus(IConveyBuilder builder,
            Func<IRabbitMqPluginRegister, IRabbitMqPluginRegister> registerPlugins = null)
        {
            builder.Services.AddSingleton<IInstanceFactory>(serviceProvider =>
            {
                var register = registerPlugins?.Invoke(new RabbitMqPluginRegister(serviceProvider));
                var options = serviceProvider.GetService<RabbitMqOptions>();
                var configuration = serviceProvider.GetService<RawRabbitConfiguration>();
                var namingConventions = new CustomNamingConventions(options.Namespace);

                return RawRabbitFactory.CreateInstanceFactory(new RawRabbitOptions
                {
                    DependencyInjection = ioc =>
                    {
                        register?.Register(ioc);
                        ioc.AddSingleton(options);
                        ioc.AddSingleton(configuration);
                        ioc.AddSingleton<INamingConventions>(namingConventions);
                    },
                    Plugins = p =>
                    {
                        register?.Register(p);
                        p.UseAttributeRouting()
                            .UseRetryLater()
                            .UpdateRetryInfo()
                            .UseMessageContext<CorrelationContext>()
                            .UseContextForwarding();
                    }
                });
            });

            builder.Services.AddTransient(serviceProvider => serviceProvider.GetService<IInstanceFactory>().Create());
        }

        private class CustomNamingConventions : NamingConventions
        {
            public CustomNamingConventions(string defaultNamespace)
            {
                var assemblyName = Assembly.GetEntryAssembly().GetName().Name;
                ExchangeNamingConvention = type => GetExchange(type, defaultNamespace);
                RoutingKeyConvention = type => GetRoutingKey(type, defaultNamespace);
                QueueNamingConvention = type => GetQueueName(assemblyName, type, defaultNamespace);
                ErrorExchangeNamingConvention = () => $"{defaultNamespace}.error";
                RetryLaterExchangeConvention = span => $"{defaultNamespace}.retry";
                RetryLaterQueueNameConvetion = (exchange, span) =>
                    $"{defaultNamespace}.retry_for_{exchange.Replace(".", "_")}_in_{span.TotalMilliseconds}_ms".ToLowerInvariant();
            }
            
            private static string GetExchange(Type type, string defaultNamespace)
            {
                var (@namespace, key) = GetNamespaceAndKey(type, defaultNamespace);

                return (string.IsNullOrWhiteSpace(@namespace) ? key : $"{@namespace}").ToLowerInvariant();
            }
            
            private static string GetRoutingKey(Type type, string defaultNamespace)
            {
                var (@namespace, key) = GetNamespaceAndKey(type, defaultNamespace);
                var separatedNamespace = string.IsNullOrWhiteSpace(@namespace) ? string.Empty : $"{@namespace}.";

                return $"{separatedNamespace}{key}".ToLowerInvariant();
            }

            private static string GetQueueName(string assemblyName, Type type, string defaultNamespace)
            {
                var (@namespace, key) = GetNamespaceAndKey(type, defaultNamespace);
                var separatedNamespace = string.IsNullOrWhiteSpace(@namespace) ? string.Empty : $"{@namespace}.";

                return $"{assemblyName}/{separatedNamespace}{key}".ToLowerInvariant();
            }

            private static (string @namespace, string key) GetNamespaceAndKey(Type type, string defaultNamespace)
            {
                var attribute = type.GetCustomAttribute<MessageNamespaceAttribute>();
                var @namespace = attribute?.Namespace ?? defaultNamespace;
                var key = string.IsNullOrWhiteSpace(attribute?.Key) ? type.Name.Underscore() : attribute.Key;

                return (@namespace, key);
            }
        }

        private class RetryStagedMiddleware : StagedMiddleware
        {
            public override string StageMarker { get; } = RawRabbit.Pipe.StageMarker.MessageDeserialized;

            public override async Task InvokeAsync(IPipeContext context,
                CancellationToken token = new CancellationToken())
            {
                var retry = context.GetRetryInformation();
                if (context.GetMessageContext() is CorrelationContext message)
                {
                    message.Retries = retry.NumberOfRetries;
                }

                await Next.InvokeAsync(context, token);
            }
        }

        private static IClientBuilder UpdateRetryInfo(this IClientBuilder clientBuilder)
        {
            clientBuilder.Register(c => c.Use<RetryStagedMiddleware>());

            return clientBuilder;
        }
    }
}