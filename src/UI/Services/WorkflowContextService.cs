using System;
using System.Collections.Generic;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services
{
    /// <summary>
    /// 工作流上下文服务默认实现 - 基于字典的懒加载缓存。
    /// </summary>
    public class WorkflowContextService : IWorkflowContextService
    {
        private readonly Dictionary<string, WorkflowContext> _contexts = new();

        public WorkflowContext GetOrCreate(string workflowId, string workflowName)
        {
            if (_contexts.TryGetValue(workflowId, out var ctx))
                return ctx;

            ctx = new WorkflowContext
            {
                WorkflowId = workflowId,
                WorkflowName = workflowName,
                ExecutionStartTime = DateTime.Now
            };
            _contexts[workflowId] = ctx;
            return ctx;
        }

        public WorkflowContext? Get(string workflowId)
        {
            return _contexts.TryGetValue(workflowId, out var ctx) ? ctx : null;
        }

        public void Remove(string workflowId)
        {
            if (_contexts.Remove(workflowId, out var context)) context.ClearNodeResults();
        }

        public void Clear()
        {
            var contexts = new List<WorkflowContext>(_contexts.Values);
            _contexts.Clear();
            var errors = new List<Exception>();
            foreach (var context in contexts)
                try { context.ClearNodeResults(); } catch (Exception ex) { errors.Add(ex); }
            if (errors.Count != 0) throw new AggregateException(errors);
        }
    }
}
