using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Core;
using Events = SunEyeVision.Core.Events;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// Hardware trigger configuration
    /// </summary>
    public class HardwareTriggerConfig
    {
        /// <summary>
        /// IO pin number
        /// </summary>
        public int IoPin { get; set; }

        /// <summary>
        /// Trigger edge (Rising, Falling, Both)
        /// </summary>
        public TriggerEdge Edge { get; set; } = TriggerEdge.Rising;

        /// <summary>
        /// Debounce time in milliseconds
        /// </summary>
        public int DebounceMs { get; set; } = 10;
    }

    /// <summary>
    /// Software trigger configuration
    /// </summary>
    public class SoftwareTriggerConfig
    {
        /// <summary>
        /// Trigger name/identifier
        /// </summary>
        public string TriggerName { get; set; }

        /// <summary>
        /// Trigger key (for API calls)
        /// </summary>
        public string TriggerKey { get; set; }
    }

    /// <summary>
    /// Timer trigger configuration
    /// </summary>
    public class TimerTriggerConfig
    {
        /// <summary>
        /// Interval in milliseconds
        /// </summary>
        public int IntervalMs { get; set; } = 1000;

        /// <summary>
        /// Initial delay in milliseconds
        /// </summary>
        public int InitialDelayMs { get; set; } = 0;
    }

    /// <summary>
    /// Trigger edge enumeration
    /// </summary>
    public enum TriggerEdge
    {
        Rising,
        Falling,
        Both
    }

    /// <summary>
    /// Unified trigger manager for hardware, software, and timer triggers
    /// </summary>
    public class TriggerManager : IDisposable
    {
        private readonly ILogger _logger;
        private readonly Events.IAsyncEventBus _eventBus;
        private readonly Dictionary<string, HardwareTriggerConfig> _hardwareTriggers;
        private readonly Dictionary<string, SoftwareTriggerConfig> _softwareTriggers;
        private readonly Dictionary<string, TimerTriggerConfig> _timerTriggers;
        private readonly Dictionary<string, CancellationTokenSource> _timerCancellations;
        private readonly object _lock = new object();

        private bool _disposed = false;

        /// <summary>
        /// Trigger fired event
        /// </summary>
        public event EventHandler<TriggerFiredEventArgs> TriggerFired;

        public TriggerManager(ILogger logger, Events.IAsyncEventBus eventBus)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _hardwareTriggers = new Dictionary<string, HardwareTriggerConfig>();
            _softwareTriggers = new Dictionary<string, SoftwareTriggerConfig>();
            _timerTriggers = new Dictionary<string, TimerTriggerConfig>();
            _timerCancellations = new Dictionary<string, CancellationTokenSource>();

            _logger.LogInfo("TriggerManager initialized");
        }

        #region Hardware Triggers

        /// <summary>
        /// Register a hardware trigger
        /// </summary>
        public void RegisterHardwareTrigger(string triggerId, HardwareTriggerConfig config)
        {
            if (string.IsNullOrEmpty(triggerId))
            {
                throw new ArgumentException("Trigger ID cannot be null or empty", nameof(triggerId));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            lock (_lock)
            {
                _hardwareTriggers[triggerId] = config;
                _logger.LogInfo($"Hardware trigger registered: {triggerId}, IO Pin={config.IoPin}, Edge={config.Edge}");
            }
        }

        /// <summary>
        /// Unregister a hardware trigger
        /// </summary>
        public void UnregisterHardwareTrigger(string triggerId)
        {
            lock (_lock)
            {
                if (_hardwareTriggers.Remove(triggerId))
                {
                    _logger.LogInfo($"Hardware trigger unregistered: {triggerId}");
                }
            }
        }

        /// <summary>
        /// Fire a hardware trigger (called by device driver)
        /// </summary>
        public void FireHardwareTrigger(string triggerId, HImage image = null, object metadata = null)
        {
            HardwareTriggerConfig config;
            lock (_lock)
            {
                _hardwareTriggers.TryGetValue(triggerId, out config);
            }

            if (config == null)
            {
                _logger.LogWarning($"Unknown hardware trigger: {triggerId}");
                return;
            }

            _logger.LogInfo($"Hardware trigger fired: {triggerId}");
            OnTriggerFired(new TriggerFiredEventArgs
            {
                TriggerType = TriggerType.Hardware,
                TriggerId = triggerId,
                TriggerSource = $"IO:{config.IoPin}",
                Image = image,
                Metadata = metadata
            });
        }

        #endregion

        #region Software Triggers

        /// <summary>
        /// Register a software trigger
        /// </summary>
        public void RegisterSoftwareTrigger(string triggerId, SoftwareTriggerConfig config)
        {
            if (string.IsNullOrEmpty(triggerId))
            {
                throw new ArgumentException("Trigger ID cannot be null or empty", nameof(triggerId));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            lock (_lock)
            {
                _softwareTriggers[triggerId] = config;
                _logger.LogInfo($"Software trigger registered: {triggerId}, Name={config.TriggerName}");
            }
        }

        /// <summary>
        /// Unregister a software trigger
        /// </summary>
        public void UnregisterSoftwareTrigger(string triggerId)
        {
            lock (_lock)
            {
                if (_softwareTriggers.Remove(triggerId))
                {
                    _logger.LogInfo($"Software trigger unregistered: {triggerId}");
                }
            }
        }

        /// <summary>
        /// Fire a software trigger
        /// </summary>
        public void FireSoftwareTrigger(string triggerId, HImage image = null, object metadata = null)
        {
            SoftwareTriggerConfig config;
            lock (_lock)
            {
                _softwareTriggers.TryGetValue(triggerId, out config);
            }

            if (config == null)
            {
                _logger.LogWarning($"Unknown software trigger: {triggerId}");
                return;
            }

            _logger.LogInfo($"Software trigger fired: {triggerId} ({config.TriggerName})");
            OnTriggerFired(new TriggerFiredEventArgs
            {
                TriggerType = TriggerType.Software,
                TriggerId = triggerId,
                TriggerSource = $"Software:{config.TriggerName}",
                Image = image,
                Metadata = metadata
            });
        }

        #endregion

        #region Timer Triggers

        /// <summary>
        /// Register a timer trigger
        /// </summary>
        public void RegisterTimerTrigger(string triggerId, TimerTriggerConfig config)
        {
            if (string.IsNullOrEmpty(triggerId))
            {
                throw new ArgumentException("Trigger ID cannot be null or empty", nameof(triggerId));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            lock (_lock)
            {
                _timerTriggers[triggerId] = config;
                _logger.LogInfo($"Timer trigger registered: {triggerId}, Interval={config.IntervalMs}ms");
            }
        }

        /// <summary>
        /// Start a timer trigger
        /// </summary>
        public void StartTimerTrigger(string triggerId)
        {
            if (string.IsNullOrEmpty(triggerId))
            {
                throw new ArgumentException("Trigger ID cannot be null or empty", nameof(triggerId));
            }

            TimerTriggerConfig config;
            CancellationTokenSource existingCts;
            lock (_lock)
            {
                if (!_timerTriggers.TryGetValue(triggerId, out config))
                {
                    _logger.LogWarning($"Timer trigger not found: {triggerId}");
                    return;
                }

                if (_timerCancellations.ContainsKey(triggerId))
                {
                    _logger.LogWarning($"Timer trigger already running: {triggerId}");
                    return;
                }

                existingCts = new CancellationTokenSource();
                _timerCancellations[triggerId] = existingCts;
            }

            // Start timer loop
            Task.Run(async () =>
            {
                _logger.LogInfo($"Timer trigger started: {triggerId}");

                try
                {
                    // Apply initial delay if configured
                    if (config.InitialDelayMs > 0)
                    {
                        await Task.Delay(config.InitialDelayMs, existingCts.Token);
                    }

                    long tickCount = 0;
                    while (!existingCts.Token.IsCancellationRequested)
                    {
                        tickCount++;
                        _logger.Info($"Timer trigger tick: {triggerId}, Count={tickCount}");

                        // Trigger the workflow
                        OnTriggerFired(new TriggerFiredEventArgs
                        {
                            TriggerType = TriggerType.Timer,
                            TriggerId = triggerId,
                            TriggerSource = $"Timer:{triggerId}",
                            Metadata = new { TickCount = tickCount }
                        });

                        // Wait for the interval
                        await Task.Delay(config.IntervalMs, existingCts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInfo($"Timer trigger stopped: {triggerId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Timer trigger error: {triggerId}, Error: {ex.Message}", ex);
                }
                finally
                {
                    lock (_lock)
                    {
                        _timerCancellations.Remove(triggerId);
                        existingCts.Dispose();
                    }
                }
            }, existingCts.Token);
        }

        /// <summary>
        /// Stop a timer trigger
        /// </summary>
        public void StopTimerTrigger(string triggerId)
        {
            if (string.IsNullOrEmpty(triggerId))
            {
                throw new ArgumentException("Trigger ID cannot be null or empty", nameof(triggerId));
            }

            CancellationTokenSource cts;
            lock (_lock)
            {
                _timerCancellations.TryGetValue(triggerId, out cts);
            }

            if (cts != null)
            {
                cts.Cancel();
                _logger.LogInfo($"Timer trigger stopped: {triggerId}");
            }
        }

        /// <summary>
        /// Unregister a timer trigger
        /// </summary>
        public void UnregisterTimerTrigger(string triggerId)
        {
            StopTimerTrigger(triggerId);

            lock (_lock)
            {
                if (_timerTriggers.Remove(triggerId))
                {
                    _logger.LogInfo($"Timer trigger unregistered: {triggerId}");
                }
            }
        }

        #endregion

        #region General Trigger Management

        /// <summary>
        /// Get all registered trigger IDs
        /// </summary>
        public List<string> GetAllTriggerIds()
        {
            lock (_lock)
            {
                var allTriggers = new List<string>();
                allTriggers.AddRange(_hardwareTriggers.Keys.Select(id => $"Hardware:{id}"));
                allTriggers.AddRange(_softwareTriggers.Keys.Select(id => $"Software:{id}"));
                allTriggers.AddRange(_timerTriggers.Keys.Select(id => $"Timer:{id}"));
                return allTriggers;
            }
        }

        /// <summary>
        /// Get trigger statistics
        /// </summary>
        public TriggerManagerStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new TriggerManagerStatistics
                {
                    HardwareTriggerCount = _hardwareTriggers.Count,
                    SoftwareTriggerCount = _softwareTriggers.Count,
                    TimerTriggerCount = _timerTriggers.Count,
                    ActiveTimerTriggers = _timerCancellations.Count
                };
            }
        }

        /// <summary>
        /// Trigger fired event handler
        /// </summary>
        protected virtual void OnTriggerFired(TriggerFiredEventArgs e)
        {
            TriggerFired?.Invoke(this, e);

            // Publish to async event bus
            var triggerEvent = new WorkflowTriggerEvent(
                "TriggerManager",
                e.TriggerType,
                e.TriggerSource,
                e.Image
            )
            {
                TriggerData = e.Metadata,
                ImageMetadata = e.Metadata
            };

            _eventBus.PublishAsync(triggerEvent).ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    _logger.LogError($"Error publishing trigger event: {t.Exception.Message}", t.Exception);
                }
            });
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                // Stop all timer triggers
                var timerIds = _timerCancellations.Keys.ToList();
                foreach (var timerId in timerIds)
                {
                    StopTimerTrigger(timerId);
                }

                _logger.LogInfo("TriggerManager disposed");
            }
        }

        #endregion
    }

    /// <summary>
    /// Trigger fired event arguments
    /// </summary>
    public class TriggerFiredEventArgs : EventArgs
    {
        public TriggerType TriggerType { get; set; }
        public string TriggerId { get; set; }
        public string TriggerSource { get; set; }
        public HImage Image { get; set; }
        public object Metadata { get; set; }
    }

    /// <summary>
    /// Trigger manager statistics
    /// </summary>
    public class TriggerManagerStatistics
    {
        public int HardwareTriggerCount { get; set; }
        public int SoftwareTriggerCount { get; set; }
        public int TimerTriggerCount { get; set; }
        public int ActiveTimerTriggers { get; set; }
    }
}
