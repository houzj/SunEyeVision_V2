using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Tool.Condition.Models;

namespace SunEyeVision.Tool.Condition.Views
{
    /// <summary>
    /// 条件分支调试控件 — 规则表格配置 + 目标节点下拉（画布下游）+ 底部类型下拉新增规则。
    /// 条件列用 SDK ParamBinding（契约 C1：BindingSource 绑定 InputText 单轨；契约 C2：SyncEditingToSource=True）。
    /// 目标列用 GetChildNodes（画布连接的直接下游）。
    /// </summary>
    public partial class ConditionDebugControl : ToolDebugControlBase
    {
        #region Properties

        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(
                nameof(Parameters),
                typeof(ConditionParameters),
                typeof(ConditionDebugControl),
                new PropertyMetadata(null));

        public ConditionParameters Parameters
        {
            get => (ConditionParameters)GetValue(ParametersProperty);
            set => SetValue(ParametersProperty, value);
        }

        /// <summary>判断选项（OK/NG）</summary>
        public List<string> JudgmentOptions { get; } = new() { "OK", "NG" };

        /// <summary>画布连接的直接下游节点（目标下拉数据源）</summary>
        public List<ParentNodeInfo> ChildNodeOptions { get; } = new();

        /// <summary>调试窗口首选尺寸（列合计约 656px + 控件边距，收窄避免过宽）</summary>
        public override Size PreferredDebugWindowSize => new Size(720, 600);

        #endregion

        #region Constructor

        public ConditionDebugControl()
        {
            InitializeComponent();
            Parameters = new ConditionParameters();
            TypeCombo.SelectedIndex = 0;  // 默认 Int
        }

        #endregion

        #region Overrides

        public override void SetParameters(ToolParameters parameters)
        {
            base.SetParameters(parameters);
            // 零兼容（rule-008/rule-015）：参数类型不符直接报错，绝不静默落到临时实例导致规则丢失。
            if (parameters is not ConditionParameters condParams)
            {
                throw new InvalidOperationException(
                    $"条件分支节点参数类型异常（期望 ConditionParameters，实际 {parameters?.GetType().Name ?? "null"}）。" +
                    "旧格式/损坏的方案节点无法打开，请删除该节点后重新创建。");
            }
            Parameters = condParams;
        }

        /// <summary>
        /// 设置当前节点后，从 DataProvider（DataSourceQueryService）拉取画布下游节点填充目标下拉。
        /// </summary>
        public override void SetCurrentNode(object node)
        {
            base.SetCurrentNode(node);
            RefreshChildNodes();
        }

        /// <summary>
        /// 设置数据提供者后，拉取画布下游节点填充目标下拉。
        /// </summary>
        public override void SetDataProvider(object dataProvider)
        {
            base.SetDataProvider(dataProvider);
            RefreshChildNodes();
        }

        protected override object ExecuteTool()
        {
            if (Parameters == null)
                throw new InvalidOperationException("Parameters not initialized.");

            var tool = new ConditionTool();
            return tool.Run(Parameters);
        }

        protected override bool CanExecuteTool()
        {
            return Parameters != null;
        }

        #endregion

        #region 下游节点填充

        /// <summary>
        /// 从 DataProvider（DataSourceQueryService）调用 GetChildNodes 填充目标下拉。
        /// 使用当前节点 ID（从 CurrentNode 反射提取，与基类 SetCurrentNode 一致）。
        /// </summary>
        private void RefreshChildNodes()
        {
            ChildNodeOptions.Clear();

            if (DataProvider is not DataSourceQueryService queryService)
                return;

            var nodeId = ExtractNodeId(CurrentNode);
            if (string.IsNullOrEmpty(nodeId))
                return;

            var children = queryService.GetChildNodes(nodeId);
            foreach (var child in children)
            {
                ChildNodeOptions.Add(child);
            }
        }

        private static string? ExtractNodeId(object? node)
        {
            if (node == null)
                return null;
            var idProperty = node.GetType().GetProperty("Id");
            return idProperty?.GetValue(node) as string;
        }

        #endregion

        #region 规则操作

        /// <summary>
        /// 新增规则：按底部类型下拉选择类型，自动命名（int0/int1/float0/string0）。
        /// 参考 ScriptVariableTab.AddVariable_Click（方案 §5 新增栏算法）。
        /// </summary>
        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            if (Parameters == null)
                return;

            var ruleType = TypeCombo.SelectedItem as string ?? "Int";
            var prefix = ruleType.ToLower();
            int maxIndex = Parameters.Rules
                .Where(r => r.Name.StartsWith(prefix))
                .Select(r => int.TryParse(r.Name[prefix.Length..], out var n) ? n : -1)
                .DefaultIfEmpty(-1).Max();

            Parameters.Rules.Add(new ConditionRule
            {
                RuleType = ruleType,
                Name = $"{prefix}{maxIndex + 1}",
                MinValue = 0,
                MaxValue = 1000,
                Judgment = "OK"
            });
        }

        /// <summary>
        /// 删除规则。
        /// </summary>
        private void DeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if (Parameters == null)
                return;

            if ((sender as FrameworkElement)?.DataContext is ConditionRule rule)
            {
                Parameters.Rules.Remove(rule);
            }
        }

        #endregion
    }
}
