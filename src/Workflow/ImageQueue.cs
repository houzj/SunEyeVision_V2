using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Core;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// Queue overflow strategy
    /// </summary>
    public enum QueueOverflowStrategy
    {
        /// <summary>
        /// Drop newest image when queue is full
        /// </summary>
        DropNewest,

        /// <summary>
        /// Drop oldest image when queue is full
        /// </summary>
        DropOldest,

        /// <summary>
        /// Block producer until space is available
        /// </summary>
        Block,

        /// <summary>
        /// Overwrite oldest image with newest
        /// </summary>
        Overwrite
    }

    /// <summary>
    /// Image queue entry with metadata
    /// </summary>
    public class ImageQueueEntry
    {
        /// <summary>
        /// Image data
        /// </summary>
        public IVisionImage Image { get; set; }

        /// <summary>
        /// Enqueue timestamp
        /// </summary>
        public DateTime EnqueueTime { get; set; }

        /// <summary>
        /// Sequence number
        /// </summary>
        public long SequenceNumber { get; set; }

        /// <summary>
        /// Metadata
        /// </summary>
        public object Metadata { get; set; }

        /// <summary>
        /// Device ID that captured the image
        /// </summary>
        public string DeviceId { get; set; }
    }

    /// <summary>
    /// Thread-safe producer-consumer queue for images with configurable overflow strategy
    /// </summary>
    public class ImageQueue : IDisposable
    {
        private readonly ConcurrentQueue<ImageQueueEntry> _queue;
        private readonly SemaphoreSlim _semaphore;
        private readonly ILogger _logger;
        private readonly QueueOverflowStrategy _overflowStrategy;
        private readonly int _maxCapacity;
        private readonly object _lock = new object();

        private long _sequenceNumber = 0;
        private long _totalEnqueued = 0;
        private long _totalDequeued = 0;
        private long _totalDropped = 0;
        private bool _disposed = false;

        /// <summary>
        /// Maximum queue capacity
        /// </summary>
        public int MaxCapacity => _maxCapacity;

        /// <summary>
        /// Current queue size
        /// </summary>
        public int Count => _queue.Count;

        /// <summary>
        /// Is queue empty
        /// </summary>
        public bool IsEmpty => _queue.Count == 0;

        /// <summary>
        /// Is queue full
        /// </summary>
        public bool IsFull => _queue.Count >= _maxCapacity;

        /// <summary>
        /// Overflow strategy
        /// </summary>
        public QueueOverflowStrategy OverflowStrategy => _overflowStrategy;

        /// <summary>
        /// Total enqueued count
        /// </summary>
        public long TotalEnqueued => _totalEnqueued;

        /// <summary>
        /// Total dequeued count
        /// </summary>
        public long TotalDequeued => _totalDequeued;

        /// <summary>
        /// Total dropped count
        /// </summary>
        public long TotalDropped => _totalDropped;

        /// <summary>
        /// Queue status changed event
        /// </summary>
        public event EventHandler<QueueStatusChangedEventArgs> StatusChanged;

        public ImageQueue(int maxCapacity, QueueOverflowStrategy overflowStrategy, ILogger logger)
        {
            _maxCapacity = maxCapacity > 0 ? maxCapacity : 10;
            _overflowStrategy = overflowStrategy;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _queue = new ConcurrentQueue<ImageQueueEntry>();
            _semaphore = new SemaphoreSlim(0);

            _logger.LogInfo($"ImageQueue initialized: MaxCapacity={_maxCapacity}, Strategy={_overflowStrategy}");
        }

        /// <summary>
        /// Enqueue an image (producer)
        /// </summary>
        public async Task<bool> EnqueueAsync(IVisionImage image, string deviceId = null, object metadata = null)
        {
            if (_disposed)
            {
                _logger.LogWarning("Attempted to enqueue to disposed queue");
                return false;
            }

            if (image == null)
            {
                _logger.LogError("Cannot enqueue null image");
                return false;
            }

            bool enqueued = false;
            lock (_lock)
            {
                switch (_overflowStrategy)
                {
                    case QueueOverflowStrategy.Block:
                        // Will block outside lock
                        break;

                    case QueueOverflowStrategy.DropNewest:
                        if (IsFull)
                        {
                            Interlocked.Increment(ref _totalDropped);
                            _logger.LogWarning($"Queue full, dropping newest image (TotalDropped: {_totalDropped})");
                            NotifyStatusChanged();
                            return false;
                        }
                        break;

                    case QueueOverflowStrategy.DropOldest:
                        if (IsFull && _queue.TryDequeue(out _))
                        {
                            Interlocked.Increment(ref _totalDropped);
                            _logger.LogInfo($"Queue full, dropped oldest image (TotalDropped: {_totalDropped})");
                        }
                        break;

                    case QueueOverflowStrategy.Overwrite:
                        if (IsFull)
                        {
                            // Dequeue the oldest and continue (will overwrite below)
                            _queue.TryDequeue(out _);
                            Interlocked.Increment(ref _totalDropped);
                            _logger.LogInfo($"Queue full, overwriting oldest image (TotalDropped: {_totalDropped})");
                        }
                        break;
                }

                // Create queue entry
                var entry = new ImageQueueEntry
                {
                    Image = image,
                    EnqueueTime = DateTime.Now,
                    SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
                    Metadata = metadata,
                    DeviceId = deviceId
                };

                _queue.Enqueue(entry);
                Interlocked.Increment(ref _totalEnqueued);
                _semaphore.Release();
                enqueued = true;

                _logger.Info($"Image enqueued: Seq={entry.SequenceNumber}, QueueSize={_queue.Count}, TotalEnqueued={_totalEnqueued}");
            }

            NotifyStatusChanged();

            // Handle blocking strategy outside lock
            if (_overflowStrategy == QueueOverflowStrategy.Block && IsFull)
            {
                await _semaphore.WaitAsync(); // Wait for a slot to be available
                // Re-enqueue the image now that there's space
                var entry = new ImageQueueEntry
                {
                    Image = image,
                    EnqueueTime = DateTime.Now,
                    SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
                    Metadata = metadata,
                    DeviceId = deviceId
                };

                _queue.Enqueue(entry);
                Interlocked.Increment(ref _totalEnqueued);
                enqueued = true;

                NotifyStatusChanged();
            }

            return enqueued;
        }

        /// <summary>
        /// Dequeue an image (consumer)
        /// </summary>
        public async Task<ImageQueueEntry> DequeueAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                _logger.LogWarning("Attempted to dequeue from disposed queue");
                return null;
            }

            await _semaphore.WaitAsync(cancellationToken);

            ImageQueueEntry entry = null;
            if (_queue.TryDequeue(out entry))
            {
                Interlocked.Increment(ref _totalDequeued);
                var waitTime = (DateTime.Now - entry.EnqueueTime).TotalMilliseconds;
                _logger.Info($"Image dequeued: Seq={entry.SequenceNumber}, WaitTime={waitTime:F2}ms, QueueSize={_queue.Count}, TotalDequeued={_totalDequeued}");
                NotifyStatusChanged();
            }

            return entry;
        }

        /// <summary>
        /// Try to dequeue without blocking
        /// </summary>
        public bool TryDequeue(out ImageQueueEntry entry)
        {
            if (_disposed)
            {
                entry = null;
                return false;
            }

            bool dequeued = _queue.TryDequeue(out entry);
            if (dequeued)
            {
                Interlocked.Increment(ref _totalDequeued);
                var waitTime = (DateTime.Now - entry.EnqueueTime).TotalMilliseconds;
                _logger.Info($"Image dequeued (non-blocking): Seq={entry.SequenceNumber}, WaitTime={waitTime:F2}ms, QueueSize={_queue.Count}, TotalDequeued={_totalDequeued}");
                NotifyStatusChanged();
            }

            return dequeued;
        }

        /// <summary>
        /// Peek at the next image without removing it
        /// </summary>
        public bool TryPeek(out ImageQueueEntry entry)
        {
            return _queue.TryPeek(out entry);
        }

        /// <summary>
        /// Clear all images from the queue
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                var count = _queue.Count;
                while (_queue.TryDequeue(out _))
                {
                    Interlocked.Increment(ref _totalDropped);
                }
                _logger.LogInfo($"Queue cleared: {count} images removed, TotalDropped={_totalDropped}");
                NotifyStatusChanged();
            }
        }

        /// <summary>
        /// Get queue statistics
        /// </summary>
        public QueueStatistics GetStatistics()
        {
            return new QueueStatistics
            {
                MaxCapacity = _maxCapacity,
                CurrentSize = _queue.Count,
                TotalEnqueued = _totalEnqueued,
                TotalDequeued = _totalDequeued,
                TotalDropped = _totalDropped,
                OverflowStrategy = _overflowStrategy,
                DropRate = _totalEnqueued > 0 ? (double)_totalDropped / _totalEnqueued * 100 : 0
            };
        }

        /// <summary>
        /// Notify subscribers of status change
        /// </summary>
        private void NotifyStatusChanged()
        {
            StatusChanged?.Invoke(this, new QueueStatusChangedEventArgs
            {
                CurrentSize = _queue.Count,
                MaxCapacity = _maxCapacity,
                IsFull = IsFull,
                IsEmpty = IsEmpty,
                TotalEnqueued = _totalEnqueued,
                TotalDequeued = _totalDequeued,
                TotalDropped = _totalDropped
            });
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                // Clear queue and release all waiting threads
                Clear();

                // Release all semaphore waiters
                _semaphore.Release(int.MaxValue);
                _semaphore.Dispose();

                _logger.LogInfo("ImageQueue disposed");
            }
        }
    }

    /// <summary>
    /// Queue status changed event arguments
    /// </summary>
    public class QueueStatusChangedEventArgs : EventArgs
    {
        public int CurrentSize { get; set; }
        public int MaxCapacity { get; set; }
        public bool IsFull { get; set; }
        public bool IsEmpty { get; set; }
        public long TotalEnqueued { get; set; }
        public long TotalDequeued { get; set; }
        public long TotalDropped { get; set; }
    }

    /// <summary>
    /// Queue statistics
    /// </summary>
    public class QueueStatistics
    {
        public int MaxCapacity { get; set; }
        public int CurrentSize { get; set; }
        public long TotalEnqueued { get; set; }
        public long TotalDequeued { get; set; }
        public long TotalDropped { get; set; }
        public QueueOverflowStrategy OverflowStrategy { get; set; }
        public double DropRate { get; set; }
    }
}
