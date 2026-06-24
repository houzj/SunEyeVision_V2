using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;
using SunEyeVision.Plugin.SDK;
using SunEyeVision.Plugin.SDK.Execution.Results;
using Events = SunEyeVision.Core.Events;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// Event-driven asynchronous workflow executor
    /// Responds to triggers and processes images from the queue
    /// </summary>
    public class EventDrivenExecutor : IDisposable
    {
        private readonly WorkflowExecutionEngine _syncEngine;
        private readonly ImageQueue _imageQueue;
        private readonly ILogger _logger;
        private readonly Events.IAsyncEventBus _eventBus;
        private readonly IPluginManager _pluginManager;

        private CancellationTokenSource _cancellationTokenSource;
        private Task _processingTask;
        private bool _isRunning = false;
        private bool _disposed = false;

        private string _currentWorkflowId;
        private long _totalExecutions = 0;
        private long _successfulExecutions = 0;
        private long _failedExecutions = 0;
        private DateTime _lastExecutionTime;

        /// <summary>
        /// Is executor running
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Current workflow ID
        /// </summary>
        public string CurrentWorkflowId => _currentWorkflowId;

        /// <summary>
        /// Total executions
        /// </summary>
        public long TotalExecutions => _totalExecutions;

        /// <summary>
        /// Successful executions
        /// </summary>
        public long SuccessfulExecutions => _successfulExecutions;

        /// <summary>
        /// Failed executions
        /// </summary>
        public long FailedExecutions => _failedExecutions;

        /// <summary>
        /// Success rate
        /// </summary>
        public double SuccessRate => _totalExecutions > 0 ? (double)_successfulExecutions / _totalExecutions * 100 : 0;

        /// <summary>
        /// Processing completed event
        /// </summary>
        public event EventHandler<ProcessingCompletedEventArgs> ProcessingCompleted;

        /// <summary>
        /// Executor status changed event
        /// </summary>
        public event EventHandler<ExecutorStatusChangedEventArgs> StatusChanged;

        public EventDrivenExecutor(
            WorkflowExecutionEngine syncEngine,
            ImageQueue imageQueue,
            ILogger logger,
            Events.IAsyncEventBus eventBus,
            IPluginManager pluginManager)
        {
            _syncEngine = syncEngine ?? throw new ArgumentNullException(nameof(syncEngine));
            _imageQueue = imageQueue ?? throw new ArgumentNullException(nameof(imageQueue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _pluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
            _cancellationTokenSource = new CancellationTokenSource();

            _logger.LogInfo("EventDrivenExecutor initialized");
        }

        /// <summary>
        /// Start the executor
        /// </summary>
        public void Start(string workflowId)
        {
            if (_isRunning)
            {
                _logger.LogWarning("Executor is already running");
                return;
            }

            if (string.IsNullOrEmpty(workflowId))
            {
                throw new ArgumentException("Workflow ID cannot be null or empty", nameof(workflowId));
            }

            _currentWorkflowId = workflowId;
            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            // Start processing loop
            _processingTask = Task.Run(() => ProcessingLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

            // Subscribe to workflow trigger events
            _eventBus.Subscribe<WorkflowTriggerEvent>(OnWorkflowTrigger);

            _logger.LogInfo($"EventDrivenExecutor started for workflow: {workflowId}");
            StatusChanged?.Invoke(this, new ExecutorStatusChangedEventArgs
            {
                IsRunning = true,
                WorkflowId = workflowId
            });
        }

        /// <summary>
        /// Stop the executor
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            _cancellationTokenSource.Cancel();

            try
            {
                _processingTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex)
            {
                _logger.LogError($"Error stopping executor: {ex.Message}", ex);
            }

            // Unsubscribe from events
            _eventBus.Unsubscribe<WorkflowTriggerEvent>(OnWorkflowTrigger);

            _logger.LogInfo("EventDrivenExecutor stopped");
            StatusChanged?.Invoke(this, new ExecutorStatusChangedEventArgs
            {
                IsRunning = false,
                WorkflowId = _currentWorkflowId
            });
        }

        /// <summary>
        /// Processing loop - continuously dequeues and processes images
        /// </summary>
        private async Task ProcessingLoop(CancellationToken cancellationToken)
        {
            _logger.LogInfo("Processing loop started");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Wait for an image from the queue
                        var queueEntry = await _imageQueue.DequeueAsync(cancellationToken);

                        if (queueEntry == null)
                        {
                            continue;
                        }

                        // Process the image
                        await ProcessImageAsync(queueEntry);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInfo("Processing loop cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error in processing loop: {ex.Message}", ex);
                        Interlocked.Increment(ref _failedExecutions);

                        // Continue processing even after errors
                        await Task.Delay(100, cancellationToken);
                    }
                }
            }
            finally
            {
                _logger.LogInfo("Processing loop stopped");
            }
        }

        /// <summary>
        /// Process an image from the queue
        /// </summary>
        private async Task ProcessImageAsync(ImageQueueEntry queueEntry)
        {
            var executionId = Guid.NewGuid().ToString();
            var startTime = DateTime.Now;

            _logger.LogInfo($"Processing image: Seq={queueEntry.SequenceNumber}, Device={queueEntry.DeviceId}");

            Interlocked.Increment(ref _totalExecutions);

            try
            {
                // Publish execution started event
                var startedEvent = new WorkflowExecutionStartedEvent(
                    "EventDrivenExecutor",
                    _currentWorkflowId,
                    _currentWorkflowId,
                    executionId,
                    ExecutionMode.Async
                );
                await _eventBus.PublishAndWaitAsync(startedEvent);

                // Execute workflow
                var result = await _syncEngine.ExecuteWorkflow(_currentWorkflowId, queueEntry.Image);

                var duration = (long)(DateTime.Now - startTime).TotalMilliseconds;

                if (result.Success)
                {
                    Interlocked.Increment(ref _successfulExecutions);
                    _logger.LogInfo($"Processing completed successfully: Seq={queueEntry.SequenceNumber}, Duration={duration}ms");
                }
                else
                {
                    Interlocked.Increment(ref _failedExecutions);
                    _logger.LogError($"Processing failed: Seq={queueEntry.SequenceNumber}, Error={result.Errors.FirstOrDefault()}");
                }

                _lastExecutionTime = DateTime.Now;

                // Publish processing completed event
                var completedEvent = new ProcessingCompletedEvent(
                    "EventDrivenExecutor",
                    _currentWorkflowId,
                    executionId,
                    queueEntry.Image,
                    result,
                    duration
                );
                await _eventBus.PublishAndWaitAsync(completedEvent);

                // Publish workflow execution completed event
                var workflowCompletedEvent = new WorkflowExecutionCompletedEvent(
                    "EventDrivenExecutor",
                    _currentWorkflowId,
                    _currentWorkflowId,
                    executionId,
                    result
                );
                await _eventBus.PublishAndWaitAsync(workflowCompletedEvent);

                // Notify subscribers
                ProcessingCompleted?.Invoke(this, new ProcessingCompletedEventArgs
                {
                    WorkflowId = _currentWorkflowId,
                    ExecutionId = executionId,
                    InputImage = queueEntry.Image,
                    Result = result,
                    DurationMs = duration,
                    QueueEntry = queueEntry
                });
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failedExecutions);
                _logger.LogError($"Error processing image: {ex.Message}", ex);

                var duration = (long)(DateTime.Now - startTime).TotalMilliseconds;

                // Notify subscribers of failure
                ProcessingCompleted?.Invoke(this, new ProcessingCompletedEventArgs
                {
                    WorkflowId = _currentWorkflowId,
                    ExecutionId = executionId,
                    InputImage = queueEntry.Image,
                    Result = ExecutionResult.CreateFailure(ex.Message),
                    DurationMs = duration,
                    QueueEntry = queueEntry
                });
            }
        }

        /// <summary>
        /// Handle workflow trigger event
        /// </summary>
        private async Task OnWorkflowTrigger(WorkflowTriggerEvent triggerEvent)
        {
            _logger.LogInfo($"Workflow trigger received: Type={triggerEvent.TriggerType}, Source={triggerEvent.TriggerSource}");

            try
            {
                // If the trigger includes an image, enqueue it directly
                if (triggerEvent.Image != null)
                {
                    await _imageQueue.EnqueueAsync(
                        triggerEvent.Image,
                        triggerEvent.TriggerSource,
                        triggerEvent.ImageMetadata
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error handling workflow trigger: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get executor statistics
        /// </summary>
        public ExecutorStatistics GetStatistics()
        {
            return new ExecutorStatistics
            {
                IsRunning = _isRunning,
                WorkflowId = _currentWorkflowId,
                TotalExecutions = _totalExecutions,
                SuccessfulExecutions = _successfulExecutions,
                FailedExecutions = _failedExecutions,
                SuccessRate = SuccessRate,
                LastExecutionTime = _lastExecutionTime
            };
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Stop();
                _cancellationTokenSource?.Dispose();
                _logger.LogInfo("EventDrivenExecutor disposed");
            }
        }
    }

    /// <summary>
    /// Processing completed event arguments
    /// </summary>
    public class ProcessingCompletedEventArgs : EventArgs
    {
        public string WorkflowId { get; set; }
        public string ExecutionId { get; set; }
        public IVisionImage InputImage { get; set; }
        public ExecutionResult Result { get; set; }
        public long DurationMs { get; set; }
        public ImageQueueEntry QueueEntry { get; set; }
    }

    /// <summary>
    /// Executor status changed event arguments
    /// </summary>
    public class ExecutorStatusChangedEventArgs : EventArgs
    {
        public bool IsRunning { get; set; }
        public string WorkflowId { get; set; }
    }

    /// <summary>
    /// Executor statistics
    /// </summary>
    public class ExecutorStatistics
    {
        public bool IsRunning { get; set; }
        public string WorkflowId { get; set; }
        public long TotalExecutions { get; set; }
        public long SuccessfulExecutions { get; set; }
        public long FailedExecutions { get; set; }
        public double SuccessRate { get; set; }
        public DateTime LastExecutionTime { get; set; }
    }
}
