using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace Convey.MessageBrokers.RabbitMQ.Processors
{
    public class InMemoryMessageProcessor : IMessageProcessor
    {
        private readonly IMemoryCache _cache;

        public InMemoryMessageProcessor(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<bool> TryProcessAsync(string id)
        {
            var key = $"messages:{id}";
            if (_cache.TryGetValue(key, out _))
            {
                return Task.FromResult(false);
            }

            _cache.Set(key, id, TimeSpan.FromMinutes(5));

            return Task.FromResult(true);
        }
    }
}