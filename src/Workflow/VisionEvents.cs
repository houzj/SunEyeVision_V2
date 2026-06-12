using System;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// Image captured event - raised when an image is captured from a camera
    /// </summary>
    public class ImageCapturedEvent : VisionEventBase
    {
        /// <summary>
        /// Device ID that captured the image
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// Captured image
        /// </summary>
        public IVisionImage Image { get; set; }

        /// <summary>
        /// Image timestamp
        /// </summary>
        public DateTime ImageTimestamp { get; set; }

        /// <summary>
        /// Frame number (for continuous capture)
        /// </summary>
        public long FrameNumber { get; set; }

        /// <summary>
        /// Image metadata
        /// </summary>
        public object Metadata { get; set; }

        public ImageCapturedEvent(string source, string deviceId, IVisionImage image, long frameNumber = 0)
            : base(source)
        {
            DeviceId = deviceId;
            Image = image;
            ImageTimestamp = DateTime.Now;
            FrameNumber = frameNumber;
        }
    }

    /// <summary>
    /// Workflow trigger event - raised when a workflow should be triggered
    /// </summary>
    public class WorkflowTriggerEvent : VisionEventBase
    {
        /// <summary>
        /// Trigger type (Hardware, Software, Timer)
        /// </summary>
        public TriggerType TriggerType { get; set; }

        /// <summary>
        /// Trigger source (device ID or other identifier)
        /// </summary>
        public string TriggerSource { get; set; }

        /// <summary>
        /// Trigger data (optional payload)
        /// </summary>
        public object TriggerData { get; set; }

        /// <summary>
        /// Image to process (if provided)
        /// </summary>
        public IVisionImage Image { get; set; }

        /// <summary>
        /// Image metadata
        /// </summary>
        public object ImageMetadata { get; set; }

        public WorkflowTriggerEvent(string source, TriggerType triggerType, string triggerSource, IVisionImage image = null)
            : base(source)
        {
            TriggerType = triggerType;
            TriggerSource = triggerSource;
            Image = image;
        }
    }

    /// <summary>
    /// Workflow execution started event (async)
    /// </summary>
    public class WorkflowExecutionStartedEvent : VisionEventBase
    {
        /// <summary>
        /// Workflow ID
        /// </summary>
        public string WorkflowId { get; set; }

        /// <summary>
        /// Workflow name
        /// </summary>
        public string WorkflowName { get; set; }

        /// <summary>
        /// Execution ID
        /// </summary>
        public string ExecutionId { get; set; }

        /// <summary>
        /// Execution mode (Sync, Async)
        /// </summary>
        public ExecutionMode ExecutionMode { get; set; }

        public WorkflowExecutionStartedEvent(string source, string workflowId, string workflowName, string executionId, ExecutionMode mode)
            : base(source)
        {
            WorkflowId = workflowId;
            WorkflowName = workflowName;
            ExecutionId = executionId;
            ExecutionMode = mode;
        }
    }

    /// <summary>
    /// Workflow execution completed event (async)
    /// </summary>
    public class WorkflowExecutionCompletedEvent : VisionEventBase
    {
        /// <summary>
        /// Workflow ID
        /// </summary>
        public string WorkflowId { get; set; }

        /// <summary>
        /// Workflow name
        /// </summary>
        public string WorkflowName { get; set; }

        /// <summary>
        /// Execution ID
        /// </summary>
        public string ExecutionId { get; set; }

        /// <summary>
        /// Execution result
        /// </summary>
        public ExecutionResult Result { get; set; }

        public WorkflowExecutionCompletedEvent(string source, string workflowId, string workflowName, string executionId, ExecutionResult result)
            : base(source)
        {
            WorkflowId = workflowId;
            WorkflowName = workflowName;
            ExecutionId = executionId;
            Result = result;
        }
    }

    /// <summary>
    /// Processing completed event - raised when image processing is complete
    /// </summary>
    public class ProcessingCompletedEvent : VisionEventBase
    {
        /// <summary>
        /// Workflow ID
        /// </summary>
        public string WorkflowId { get; set; }

        /// <summary>
        /// Execution ID
        /// </summary>
        public string ExecutionId { get; set; }

        /// <summary>
        /// Input image
        /// </summary>
        public IVisionImage InputImage { get; set; }

        /// <summary>
        /// Processing result
        /// </summary>
        public ExecutionResult Result { get; set; }

        /// <summary>
        /// Processing duration in milliseconds
        /// </summary>
        public long ProcessingDurationMs { get; set; }

        public ProcessingCompletedEvent(string source, string workflowId, string executionId, IVisionImage inputImage, ExecutionResult result, long durationMs)
            : base(source)
        {
            WorkflowId = workflowId;
            ExecutionId = executionId;
            InputImage = inputImage;
            Result = result;
            ProcessingDurationMs = durationMs;
        }
    }

    /// <summary>
    /// Queue status changed event - raised when image queue status changes
    /// </summary>
    public class QueueStatusChangedEvent : VisionEventBase
    {
        /// <summary>
        /// Current queue size
        /// </summary>
        public int CurrentSize { get; set; }

        /// <summary>
        /// Maximum queue capacity
        /// </summary>
        public int MaxCapacity { get; set; }

        /// <summary>
        /// Queue full flag
        /// </summary>
        public bool IsFull { get; set; }

        /// <summary>
        /// Queue empty flag
        /// </summary>
        public bool IsEmpty { get; set; }

        /// <summary>
        /// Total images enqueued
        /// </summary>
        public long TotalEnqueued { get; set; }

        /// <summary>
        /// Total images dequeued
        /// </summary>
        public long TotalDequeued { get; set; }

        /// <summary>
        /// Total images dropped
        /// </summary>
        public long TotalDropped { get; set; }

        public QueueStatusChangedEvent(string source, int currentSize, int maxCapacity, long totalEnqueued, long totalDequeued, long totalDropped)
            : base(source)
        {
            CurrentSize = currentSize;
            MaxCapacity = maxCapacity;
            IsFull = currentSize >= maxCapacity;
            IsEmpty = currentSize == 0;
            TotalEnqueued = totalEnqueued;
            TotalDequeued = totalDequeued;
            TotalDropped = totalDropped;
        }
    }

    /// <summary>
    /// Trigger type enumeration
    /// </summary>
    public enum TriggerType
    {
        /// <summary>
        /// Hardware trigger (e.g., GPIO, sensor)
        /// </summary>
        Hardware,

        /// <summary>
        /// Software trigger (e.g., manual trigger, API call)
        /// </summary>
        Software,

        /// <summary>
        /// Timer trigger (e.g., periodic interval)
        /// </summary>
        Timer,

        /// <summary>
        /// Queue trigger (triggered by image availability in queue)
        /// </summary>
        Queue
    }

    /// <summary>
    /// Execution mode enumeration
    /// </summary>
    public enum ExecutionMode
    {
        /// <summary>
        /// Synchronous execution - process one image at a time
        /// </summary>
        Sync,

        /// <summary>
        /// Asynchronous execution - process images concurrently
        /// </summary>
        Async
    }

    /// <summary>
    /// Base class for vision events
    /// </summary>
    public abstract class VisionEventBase : Core.Events.EventBase
    {
        protected VisionEventBase(string source) : base(source)
        {
        }
    }
}
