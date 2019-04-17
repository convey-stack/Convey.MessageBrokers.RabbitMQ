using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Convey.MessageBrokers.RabbitMQ.Builders;
using Convey.MessageBrokers.RabbitMQ.Publishers;
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
        
        public static IBusSubscriber UseRabbitMq(this IApplicationBuilder app)
            => new BusSubscriber(app);

        public static IConveyBuilder AddRabbitMq(this IConveyBuilder builder, string sectionName = SectionName)
        {
            var options = builder.GetOptions<RabbitMqOptions>(sectionName);
            return builder.AddRabbitMq(options);
        }
        
        public static IConveyBuilder AddRabbitMq(this IConveyBuilder builder, 
            Func<IRabbitMqOptionsBuilder, IRabbitMqOptionsBuilder> buildOptions)
        {
            var options = buildOptions(new RabbitMqOptionsBuilder()).Build();
            return builder.AddRabbitMq(options);
        }

        public static IConveyBuilder AddRabbitMq(this IConveyBuilder builder, RabbitMqOptions options)
        {
            if (!builder.TryRegister(RegistryName))
            {
                return builder;
            }
            
            builder.Services.AddSingleton(options);
            builder.Services.AddSingleton<RawRabbitConfiguration>(options);
            builder.Services.AddTransient<IBusPublisher, BusPublisher>();

            ConfigureBus(builder);

            return builder;
        }
        
        internal static string Underscore(this string value)
            => string.Concat(value.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString()));

        private static void ConfigureBus(IConveyBuilder builder)
        {
            builder.Services.AddSingleton<IInstanceFactory>(serviceProvider =>
            {
                var options = serviceProvider.GetService<RabbitMqOptions>();
                var configuration = serviceProvider.GetService<RawRabbitConfiguration>();
                var namingConventions = new CustomNamingConventions(options.Namespace);

                return RawRabbitFactory.CreateInstanceFactory(new RawRabbitOptions
                {
                    DependencyInjection = ioc =>
                    {
                        ioc.AddSingleton(options);
                        ioc.AddSingleton(configuration);
                        ioc.AddSingleton<INamingConventions>(namingConventions);
                    },
                    Plugins = p => p
                        .UseAttributeRouting()
                        .UseRetryLater()
                        .UpdateRetryInfo()
                        .UseMessageContext<CorrelationContext>()
                        .UseContextForwarding()
                });
            });
            
            builder.Services.AddTransient<IBusClient>(serviceProvider => serviceProvider.GetService<IInstanceFactory>().Create());
        }

        private class CustomNamingConventions : NamingConventions
        {
            public CustomNamingConventions(string defaultNamespace)
            {
                ExchangeNamingConvention = type => GetNamespace(type, defaultNamespace).ToLowerInvariant();
                RoutingKeyConvention = type =>
                    $"#.{GetRoutingKeyNamespace(type, defaultNamespace)}{type.Name.Underscore()}".ToLowerInvariant();
                ErrorExchangeNamingConvention = () => $"{defaultNamespace}.error";
                RetryLaterExchangeConvention = span => $"{defaultNamespace}.retry";
                RetryLaterQueueNameConvetion = (exchange, span) =>
                    $"{defaultNamespace}.retry_for_{exchange.Replace(".", "_")}_in_{span.TotalMilliseconds}_ms".ToLowerInvariant();
            }

            private static string GetRoutingKeyNamespace(Type type, string defaultNamespace)
            {
                var @namespace = type.GetCustomAttribute<MessageNamespaceAttribute>()?.Namespace ?? defaultNamespace;

                return string.IsNullOrWhiteSpace(@namespace) ? string.Empty : $"{@namespace}.";
            }
            
            private static string GetNamespace(Type type, string defaultNamespace)
            {
                var @namespace = type.GetCustomAttribute<MessageNamespaceAttribute>()?.Namespace ?? defaultNamespace;

                return string.IsNullOrWhiteSpace(@namespace) ? "#" : $"{@namespace}";
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