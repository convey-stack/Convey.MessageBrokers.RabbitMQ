using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;

namespace Convey.MessageBrokers.RabbitMQ.Middleware
{
    public class UniqueMessagesMiddleware : IUniqueMessagesMiddleware
    {
        private readonly IMessageProcessor _messageProcessor;
        private readonly ILogger<UniqueMessagesMiddleware> _logger;

        public UniqueMessagesMiddleware(IMessageProcessor messageProcessor, ILogger<UniqueMessagesMiddleware> logger)
        {
            _messageProcessor = messageProcessor;
            _logger = logger;
        }

        public async Task HandleAsync(Func<Task> next, object message,
            ICorrelationContext correlationContext,
            BasicDeliverEventArgs args)
        {
            var messageId = args.BasicProperties.MessageId;
            _logger.LogTrace($"Received a unique message with id: {messageId} to be processed.");
            if (!await _messageProcessor.TryProcessAsync(messageId))
            {
                _logger.LogTrace($"A unique message with id: {messageId} was already processed.");
                return;
            }

            try
            {
                _logger.LogTrace($"Processing a unique message with id: {messageId}...");
                await next();
                _logger.LogTrace($"Processed a unique message with id: {messageId}.");
            }
            catch
            {
                _logger.LogTrace($"There was an error when processing a unique message with id: {messageId}.");
                await _messageProcessor.RemoveAsync(messageId);
                throw;
            }
        }
    }
}