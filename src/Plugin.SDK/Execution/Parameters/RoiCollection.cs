using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 几何输入集合参数 - 一组几何输入（<see cref="Roi"/>），带角色（检测区/屏蔽区）。
    /// 取代历史 <c>RoiCollectionParameter</c>（原集合元素为 UI 层 RoiData）。
    /// 元素级订阅/本地语义由 <see cref="Roi"/> 自身承载；本集合仅管理一组 + 角色。
    /// </summary>
    public class RoiCollection : ParamValueBase
    {
        private readonly ObservableCollection<Roi> _items;
        private RoiParameterMode _mode;
        private ParamSetting? _bindingConfig;
        private object? _bindingConstantValue;

        /// <summary>构造函数</summary>
        public RoiCollection() : this(RoiParameterMode.InspectionRoi)
        {
        }

        /// <summary>构造函数（指定角色）</summary>
        public RoiCollection(RoiParameterMode mode)
        {
            _items = new ObservableCollection<Roi>();
            _mode = mode;
        }

        /// <summary>几何输入集合（元素为 <see cref="Roi"/>，非像素 Region）</summary>
        public ObservableCollection<Roi> Rois
        {
            get => _items;
            set
            {
                if (value != null && value != _items)
                {
                    _items.Clear();
                    foreach (var item in value)
                    {
                        _items.Add(item);
                    }
                }
            }
        }

        /// <summary>角色（检测区 / 屏蔽区）</summary>
        public RoiParameterMode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value, "几何输入角色");
        }

        /// <summary>绑定配置（集合级订阅；元素级订阅见 <see cref="Roi.Binding"/>）</summary>
        public override ParamSetting? BindingConfig
        {
            get => _bindingConfig;
            set
            {
                _bindingConfig = value;
                OnPropertyChanged(nameof(BindingConfig));
            }
        }

        /// <summary>参数值（实现 ParamValueBase）</summary>
        [JsonIgnore]
        public override object? ObjectValue
        {
            get => _items;
            set
            {
                if (value is ObservableCollection<Roi> collection)
                {
                    _items.Clear();
                    foreach (var item in collection)
                    {
                        _items.Add(item);
                    }
                }
            }
        }

        /// <summary>值来源标识（实现 ParamValueBase）</summary>
        public override string ValueSource => _bindingConfig != null ? "Binding" : "Constant";

        /// <summary>是否为绑定模式（实现 ParamValueBase）</summary>
        public override bool IsBinding => _bindingConfig != null;

        /// <summary>切换到绑定模式前保存的常量值</summary>
        [JsonIgnore]
        public override object? BindingConstantValue
        {
            get => _bindingConstantValue;
            set => SetProperty(ref _bindingConstantValue, value);
        }

        /// <summary>获取所有启用的几何输入</summary>
        public System.Collections.Generic.IEnumerable<Roi> GetEnabledInputs()
            => _items.Where(r => r.IsEnabled);

        /// <summary>获取所有可见的几何输入</summary>
        public System.Collections.Generic.IEnumerable<Roi> GetVisibleInputs()
            => _items.Where(r => r.IsVisible);

        /// <summary>清空所有输入</summary>
        public void ClearInputs() => _items.Clear();

        /// <summary>添加输入</summary>
        public void AddInput(Roi input) => _items.Add(input);

        /// <summary>移除输入</summary>
        public void RemoveInput(Roi input) => _items.Remove(input);

        /// <summary>克隆</summary>
        public RoiCollection Clone()
        {
            var cloned = new RoiCollection(_mode)
            {
                BindingConfig = BindingConfig
            };
            foreach (var item in _items)
            {
                cloned._items.Add((Roi)item.Clone());
            }
            return cloned;
        }
    }
}
