using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Tool.Condition.Models;

namespace SunEyeVision.Tool.Condition.Views
{
    /// <summary>
    /// Condition tool debug control — for tuning rule parameters and testing condition evaluation.
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

        #endregion

        #region Constructor

        public ConditionDebugControl()
        {
            InitializeComponent();
            Parameters = new ConditionParameters();
        }

        #endregion

        #region Overrides

        public override void SetParameters(ToolParameters parameters)
        {
            base.SetParameters(parameters);
            if (parameters is ConditionParameters condParams)
            {
                Parameters = condParams;
            }
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
    }
}
