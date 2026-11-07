using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Foundation.Logging;

namespace SunEyeVision.Core.Events
{
    /// <summary>
    /// Async event bus - provides decoupled async event-based communication between modules
    /// Supports two publishing modes: fire-and-forget and wait-for-all
    /// </summary>
    public class AsyncEventBus : IAsyncEventBus
    {
        /// <summary>
        /// Event subscriptions dictionary
        /// </summary>
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers;

        /// <summary>
        /// Logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Lock object for thread-safe operations
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// Cancellation token source
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Statistics fields (using long for thread-safe operations with Interlocked)
        /// </summary>
        private long _totalSubscriptions;
        private long _totalUnsubscriptions;
        private long _totalPublishedEvents;
        private long _totalExecutedHandlers;
        private long _failedHandlerExecutions;

        /// <summary>
        /// Event bus statistics
        /// </summary>
        public AsyncEventBusStatistics Statistics => new AsyncEventBusStatistics
        {
            TotalSubscriptions = _totalSubscriptions,
            TotalUnsubscriptions = _totalUnsubscriptions,
            TotalPublishedEvents = _totalPublishedEvents,
            TotalExecutedHandlers = _totalExecutedHandlers,
            FailedHandlerExecutions = _failedHandlerExecutions
        };

        public AsyncEventBus(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _handlers = new ConcurrentDictionary<Type, List<Delegate>>();
            _cancellationTokenSource = new CancellationTokenSource();
            _logger.LogInfo("AsyncEventBus initialized");
        }

        /// <summary>
        /// Subscribe to an async event
        /// </summary>
        public void Subscribe<TEvent>(AsyncEventHandler<TEvent> handler) where TEvent : IEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var eventType = typeof(TEvent);

            lock (_lock)
            {
                if (!_handlers.ContainsKey(eventType))
                {
                    _handlers[eventType] = new List<Delegate>();
                }

                _handlers[eventType].Add(handler);
            }

            _logger.LogInfo($"Subscribed to async event: {eventType.Name}, total handlers: {_handlers[eventType].Count}");
            Interlocked.Increment(ref _totalSubscriptions);
        }

        /// <summary>
        /// Unsubscribe from an async event
        /// </summary>
        public void Unsubscribe<TEvent>(AsyncEventHandler<TEvent> handler) where TEvent : IEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var eventType = typeof(TEvent);

            lock (_lock)
            {
                if (_handlers.TryGetValue(eventType, out var handlers))
                {
                    handlers.Remove(handler);

                    if (handlers.Count == 0)
                    {
                        _handlers.TryRemove(eventType, out _);
                    }

                    _logger.LogInfo($"Unsubscribed from async event: {eventType.Name}, remaining handlers: {handlers.Count}");
                }
            }

