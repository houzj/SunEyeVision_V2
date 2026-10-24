using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Execution.Results
{
    public class ExecutionResult : IDisposable
    {
        private readonly object _declaredGate = new();
        private bool _disposed;
        private bool _declaredCaptured;
        private IReadOnlyDictionary<SunEyeVision.Plugin.Contracts.RunIdentity,DeclaredOutputSnapshot> _declaredOutputs
            = new Dictionary<SunEyeVision.Plugin.Contracts.RunIdentity,DeclaredOutputSnapshot>();
        public IReadOnlyList<DeclaredOutputSnapshot> DeclaredOutputs
        {get{lock(_declaredGate){ObjectDisposedException.ThrowIf(_disposed,this);return Array.AsReadOnly(_declaredOutputs.Values.ToArray());}}}
        public void CaptureDeclaredOutputs(IEnumerable<DeclaredOutputSnapshot> outputs)
        {
            var frozen=outputs.ToDictionary(output=>output.Identity);
            lock(_declaredGate)
            {
                ObjectDisposedException.ThrowIf(_disposed,this);
                if(_declaredCaptured)throw new InvalidOperationException("Declared run outputs were already captured.");
                _declaredOutputs=new System.Collections.ObjectModel.ReadOnlyDictionary<SunEyeVision.Plugin.Contracts.RunIdentity,DeclaredOutputSnapshot>(frozen);
                _declaredCaptured=true;
            }
        }
        public DeclaredOutputReader AcquireDeclaredOutput(SunEyeVision.Plugin.Contracts.RunIdentity identity)
        {
            lock(_declaredGate)
            {
                ObjectDisposedException.ThrowIf(_disposed,this);
                return _declaredOutputs.TryGetValue(identity,out var output)?output.AcquireReader():throw new KeyNotFoundException("This exact invocation is not in the selected run.");
            }
        }
        public bool Success { get; set; }
        public ExecutionStatus Status { get; set; } = ExecutionStatus.NotExecuted;
        public Dictionary<string, object> Outputs { get; set; }
        public List<string> Errors { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public bool IsStopped { get; set; }
        private IReadOnlyDictionary<string, NodeExecutionResult> _nodeResults = new Dictionary<string, NodeExecutionResult>();
        private NodeResultSnapshot? _resultSnapshot;
        public IReadOnlyDictionary<string, NodeExecutionResult> NodeResults
        {
            get => _nodeResults;
            set
            {
                var snapshot = new NodeResultSnapshot(value);
                NodeResultSnapshot? previous;
                bool rejected;
                lock(_declaredGate)
                {
                    rejected=_disposed||_declaredCaptured;
                    previous=rejected?null:_resultSnapshot;
                    if(!rejected){_resultSnapshot = snapshot;_nodeResults = snapshot.Results;}
                }
                if(rejected){snapshot.Dispose();throw new InvalidOperationException("The run output has already closed or frozen.");}
                previous?.Dispose();
            }
        }
        public void SetNodeResult(string nodeId, NodeExecutionResult result)
        {
            var values = new Dictionary<string, NodeExecutionResult>(_nodeResults) { [nodeId] = result };
            NodeResults = values;
        }
        public void Dispose()
        {
            NodeResultSnapshot? snapshot;
            lock(_declaredGate)
            {
                if(_disposed)return;
                _disposed=true;
                snapshot = _resultSnapshot;
                _resultSnapshot = null;
                _nodeResults = new Dictionary<string, NodeExecutionResult>();
                _declaredOutputs=new Dictionary<SunEyeVision.Plugin.Contracts.RunIdentity,DeclaredOutputSnapshot>();
            }
            snapshot?.Dispose();
        }

        public ExecutionResult()
        {
            Outputs = new Dictionary<string, object>();
            Errors = new List<string>();
            NodeResults = new Dictionary<string, NodeExecutionResult>();
        }

        public static ExecutionResult CreateSuccess()
            => new ExecutionResult { Success = true, Status = ExecutionStatus.Success };
        public static ExecutionResult CreateFailure(string errorMessage)
            => new ExecutionResult
            {
                Success = false,
                Status = ExecutionStatus.Failed,
                Errors = new List<string> { errorMessage }
            };

        public static ExecutionResult CreateCancelled(string message = "工作流执行被取消")
            => new ExecutionResult
            {
                Success = false,
                Status = ExecutionStatus.Cancelled,
                IsStopped = true,
                CompletionMessage = message
            };

        public string? CompletionMessage { get; set; }

        public void AddError(string message, string nodeId = null)
        {
            Success = false;
            if (Status != ExecutionStatus.Cancelled)
                Status = ExecutionStatus.Failed;
            Errors.Add(message);
        }

        public void Merge(ExecutionResult other)
        {
            if (other == null) return;
            Success = Success && other.Success;
            Status = Status == ExecutionStatus.Cancelled || other.Status == ExecutionStatus.Cancelled
                ? ExecutionStatus.Cancelled
                : Success ? ExecutionStatus.Success : ExecutionStatus.Failed;
            IsStopped = IsStopped || other.IsStopped;
            CompletionMessage ??= other.CompletionMessage;
            ExecutionTime = ExecutionTime.Add(other.ExecutionTime);
            foreach (var kvp in other.Outputs) Outputs[kvp.Key] = kvp.Value;
            foreach (var e in other.Errors) Errors.Add(e);
            foreach (var nr in other.NodeResults) SetNodeResult(nr.Key, nr.Value);
        }
    }

    public class NodeExecutionResult
    {
        public string NodeId { get; set; }
        public bool Success { get; set; }
        public ExecutionStatus Status { get; set; } = ExecutionStatus.NotExecuted;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
        public List<string> ErrorMessages { get; set; }
        public IReadOnlyList<ResultItem>? ResultItems { get; set; }
        public ToolResults? Result { get; set; }

        public NodeExecutionResult() { ErrorMessages = new List<string>(); }
    }
}
