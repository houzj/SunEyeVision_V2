using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.ViewModels
{
    public class TargetParamTreeNode : ObservableObject
    {
        private bool? _isSelected;
        private bool _isExpanded = true;

        public string Name { get; set; } = string.Empty;
        public string DisplayPath { get; set; } = string.Empty;
        public string NodeType { get; set; } = "";
        public string? NodeId { get; set; }
        public string? ParameterName { get; set; }
        public ObservableCollection<TargetParamTreeNode> Children { get; set; } = new();
        public TargetParamTreeNode? Parent { get; set; }

        /// <summary>
        /// 非叶子节点支持三态（☐ / ▣ / ☑），叶子仅两态（☐ / ☑）
        /// </summary>
        public bool SupportsTriState => Children.Count > 0;

        /// <summary>
        /// 三态选中：true=全选, false=未选, null=半选
        /// 用户点击非叶子 → 向下全选/全取消；叶子变化 → 同步右侧 + 向上聚合
        /// </summary>
        public bool? IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    // 用户主动点击（有明确 true/false）时向下传播
                    if (value.HasValue && Children.Count > 0)
                        SetChildrenSelected(value.Value);

                    // 叶子节点状态变化 → 通知右侧列表实时同步
                    if (Children.Count == 0)
                        OnLeafSelectionChanged?.Invoke(this);

                    // 向上传播，聚合父节点状态
                    Parent?.UpdateParentCheckState();
                }
            }
        }

        private void SetChildrenSelected(bool isSelected)
        {
            foreach (var child in Children)
            {
                child._isSelected = isSelected;
                child.OnPropertyChanged(nameof(IsSelected));

                if (child.Children.Count == 0)
                    child.OnLeafSelectionChanged?.Invoke(child);
                else
                    child.SetChildrenSelected(isSelected);
            }
        }

        internal void UpdateParentCheckState()
        {
            if (Parent == null) return;

            bool allSelected = Parent.Children.All(c => c._isSelected == true);
            bool noneSelected = Parent.Children.All(c => c._isSelected == false || c._isSelected == null);

            bool? newState = allSelected ? true : (noneSelected ? false : (bool?)null);

            if (Parent._isSelected != newState)
            {
                Parent._isSelected = newState;
                Parent.OnPropertyChanged(nameof(IsSelected));
                Parent.Parent?.UpdateParentCheckState();
            }
        }

        /// <summary>
        /// 初始化时直接设置选中状态（不触发副作用），供 BuildTree 使用
        /// </summary>
        internal void SetInitialSelection(bool isSelected)
        {
            _isSelected = isSelected;
            OnPropertyChanged(nameof(IsSelected));
        }

        /// <summary>
        /// 叶子节点选中变化事件（用于实时同步右侧列表）
        /// </summary>
        public event Action<TargetParamTreeNode>? OnLeafSelectionChanged;

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }
    }

    public class ConsumerItemViewModel : ObservableObject
    {
        public string WorkflowName { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public string ParameterName { get; set; } = string.Empty;
        public string ParameterDisplayName { get; set; } = string.Empty;

        /// <summary>显示路径："工作流名.节点名.参数显示名"</summary>
        public string DisplayPath => $"{WorkflowName}.{NodeName}.{ParameterDisplayName}";

        public ConsumerBinding ToConsumerBinding() => new()
        {
            NodeId = NodeId,
            NodeName = NodeName,
            WorkflowName = WorkflowName,
            ParameterName = ParameterName,
            ParameterDisplayName = ParameterDisplayName
        };
    }

    public class TargetParameterViewModel : ViewModelBase
    {
        private readonly ObservableCollection<ConsumerItemViewModel> _selectedConsumers = new();
        private readonly string? _targetTypeName;
        public ObservableCollection<TargetParamTreeNode> TreeNodes { get; } = new();
        public ReadOnlyObservableCollection<ConsumerItemViewModel> SelectedConsumers { get; }
        public ICommand SaveCommand { get; }
        public ICommand RemoveConsumerCommand { get; }
        public event EventHandler? RequestClose;
        public List<ConsumerBinding> ResultBindings { get; private set; } = new();

        public TargetParameterViewModel(
            IEnumerable<Workflow.Workflow> workflows,
            List<ConsumerBinding>? existingBindings,
            string? targetTypeName = null)
        {
            _targetTypeName = targetTypeName;
            SelectedConsumers = new ReadOnlyObservableCollection<ConsumerItemViewModel>(_selectedConsumers);
            SaveCommand = new RelayCommand(Save);
            RemoveConsumerCommand = new RelayCommand<object?>(RemoveConsumer);
            BuildTree(workflows, existingBindings);
        }

        private void BuildTree(IEnumerable<Workflow.Workflow> workflows, List<ConsumerBinding>? existingBindings)
        {
            var selectedSet = BuildSelectedSet(existingBindings);
            var workflowsList = workflows.ToList();

            // 1. 构建子程序引用映射: $"{parentWfId}|{nodeId}" → subWorkflowId
            var subCalls = new Dictionary<string, string>();
            var referencedIds = new HashSet<string>();

            foreach (var wf in workflowsList)
            {
                foreach (var node in wf.Nodes ?? Enumerable.Empty<WorkflowNodeBase>())
                {
                    var subId = node.SubroutineId;
                    if (string.IsNullOrEmpty(subId) && node.Parameters is SubroutineParameters subParams)
                        subId = subParams.SubroutineId;

                    if (!string.IsNullOrEmpty(subId) && workflowsList.Any(w => w.Id == subId))
                    {
                        subCalls[$"{wf.Id}|{node.Id}"] = subId;
                        referencedIds.Add(subId);
                    }
                }
            }

            // 2. 子工作流查找表
            var subWorkflowMap = workflowsList
                .Where(w => referencedIds.Contains(w.Id))
                .ToDictionary(w => w.Id);

            // 3. 根工作流（不被任何子程序引用的）
            var rootWorkflows = workflowsList.Where(w => !referencedIds.Contains(w.Id));

            // 4. 递归构建嵌套树
            foreach (var wf in rootWorkflows)
            {
                var wfName = wf.Name ?? wf.Id;
                var wfNode = new TargetParamTreeNode
                {
                    Name = wfName,
                    DisplayPath = wfName,
                    NodeType = "workflow",
                    IsExpanded = true
                };

                BuildNodeTree(wf, subCalls, subWorkflowMap, wfNode, selectedSet,
                    new HashSet<string> { wf.Id }, wf.Id);

                if (wfNode.Children.Count > 0)
                    TreeNodes.Add(wfNode);
            }

            // 初始状态传播：自底向上聚合父节点三态
            foreach (var root in TreeNodes)
                PropagateParentStates(root);
        }

        /// <summary>
        /// 提取已有绑定的快速查找集合
        /// </summary>
        private static HashSet<(string nodeId, string paramName)> BuildSelectedSet(
            List<ConsumerBinding>? existingBindings)
        {
            return existingBindings != null
                ? new HashSet<(string, string)>(
                    existingBindings.Select(b => (b.NodeId, b.ParameterName)))
                : new HashSet<(string, string)>();
        }

        /// <summary>
        /// 创建工作流树节点
        /// </summary>
        private static TargetParamTreeNode BuildWorkflowNode(
            Workflow.Workflow wf, string name, TargetParamTreeNode? parent)
        {
            return new TargetParamTreeNode
            {
                Name = name,
                DisplayPath = name,
                NodeType = "workflow",
                IsExpanded = true,
                Parent = parent
            };
        }

        /// <summary>
        /// 递归构建：将 workflow 中的所有节点添加到 parentNode 下（支持子程序嵌套展开）
        /// </summary>
        private void BuildNodeTree(
            Workflow.Workflow workflow,
            Dictionary<string, string> subCalls,
            Dictionary<string, Workflow.Workflow> subWorkflowMap,
            TargetParamTreeNode parentNode,
            HashSet<(string nodeId, string paramName)> selectedSet,
            HashSet<string> visited,
            string parentWfId)
        {
            var nodes = workflow.Nodes?.OrderBy(n => n.GlobalIndex).ToList() ?? new();
            foreach (var node in nodes)
            {
                var nodeName = !string.IsNullOrEmpty(node.DispName) ? node.DispName : node.Name;
                var nodeTreeNode = new TargetParamTreeNode
                {
                    Name = nodeName,
                    DisplayPath = parentNode.DisplayPath + "/" + nodeName,
                    NodeType = "node",
                    NodeId = node.Id,
                    IsExpanded = true,
                    Parent = parentNode
                };

                // 添加参数（ParamValue + 集合型输入）
                var paramInfos = GetParameterNames(node, _targetTypeName);
                foreach (var (paramName, displayName) in paramInfos)
                    AddParameterLeaf(node, nodeTreeNode, paramName, displayName, selectedSet);

                // 递归展开子程序
                var subCallKey = $"{parentWfId}|{node.Id}";
                if (subCalls.TryGetValue(subCallKey, out var subWfId)
                    && subWorkflowMap.TryGetValue(subWfId, out var subWf)
                    && visited.Add(subWf.Id))  // 防止循环引用
                {
                    BuildNodeTree(subWf, subCalls, subWorkflowMap,
                        nodeTreeNode, selectedSet, visited, subWfId);
                }

                if (nodeTreeNode.Children.Count > 0)
                    parentNode.Children.Add(nodeTreeNode);
            }
        }

        /// <summary>
        /// 为节点添加参数叶子节点
        /// </summary>
        private void AddParameterLeaf(
            WorkflowNodeBase node, TargetParamTreeNode parentNode,
            string paramName, string displayName,
            HashSet<(string nodeId, string paramName)> selectedSet)
        {
            var isSelected = selectedSet.Contains((node.Id, paramName));
            var leaf = new TargetParamTreeNode
            {
                Name = displayName,
                DisplayPath = parentNode.DisplayPath + " > " + displayName,
                NodeType = "parameter",
                NodeId = node.Id,
                ParameterName = paramName,
                Parent = parentNode
            };
            leaf.OnLeafSelectionChanged += OnLeafSelectionChanged;
            leaf.SetInitialSelection(isSelected);

            if (isSelected)
            {
                _selectedConsumers.Add(new ConsumerItemViewModel
                {
                    NodeId = node.Id,
                    NodeName = parentNode.Name,
                    WorkflowName = GetWorkflowName(leaf),
                    ParameterName = paramName,
                    ParameterDisplayName = displayName
                });
            }

            parentNode.Children.Add(leaf);
        }

        /// <summary>
        /// 自底向上传播父节点选中状态
        /// </summary>
        private static void PropagateParentStates(TargetParamTreeNode node)
        {
            foreach (var child in node.Children)
                PropagateParentStates(child);
            node.UpdateParentCheckState();
        }

        /// <summary>
        /// 叶子节点选中变化 → 实时同步右侧"目标参数"列表
        /// </summary>
        private void OnLeafSelectionChanged(TargetParamTreeNode leaf)
        {
            if (leaf.IsSelected == true)
            {
                if (!_selectedConsumers.Any(c => c.NodeId == leaf.NodeId && c.ParameterName == leaf.ParameterName))
                {
                    _selectedConsumers.Add(new ConsumerItemViewModel
                    {
                        NodeId = leaf.NodeId!,
                        NodeName = GetNodeName(leaf),
                        WorkflowName = GetWorkflowName(leaf),
                        ParameterName = leaf.ParameterName!,
                        ParameterDisplayName = leaf.Name
                    });
                }
            }
            else
            {
                var existing = _selectedConsumers.FirstOrDefault(
                    c => c.NodeId == leaf.NodeId && c.ParameterName == leaf.ParameterName);
                if (existing != null)
                    _selectedConsumers.Remove(existing);
            }
        }

        private static string GetNodeName(TargetParamTreeNode leaf)
        {
            return leaf.Parent?.Name ?? leaf.NodeId ?? "";
        }

        /// <summary>
        /// 从叶子节点向上遍历获取所属工作流名称
        /// </summary>
        private static string GetWorkflowName(TargetParamTreeNode leaf)
        {
            var current = leaf.Parent;
            while (current?.Parent != null)
                current = current.Parent;
            return current?.Name ?? "";
        }

        private static List<(string name, string displayName)> GetParameterNames(WorkflowNodeBase node, string? targetTypeName)
        {
            var result = new List<(string name, string displayName)>();

            // ═══ 途径1: 从 ParamSettings 获取（用户已配置的参数）═══
            if (node.ParamSettings?.Settings != null && node.ParamSettings.Settings.Any(s => !string.IsNullOrEmpty(s.ParameterName)))
            {
                foreach (var setting in node.ParamSettings.Settings)
                {
                    if (string.IsNullOrEmpty(setting.ParameterName)) continue;
                    var displayName = GetDisplayName(node.Parameters, setting.ParameterName);
                    result.Add((setting.ParameterName, displayName));
                }
                return result.OrderBy(p => p.name).ToList();
            }

            // ═══ 途径2: 首次打开，从 ToolParameters 元数据获取 ═══
            if (node.Parameters != null)
            {
                var bindableProps = node.Parameters.GetBindableInputProperties()
                    .Where(p => IsBindableParamForTargetBinding(p, targetTypeName));

                foreach (var prop in bindableProps)
                {
                    result.Add((prop.Name, GetDisplayName(prop)));
                }

                // ═══ 途径3: 集合型输入（脚本工具 Inputs / 子程序 Inputs）═══
                var inputsProp = node.Parameters.GetType().GetProperty("Inputs");
                if (inputsProp != null && inputsProp.GetValue(node.Parameters) is IEnumerable inputs)
                {
                    foreach (var input in inputs)
                    {
                        var nameProp = input.GetType().GetProperty("Name");
                        var typeNameProp = input.GetType().GetProperty("TypeName");
                        if (nameProp?.GetValue(input) is string name && !string.IsNullOrEmpty(name)
                            && !result.Any(r => r.name == name))
                        {
                            // ★ 类型筛选：按目标类型过滤集合型输入
                            if (!string.IsNullOrEmpty(targetTypeName))
                            {
                                var inputTypeName = typeNameProp?.GetValue(input) as string;
                                if (!string.Equals(inputTypeName, targetTypeName, StringComparison.OrdinalIgnoreCase))
                                    continue;
                            }

                            result.Add((name, name));
                        }
                    }
                }
            }

            return result.OrderBy(p => p.name).ToList();
        }

        /// <summary>
        /// 判断属性是否为目标参数绑定场景中可显示的参数
        /// </summary>
        private static bool IsBindableParamForTargetBinding(PropertyInfo prop, string? targetTypeName)
        {
            // 排除 [JsonIgnore] 运行时注入属性
            if (prop.IsDefined(typeof(JsonIgnoreAttribute), false))
                return false;

            var type = prop.PropertyType;

            // ParamValue<T>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ParamValue<>))
            {
                var genericArg = type.GetGenericArguments()[0];
                if (string.IsNullOrEmpty(targetTypeName))
                    return IsBasicType(genericArg); // 无筛选时只显示基本类型（原行为）
                else
                {
                    var paramTypeName = GlobalVariableTypeMapper.ToGlobalVarType(genericArg);
                    return paramTypeName != null
                        && string.Equals(paramTypeName, targetTypeName, StringComparison.OrdinalIgnoreCase);
                }
            }

            // 直接属性（非 ParamValue 包装）
            if (string.IsNullOrEmpty(targetTypeName))
                return IsBasicType(type);
            else
            {
                var paramTypeName = GlobalVariableTypeMapper.ToGlobalVarType(type);
                return paramTypeName != null
                    && string.Equals(paramTypeName, targetTypeName, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 判断是否为基本数据类型（可绑定到全局变量的类型）
        /// </summary>
        private static bool IsBasicType(Type type)
        {
            return type == typeof(bool)
                || type == typeof(int) || type == typeof(long)
                || type == typeof(float) || type == typeof(double)
                || type == typeof(string)
                || type == typeof(byte) || type == typeof(sbyte)
                || type == typeof(short) || type == typeof(ushort)
                || type == typeof(uint) || type == typeof(ulong)
                || type == typeof(char);
        }

        /// <summary>
        /// 获取属性的中文显示名（优先从 [DisplayName] 特性获取，回退到属性名）
        /// </summary>
        private static string GetDisplayName(PropertyInfo prop)
        {
            var attr = prop.GetCustomAttribute<DisplayNameAttribute>();
            if (attr != null && !string.IsNullOrEmpty(attr.DisplayName))
                return attr.DisplayName;
            return prop.Name;
        }

        /// <summary>
        /// 通过属性名获取中文显示名
        /// </summary>
        private static string GetDisplayName(ToolParameters? parameters, string propertyName)
        {
            if (parameters == null) return propertyName;
            var prop = parameters.GetType().GetProperty(propertyName);
            if (prop == null) return propertyName;
            return GetDisplayName(prop);
        }

        private void Save()
        {
            ResultBindings = new List<ConsumerBinding>();
            foreach (var c in _selectedConsumers)
                ResultBindings.Add(c.ToConsumerBinding());
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        private void RemoveConsumer(object? parameter)
        {
            if (parameter is ConsumerItemViewModel item)
            {
                _selectedConsumers.Remove(item);
                DeselectInTree(item.NodeId, item.ParameterName);
            }
        }

        private void DeselectInTree(string nodeId, string paramName)
        {
            foreach (var wfNode in TreeNodes)
                DeselectRecursive(wfNode, nodeId, paramName);
        }

        private static void DeselectRecursive(TargetParamTreeNode node, string nodeId, string paramName)
        {
            foreach (var child in node.Children)
            {
                if (child.NodeType == "parameter" && child.NodeId == nodeId && child.ParameterName == paramName)
                {
                    child.IsSelected = false;
                    return;
                }
                DeselectRecursive(child, nodeId, paramName);
            }
        }
    }
}
