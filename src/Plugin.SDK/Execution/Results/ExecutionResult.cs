using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Execution.Results
{
    public class ExecutionResult
    {
        public bool Success { get; set; }
        public Dictionary<string, object> Outputs { get; set; }
        public List<string> Errors { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public bool IsStopped { get; set; }
        public Dictionary<string, NodeExecutionResult> NodeResults { get; set; }

        public ExecutionResult()
        {
            Outputs = new Dictionary<string, object>();
            Errors = new List<string>();
            NodeResults = new Dictionary<string, NodeExecutionResult>();
        }

        public static ExecutionResult CreateSuccess() => new ExecutionResult { Success = true };
        public static ExecutionResult CreateFailure(string errorMessage)
            => new ExecutionResult { Success = false, Errors = new List<string> { errorMessage } };

        public void AddError(string message, string nodeId = null) { Success = false; Errors.Add(message); }

        public void Merge(ExecutionResult other)
        {
            if (other == null) return;
            Success = Success && other.Success;
            IsStopped = IsStopped || other.IsStopped;
            ExecutionTime = ExecutionTime.Add(other.ExecutionTime);
            foreach (var kvp in other.Outputs) Outputs[kvp.Key] = kvp.Value;
            foreach (var e in other.Errors) Errors.Add(e);
            foreach (var nr in other.NodeResults) NodeResults[nr.Key] = nr.Value;
        }
    }

    public class NodeExecutionResult
    {
        public string NodeId { get; set; }
        public bool Success { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
        public List<string> ErrorMessages { get; set; }
        public IReadOnlyList<ResultItem>? ResultItems { get; set; }
        public ToolResults? Result { get; set; }

        public NodeExecutionResult() { ErrorMessages = new List<string>(); }
    }
}
