using System;
using RawRabbit.DependencyInjection;
using RawRabbit.Instantiation;

namespace Convey.MessageBrokers.RabbitMQ
{
    public interface IRabbitMqPluginRegister
    {
        IServiceProvider ServiceProvider { get; }
        IRabbitMqPluginRegister AddPlugin(Action<IClientBuilder> buildClient, Action<IDependencyRegister> registerDependencies = null);
        void Register(IDependencyRegister ioc);
        void Register(IClientBuilder builder);
    }
}