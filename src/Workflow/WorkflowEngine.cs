using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenCvSharp;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Logging;

using SunEyeVision.Core.Services.Serialization;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// Workflow Engine
    /// </summary>
    public class WorkflowEngine
    {
        /// <summary>
        /// Workflow list
        /// </summary>
        private Dictionary<string, Workflow> Workflows { get; set; }

        /// <summary>
        /// Current active workflow
        /// </summary>
        public Workflow CurrentWorkflow { get; private set; }

        /// <summary>
        /// Logger
        /// </summary>
        private ILogger Logger { get; set; }

        public WorkflowEngine(ILogger logger)
        {
            Logger = logger;
            Workflows = new Dictionary<string, Workflow>();
        }

        /// <summary>
        /// Create new workflow
        /// </summary>
        public Workflow CreateWorkflow(string id, string name, string description = "")
        {
            if (Workflows.ContainsKey(id))
            {
                throw new ArgumentException($"Workflow ID {id} already exists");
            }

            var workflow = new Workflow
            {
                Id = id,
                Name = name
            };

            Workflows[id] = workflow;
            Logger.LogInfo($"Created workflow {name} (ID: {id})");

            return workflow;
        }

        /// <summary>
        /// Delete workflow
        /// </summary>
        public bool DeleteWorkflow(string id)
        {
            if (Workflows.TryGetValue(id, out var workflow))
            {
                Workflows.Remove(id);
                
                if (CurrentWorkflow?.Id == id)
                {
                    CurrentWorkflow = null;
                }

                Logger.LogInfo($"Deleted workflow {workflow.Name} (ID: {id})");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Register workflow (用于外部加载的工作流同步注册)
        /// </summary>
        public void RegisterWorkflow(Workflow workflow)
        {
            if (workflow == null)
            {
                throw new ArgumentNullException(nameof(workflow));
            }

            if (string.IsNullOrEmpty(workflow.Id))
            {
                throw new ArgumentException("Workflow ID cannot be null or empty");
            }

            if (Workflows.ContainsKey(workflow.Id))
            {
                // 工作流已存在，更新它
                Workflows[workflow.Id] = workflow;
                Logger.LogInfo($"Updated workflow {workflow.Name} (ID: {workflow.Id})");
            }
            else
            {
                // 添加新工作流
                Workflows[workflow.Id] = workflow;
                Logger.LogInfo($"Registered workflow {workflow.Name} (ID: {workflow.Id})");
            }
        }

        /// <summary>
        /// Clear all workflows (用于重置引擎状态)
        /// </summary>
        public void ClearWorkflows()
        {
            Workflows.Clear();
            CurrentWorkflow = null;
            Logger.LogInfo("All workflows cleared from engine");
        }

        /// <summary>
        /// Set current workflow
        /// </summary>
        public void SetCurrentWorkflow(string id)
        {
            if (!Workflows.ContainsKey(id))
            {
                throw new ArgumentException($"Workflow ID {id} does not exist");
            }

            CurrentWorkflow = Workflows[id];
            Logger.LogInfo($"Set current workflow: {CurrentWorkflow.Name}");
        }

        /// <summary>
        /// Get workflow
        /// </summary>
        public Workflow GetWorkflow(string id)
        {
            Workflows.TryGetValue(id, out var workflow);
            return workflow;
        }

        /// <summary>
        /// Get workflow by ID (alias for GetWorkflow)
        /// </summary>
        public Workflow? GetWorkflowById(string workflowId)
        {
            Workflows.TryGetValue(workflowId, out var workflow);
            return workflow;
        }

        /// <summary>
        /// Get all workflows
        /// </summary>
        public List<Workflow> GetAllWorkflows()
        {
            return Workflows.Values.ToList();
        }

        /// <summary>
        /// Execute workflow
        /// </summary>
        public List<AlgorithmResult> ExecuteWorkflow(string workflowId, Mat inputImage)
        {
            if (!Workflows.ContainsKey(workflowId))
            {
                Logger.LogError($"Workflow does not exist: {workflowId}");
                return new List<AlgorithmResult>();
            }

            var workflow = Workflows[workflowId];
            Logger.LogInfo($"Executing workflow: {workflow.Name}");

            // Workflow.Execute 已迁移到 WorkflowExecutionEngine
            // var results = workflow.Execute(inputImage);
            var results = new List<AlgorithmResult>();

            return results;
        }

        /// <summary>
        /// Execute current workflow
        /// </summary>
        public List<AlgorithmResult> ExecuteCurrentWorkflow(Mat inputImage)
        {
            if (CurrentWorkflow == null)
            {
                Logger.LogWarning("No active workflow");
                return new List<AlgorithmResult>();
            }

            return ExecuteWorkflow(CurrentWorkflow.Id, inputImage);
        }

        /// <summary>
        /// Save workflow to file
        /// </summary>
        public bool SaveWorkflow(string workflowId, string filePath)
        {
            if (!Workflows.ContainsKey(workflowId))
            {
                Logger.LogError($"Workflow does not exist: {workflowId}");
                return false;
            }

            try
            {
                var workflow = Workflows[workflowId];

                // 使用 Workflow 层的序列化配置
                var options = WorkflowSerializationOptions.Default;

                var json = JsonSerializer.Serialize(workflow, options);
                File.WriteAllText(filePath, json);

                Logger.LogInfo($"Workflow saved to: {filePath}");
                Logger.LogInfo($"  - Saved {workflow.Nodes.Count} nodes and {workflow.Connections.Count} connections");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save workflow: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Load workflow from file
        /// </summary>
        public bool LoadWorkflow(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.LogError($"File does not exist: {filePath}");
                return false;
            }

            try
            {
                var json = File.ReadAllText(filePath);

                // 使用 Workflow 层的序列化配置
                var options = WorkflowSerializationOptions.Default;

                var workflow = JsonSerializer.Deserialize<Workflow>(json, options);

                if (workflow != null)
                {
                    // Logger is not assigned to Workflow anymore
                    // workflow.SetLogger(Logger);

                    // Ensure collections are initialized if null
                    if (workflow.Nodes == null)
                    {
                        workflow.Nodes = new ObservableCollection<WorkflowNodeBase>();
                    }

                    if (workflow.Connections == null)
                    {
                        workflow.Connections = new ObservableCollection<WorkflowConnection>();
                    }

                    Workflows[workflow.Id] = workflow;
                    CurrentWorkflow = workflow;

                    Logger.LogInfo($"Workflow loaded from file: {workflow.Name}");
                    Logger.LogInfo($"  - Loaded {workflow.Nodes.Count} nodes and {workflow.Connections.Count} connections");
                    return true;
                }

                Logger.LogError("Failed to load workflow: Deserialization returned null");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load workflow: {ex.Message}", ex);
                return false;
            }
        }


    }
}
