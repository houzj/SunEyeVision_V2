using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 脚本参数项 — 输入/输出变量的封装模型
    /// 用于调试控件中 DataGrid 的绑定显示和实时监视
    /// </summary>
    public class ScriptParamItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private object? _value;
        private string _typeName = "object";

        /// <summary>
        /// 变量名称
        /// </summary>
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 变量值
        /// </summary>
        public object? Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 值类型名称（如 int, double, string 等）
        /// </summary>
        public string TypeName
        {
            get => _typeName;
            set { _typeName = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 值的字符串表示（用于 DataGrid 显示）
        /// </summary>
        [JsonIgnore]
        public string DisplayValue => Value?.ToString() ?? "(null)";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // Value 变化时刷新 DisplayValue
            if (propertyName == nameof(Value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayValue)));
            }
        }
    }
}