            Interlocked.Increment(ref _totalUnsubscriptions);
        }

        /// <summary>
        /// Publish an event (fire-and-forget mode) - does not wait for handlers to complete
        /// </summary>
        public async Task PublishAsync<TEvent>(TEvent eventData) where TEvent : IEvent
        {
            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData));
            }

            var eventType = typeof(TEvent);

            _logger.LogInfo($"Publishing async event (fire-and-forget): {eventType.Name} from {eventData.Source}");

            Interlocked.Increment(ref _totalPublishedEvents);

            List<Delegate> handlersToInvoke;
            lock (_lock)
            {
                if (!_handlers.TryGetValue(eventType, out var handlers))
                {
                    _logger.LogInfo($"No handlers registered for async event: {eventType.Name}");
                    return;
                }

                // Create a copy of handlers to avoid issues with handlers being added/removed during invocation
                handlersToInvoke = handlers.ToList();
            }

            // Fire and forget: invoke all handlers in parallel without awaiting them
            var invocationTasks = handlersToInvoke.Select(handler =>
            {
                try
                {
                    var typedHandler = handler as AsyncEventHandler<TEvent>;
                    if (typedHandler != null)
                    {
                        // Start the task but don't await it
                        return Task.Run(async () =>
                        {
                            try
                            {
                                await typedHandler(eventData);
                                Interlocked.Increment(ref _totalExecutedHandlers);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Error executing async handler for event {eventType.Name}: {ex.Message}", ex);
                                Interlocked.Increment(ref _failedHandlerExecutions);
                            }
                        }, _cancellationTokenSource.Token);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error starting async handler for event {eventType.Name}: {ex.Message}", ex);
                    Interlocked.Increment(ref _failedHandlerExecutions);
                }
                return Task.CompletedTask;
            });

            await Task.WhenAll(invocationTasks);
        }

        /// <summary>
        /// Publish an event and wait for all handlers to complete
        /// </summary>
        public async Task PublishAndWaitAsync<TEvent>(TEvent eventData) where TEvent : IEvent
        {
            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData));
            }

            var eventType = typeof(TEvent);

            _logger.LogInfo($"Publishing async event (wait-all): {eventType.Name} from {eventData.Source}");

            Interlocked.Increment(ref _totalPublishedEvents);

            List<Delegate> handlersToInvoke;
            lock (_lock)
            {
                if (!_handlers.TryGetValue(eventType, out var handlers))
                {
                    _logger.LogInfo($"No handlers registered for async event: {eventType.Name}");
                    return;
                }

                handlersToInvoke = handlers.ToList();
            }

            // Wait for all: invoke all handlers and wait for completion
            var executionTasks = handlersToInvoke.Select(async handler =>
            {
                try
                {
                    var typedHandler = handler as AsyncEventHandler<TEvent>;
                    if (typedHandler != null)
                    {
                        await typedHandler(eventData);
                        Interlocked.Increment(ref _totalExecutedHandlers);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error executing async handler for event {eventType.Name}: {ex.Message}", ex);
                    Interlocked.Increment(ref _failedHandlerExecutions);
                }
            });

            await Task.WhenAll(executionTasks);
        }

        /// <summary>
        /// Clear all subscriptions
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                var count = _handlers.Count;
                _handlers.Clear();
                _logger.LogInfo($"Cleared {count} async event subscriptions");
                _totalSubscriptions = 0;
                _totalUnsubscriptions = 0;
                _totalPublishedEvents = 0;
                _totalExecutedHandlers = 0;
                _failedHandlerExecutions = 0;
            }
        }

        /// <summary>
        /// Get statistics about event bus usage
        /// </summary>
        public string GetStatistics()
        {
            return $"AsyncEventBus Statistics:\n" +
                   $"  - Total subscriptions: {_totalSubscriptions}\n" +
                   $"  - Total unsubscriptions: {_totalUnsubscriptions}\n" +
                   $"  - Active subscriptions: {GetActiveSubscriptionCount()}\n" +
                   $"  - Total published events: {_totalPublishedEvents}\n" +
                   $"  - Total executed handlers: {_totalExecutedHandlers}\n" +
                   $"  - Failed handler executions: {_failedHandlerExecutions}";
        }

        /// <summary>
        /// Get the number of active subscriptions
        /// </summary>
        private int GetActiveSubscriptionCount()
        {
            lock (_lock)
            {
                return _handlers.Values.Sum(h => h.Count);
            }
        }

        /// <summary>
        /// Dispose async event bus resources
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            Clear();
            _logger.LogInfo("AsyncEventBus disposed");
        }
    }

    /// <summary>
    /// Async event bus statistics
    /// </summary>
    public class AsyncEventBusStatistics
    {
        /// <summary>
        /// Total number of subscriptions
        /// </summary>
        public long TotalSubscriptions { get; set; }

        /// <summary>
        /// Total number of unsubscriptions
        /// </summary>
        public long TotalUnsubscriptions { get; set; }

        /// <summary>
        /// Total number of published events
        /// </summary>
        public long TotalPublishedEvents { get; set; }

        /// <summary>
        /// Total number of successfully executed handlers
        /// </summary>
        public long TotalExecutedHandlers { get; set; }

        /// <summary>
        /// Total number of failed handler executions
        /// </summary>
        public long FailedHandlerExecutions { get; set; }
    }
}
