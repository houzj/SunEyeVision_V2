using System;
using System.Collections.Generic;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 文本样式 - SDK级文本样式配置（持久化，UI双向绑定，全链路共享同一实例）
    /// </summary>
    public class TextStyle : ObservableObject
    {
        private string _name = "";
        private bool _isVisible = true;
        private Color _okColor = Color.FromRgb(0, 255, 0);
        private Color _ngColor = Color.FromRgb(255, 0, 0);
        private double _opacity = 1.0;
        private double _fontSize = 14.0;
        private bool _isBold;
        private double _positionX = 10;
        private double _positionY = 10;

        // PositionX 绑定状态持久化字段（ConfigSetting 关闭再打开后恢复用）
        private bool _isPositionXBinding;
        private string _positionXBindingSource = "";
        private string _positionXBindingDisplayText = "";

        // PositionY 绑定状态持久化字段
        private bool _isPositionYBinding;
        private string _positionYBindingSource = "";
        private string _positionYBindingDisplayText = "";

        /// <summary>样式名称</summary>
        public string Name { get => _name; set => SetProperty(ref _name, value, "名称"); }

        /// <summary>是否可见</summary>
        public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value, "可见性"); }

        /// <summary>OK状态文本颜色</summary>
        public Color OkColor { get => _okColor; set => SetProperty(ref _okColor, value, "OK颜色"); }

        /// <summary>NG状态文本颜色</summary>
        public Color NgColor { get => _ngColor; set => SetProperty(ref _ngColor, value, "NG颜色"); }

        /// <summary>透明度(0-1)，渲染时通过 PushOpacity 生效</summary>
        public double Opacity { get => _opacity; set => SetProperty(ref _opacity, value, "透明度"); }

        /// <summary>字号</summary>
        public double FontSize { get => _fontSize; set => SetProperty(ref _fontSize, value, "字号"); }

        /// <summary>是否加粗</summary>
        public bool IsBold { get => _isBold; set => SetProperty(ref _isBold, value, "加粗"); }

        /// <summary>X坐标位置（基准位置或ParamBinding动态值）</summary>
        public double PositionX { get => _positionX; set => SetProperty(ref _positionX, value, "X坐标"); }

        /// <summary>Y坐标位置（基准位置或ParamBinding动态值）</summary>
        public double PositionY { get => _positionY; set => SetProperty(ref _positionY, value, "Y坐标"); }

        // ===== PositionX 绑定状态持久化（ConfigSetting 关闭再打开后恢复绑定模式用） =====

        /// <summary>PositionX 是否为绑定模式</summary>
        internal bool IsPositionXBinding { get => _isPositionXBinding; set => SetProperty(ref _isPositionXBinding, value); }

        /// <summary>PositionX 绑定源标识</summary>
        internal string PositionXBindingSource { get => _positionXBindingSource; set => SetProperty(ref _positionXBindingSource, value); }

        /// <summary>PositionX 绑定显示文本</summary>
        internal string PositionXBindingDisplayText { get => _positionXBindingDisplayText; set => SetProperty(ref _positionXBindingDisplayText, value); }

        // ===== PositionY 绑定状态持久化 =====

        /// <summary>PositionY 是否为绑定模式</summary>
        internal bool IsPositionYBinding { get => _isPositionYBinding; set => SetProperty(ref _isPositionYBinding, value); }

        /// <summary>PositionY 绑定源标识</summary>
        internal string PositionYBindingSource { get => _positionYBindingSource; set => SetProperty(ref _positionYBindingSource, value); }

        /// <summary>PositionY 绑定显示文本</summary>
        internal string PositionYBindingDisplayText { get => _positionYBindingDisplayText; set => SetProperty(ref _positionYBindingDisplayText, value); }

        // ===== 绑定配置存取方法（由 DispTextColorControl 通过 IBindingConfigProvider 调用） =====

        internal ParamSetting? GetBindingConfig(string parameterName)
        {
            if (parameterName == "PositionX")
            {
                if (!_isPositionXBinding || string.IsNullOrEmpty(_positionXBindingSource))
                    return null;

                var parts = _positionXBindingSource.Split('.');
                var nodeId = parts.Length > 0 ? parts[0] : string.Empty;
                var property = parts.Length > 1 ? parts[1] : _positionXBindingSource;

                var setting = ParamSetting.CreateBinding(parameterName, nodeId, property);
                if (!string.IsNullOrEmpty(_positionXBindingDisplayText))
                {
                    var displayParts = _positionXBindingDisplayText.Split('.');
                    if (displayParts.Length == 2)
                    {
                        setting.SourceNodeName = displayParts[0];
                        setting.SourceDisplayName = displayParts[1];
                    }
                }
                return setting;
            }

            if (parameterName == "PositionY")
            {
                if (!_isPositionYBinding || string.IsNullOrEmpty(_positionYBindingSource))
                    return null;

                var parts = _positionYBindingSource.Split('.');
                var nodeId = parts.Length > 0 ? parts[0] : string.Empty;
                var property = parts.Length > 1 ? parts[1] : _positionYBindingSource;

                var setting = ParamSetting.CreateBinding(parameterName, nodeId, property);
                if (!string.IsNullOrEmpty(_positionYBindingDisplayText))
                {
                    var displayParts = _positionYBindingDisplayText.Split('.');
                    if (displayParts.Length == 2)
                    {
                        setting.SourceNodeName = displayParts[0];
                        setting.SourceDisplayName = displayParts[1];
                    }
                }
                return setting;
            }

            return null;
        }

        internal void SetBindingConfig(string parameterName, ParamSetting? config)
        {
            if (parameterName == "PositionX")
            {
                if (config == null || config.BindingType != BindingType.Binding)
                {
                    IsPositionXBinding = false;
                    PositionXBindingSource = "";
                    PositionXBindingDisplayText = "";
                }
                else
                {
                    IsPositionXBinding = true;
                    PositionXBindingSource = string.IsNullOrEmpty(config.SourceProperty)
                        ? config.SourceNodeId ?? ""
                        : $"{config.SourceNodeId}.{config.SourceProperty}";
                    PositionXBindingDisplayText = !string.IsNullOrEmpty(config.SourceNodeName) && !string.IsNullOrEmpty(config.SourceDisplayName)
                        ? $"{config.SourceNodeName}.{config.SourceDisplayName}"
                        : PositionXBindingSource;
                }
            }
            else if (parameterName == "PositionY")
            {
                if (config == null || config.BindingType != BindingType.Binding)
                {
                    IsPositionYBinding = false;
                    PositionYBindingSource = "";
                    PositionYBindingDisplayText = "";
                }
                else
                {
                    IsPositionYBinding = true;
                    PositionYBindingSource = string.IsNullOrEmpty(config.SourceProperty)
                        ? config.SourceNodeId ?? ""
                        : $"{config.SourceNodeId}.{config.SourceProperty}";
                    PositionYBindingDisplayText = !string.IsNullOrEmpty(config.SourceNodeName) && !string.IsNullOrEmpty(config.SourceDisplayName)
                        ? $"{config.SourceNodeName}.{config.SourceDisplayName}"
                        : PositionYBindingSource;
                }
            }
        }

        /// <summary>
        /// 解析所有绑定属性的运行时值（在工具执行后调用）
        /// </summary>
        /// <param name="nodeResults">节点结果缓存（用于动态绑定解析）</param>
        /// <param name="resolver">参数解析器（可选，默认新建）</param>
        public void ResolveBindings(IDictionary<string, ToolResults> nodeResults, IParameterResolver? resolver = null)
        {
            resolver ??= new ParameterResolver();

            // 解析 PositionX 绑定
            var bindingX = GetBindingConfig("PositionX");
            if (bindingX != null)
            {
                var result = resolver.Resolve(bindingX, nodeResults, typeof(double));
                if (result.IsSuccess && result.Value != null)
                {
                    PositionX = Convert.ToDouble(result.Value);
                }
            }

            // 解析 PositionY 绑定
            var bindingY = GetBindingConfig("PositionY");
            if (bindingY != null)
            {
                var result = resolver.Resolve(bindingY, nodeResults, typeof(double));
                if (result.IsSuccess && result.Value != null)
                {
                    PositionY = Convert.ToDouble(result.Value);
                }
            }
        }

        /// <summary>获取指定状态的有效颜色</summary>
        public Color GetEffectiveColor(bool isOk) => isOk ? OkColor : NgColor;

        /// <summary>获取指定状态的有效颜色（ARGB uint 格式，用于渲染层）</summary>
        public uint GetEffectiveColorArgb(bool isOk)
        {
            var c = GetEffectiveColor(isOk);
            return (uint)((c.A << 24) | (c.R << 16) | (c.G << 8) | c.B);
        }
    }
}
