using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Foundation.Logging;

namespace SunEyeVision.Core.Events
{
    /// <summary>
    /// Event bus - provides decoupled event-based communication between modules
    /// </summary>
    public class EventBus : IEventBus, IDisposable
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
        /// Event bus statistics
        /// </summary>
        public EventBusStatistics Statistics { get; private set; }

        public EventBus(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _handlers = new ConcurrentDictionary<Type, List<Delegate>>();
            Statistics = new EventBusStatistics();
            _logger.LogInfo("EventBus initialized");
        }

        /// <summary>
        /// Subscribe to an event
        /// </summary>
        /// <typeparam name="TEvent">Event type</typeparam>
        /// <param name="handler">Event handler</param>
        public void Subscribe<TEvent>(EventHandler<TEvent> handler) where TEvent : IEvent
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

            _logger.LogInfo($"Subscribed to event: {eventType.Name}, total handlers: {_handlers[eventType].Count}");
            Statistics.TotalSubscriptions++;
        }

        /// <summary>
        /// Unsubscribe from an event
        /// </summary>
        /// <typeparam name="TEvent">Event type</typeparam>
        /// <param name="handler">Event handler</param>
        public void Unsubscribe<TEvent>(EventHandler<TEvent> handler) where TEvent : IEvent
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

                    _logger.LogInfo($"Unsubscribed from event: {eventType.Name}, remaining handlers: {handlers.Count}");
                }
            }

            Statistics.TotalUnsubscriptions++;
        }

        /// <summary>
        /// Publish an event to all subscribers
        /// </summary>
        /// <typeparam name="TEvent">Event type</typeparam>
        /// <param name="eventData">Event data</param>
        public void Publish<TEvent>(TEvent eventData) where TEvent : IEvent
        {
            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData));
            }

            var eventType = typeof(TEvent);

            _logger.LogInfo($"Publishing event: {eventType.Name} from {eventData.Source}");

            Statistics.TotalPublishedEvents++;

            lock (_lock)
            {
                if (!_handlers.TryGetValue(eventType, out var handlers))
                {
                    _logger.LogInfo($"No handlers registered for event: {eventType.Name}");
                    return;
                }

                // Create a copy of handlers to avoid issues with handlers being added/removed during invocation
                var handlersToInvoke = handlers.ToArray();

                foreach (var handler in handlersToInvoke)
                {
                    try
                    {
                        var typedHandler = handler as EventHandler<TEvent>;
                        if (typedHandler != null)
                        {
                            typedHandler(eventData);
                            Statistics.TotalExecutedHandlers++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error executing handler for event {eventType.Name}: {ex.Message}", ex);
                        Statistics.FailedHandlerExecutions++;
                    }
                }
            }
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
                _logger.LogInfo($"Cleared {count} event subscriptions");
                Statistics = new EventBusStatistics();
            }
        }

        /// <summary>
        /// Get statistics about event bus usage
        /// </summary>
        public string GetStatistics()
        {
            return $"EventBus Statistics:\n" +
                   $"  - Total subscriptions: {Statistics.TotalSubscriptions}\n" +
                   $"  - Total unsubscriptions: {Statistics.TotalUnsubscriptions}\n" +
                   $"  - Active subscriptions: {GetActiveSubscriptionCount()}\n" +
                   $"  - Total published events: {Statistics.TotalPublishedEvents}\n" +
                   $"  - Total executed handlers: {Statistics.TotalExecutedHandlers}\n" +
                   $"  - Failed handler executions: {Statistics.FailedHandlerExecutions}";
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
        /// Dispose event bus resources
        /// </summary>
        public void Dispose()
        {
            Clear();
            _logger.LogInfo("EventBus disposed");
        }
    }

    /// <summary>
    /// Event bus statistics
    /// </summary>
    public class EventBusStatistics
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
