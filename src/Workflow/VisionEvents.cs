using System;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;

namespace SunEyeVision.Workflow
{
    public class ImageCapturedEvent : VisionEventBase
    {
        public string DeviceId { get; set; }
        public HImage Image { get; set; }
        public DateTime ImageTimestamp { get; set; }
        public long FrameNumber { get; set; }
        public object Metadata { get; set; }

        public ImageCapturedEvent(string source, string deviceId, HImage image, long frameNumber = 0)
            : base(source)
        {
            DeviceId = deviceId;
            Image = image;
            ImageTimestamp = DateTime.Now;
            FrameNumber = frameNumber;
        }
    }

    public class WorkflowTriggerEvent : VisionEventBase
    {
        public TriggerType TriggerType { get; set; }
        public string TriggerSource { get; set; }
        public object TriggerData { get; set; }
        public HImage Image { get; set; }
        public object ImageMetadata { get; set; }

        public WorkflowTriggerEvent(string source, TriggerType triggerType, string triggerSource, HImage image = null)
            : base(source)
        {
            TriggerType = triggerType;
            TriggerSource = triggerSource;
            Image = image;
        }
    }

    public class WorkflowExecutionStartedEvent : VisionEventBase
    {
        public string WorkflowId { get; set; }
        public string WorkflowName { get; set; }
        public string ExecutionId { get; set; }
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

    public class WorkflowExecutionCompletedEvent : VisionEventBase
    {
        public string WorkflowId { get; set; }
        public string WorkflowName { get; set; }
        public string ExecutionId { get; set; }
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

    public class ProcessingCompletedEvent : VisionEventBase
    {
        public string WorkflowId { get; set; }
        public string ExecutionId { get; set; }
        public HImage InputImage { get; set; }
        public ExecutionResult Result { get; set; }
        public long ProcessingDurationMs { get; set; }

        public ProcessingCompletedEvent(string source, string workflowId, string executionId, HImage inputImage, ExecutionResult result, long durationMs)
            : base(source)
        {
            WorkflowId = workflowId;
            ExecutionId = executionId;
            InputImage = inputImage;
            Result = result;
            ProcessingDurationMs = durationMs;
        }
    }

    public class QueueStatusChangedEvent : VisionEventBase
    {
        public int CurrentSize { get; set; }
        public int MaxCapacity { get; set; }
        public bool IsFull { get; set; }
        public bool IsEmpty { get; set; }
        public long TotalEnqueued { get; set; }
        public long TotalDequeued { get; set; }
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

    public enum TriggerType
    {
        Hardware,
        Software,
        Timer,
        Queue
    }

    public enum ExecutionMode
    {
        Sync,
        Async
    }

    public abstract class VisionEventBase : Core.Events.EventBase
    {
        protected VisionEventBase(string source) : base(source) { }
    }
}
