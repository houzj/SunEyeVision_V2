using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 脚本工具参数 — 包含脚本代码、DLL 引用目录、输入/输出变量定义
    /// </summary>
    [JsonDerivedType(typeof(ScriptParameters), "Script")]
    public class ScriptParameters : ToolParameters, IOutputContractProvider, INotifyOutputContractChanged
    {
        /// <summary>
        /// 最简代码模板 — Init + Process 空壳骨架，无示例注释，仅含 null 检查和输出传递。
        /// 默认新建工具时显示的代码。如果代码不小心被删除，可通过"复位最简"按钮恢复到此模板。
        /// </summary>
        public static string MinimalCode => LoadEmbeddedResource("Resources.MinimalCode.txt");

        /// <summary>
        /// Demo 示例代码 — 纯 IVisionImage 通用接口风格，覆盖图像信息/拷贝/像素数据/全局变量读写。
        /// 用户可通过工具栏"Demo示例"按钮加载此代码进行学习和测试。
        /// 默认引用（系统必需项）即可编译，无需额外勾选 Halcon 引用。
        /// </summary>
        /// <remarks>
        /// 场景1: 图像信息查询（IVisionImage.Width/Height/Channels/IsEmpty）
        /// 场景2: 图像拷贝与像素数据访问（Clone/GetPixelData）
        /// 场景3: 输出传递
        /// 场景4: 全局变量读写（GetGlobal/SetGlobal，通用接口优先）
        /// 注意：input1/output1 类型为 IVisionImage（通用接口，不涉及 HalImage 具体类型）；
        ///       需 Halcon 底层算子时，通过"程序集引用"勾选 halcondotnetxl 后自行扩展。
        /// </remarks>
        public static string DemoCode => LoadEmbeddedResource("Resources.DemoCode.txt");

        /// <summary>
        /// 向后兼容别名 — DefaultMethodCode 目前指向 DemoCode
        /// </summary>
        public static string DefaultMethodCode => DemoCode;

        private string _scriptCode = MinimalCode;
        private string _assemblyReferenceDir = "ScriptRefs";
        private ObservableCollection<ScriptInputConfig> _inputs =
        [
            new ScriptInputConfig { Name = "input1", TypeName = "Image" }
        ];
        private ObservableCollection<ScriptOutputConfig> _outputs =
        [
            new ScriptOutputConfig { Name = "output1", TypeName = "Image" }
        ];

        public ScriptParameters()
        {
            // 默认引用系统必需项（BCL + Script 基类）— 编译基线最小化，halcon/OpenCvSharp 按需勾选
            _assemblyReferences = new ObservableCollection<string>(
                SunEyeVision.Tool.Script.ScriptEngine.MetadataReferenceProvider.RequiredAssemblyNames);
            _assemblyReferences.CollectionChanged += OnReferencesChanged;
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(ScriptCode) or nameof(AssemblyReferenceDir) or nameof(AssemblyReferences))
                    System.Threading.Interlocked.Increment(ref _scriptRevision);
            };
            SubscribeInputsCollection();
            SubscribeOutputsCollection();
        }

        /// <summary>
        /// 脚本方法体代码（不含 class 声明，由 BuildFullCode 自动包裹）
        /// </summary>
        [JsonPropertyOrder(1)]
        public string ScriptCode
        {
            get => _scriptCode;
            set => SetProperty(ref _scriptCode, value, "脚本代码");
        }

        /// <summary>
        /// 程序集引用目录（相对路径，相对于插件输出目录）。目录下的所有 *.dll 均为程序集引用。
        /// </summary>
        [JsonPropertyOrder(2)]
        public string AssemblyReferenceDir
        {
            get => _assemblyReferenceDir;
            set => SetProperty(ref _assemblyReferenceDir, value, "程序集引用目录");
        }

        private long _scriptRevision;
        [JsonIgnore]
        public long ScriptRevision => System.Threading.Interlocked.Read(ref _scriptRevision);
        private void OnReferencesChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => OnPropertyChanged(nameof(AssemblyReferences));
        internal SunEyeVision.Tool.Script.ScriptEngine.ScriptReferenceSnapshot CaptureReferences()
            => SunEyeVision.Tool.Script.ScriptEngine.ScriptReferenceSnapshot.Capture(AssemblyReferences.ToArray(), AssemblyReferenceFullPath);

        private ObservableCollection<string> _assemblyReferences;

        /// <summary>
        /// 每节点启用的程序集引用列表（唯一真相源）— 编译只引用此列表里的程序集。
        /// 序列化保存到节点配置，随方案加载/保存，各脚本节点独立。
        /// 新节点默认预置系统必需项（BCL + Script 基类），编译基线最小化。
        /// </summary>
        [JsonPropertyOrder(5)]
        public ObservableCollection<string> AssemblyReferences
        {
            get => _assemblyReferences;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (ReferenceEquals(_assemblyReferences, value)) return;
                _assemblyReferences.CollectionChanged -= OnReferencesChanged;
                SetProperty(ref _assemblyReferences, value, "程序集引用");
                _assemblyReferences.CollectionChanged += OnReferencesChanged;
            }
        }

        /// <summary>
        /// 输入绑定解析后的实际值（运行时填充，供调试监视面板显示）
        /// 相当于普通工具的 ResolvedInputImage —— 都是"绑定解析完成后得到的实际输入值"
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, object?>? ResolvedInputValues { get; set; }

        /// <summary>
        /// 调试模式标志（运行时填充，不序列化）— 脚本调试窗口点"执行"时置 true。
        /// 为 true 时工具执行摘要（Script complete/failed）写入 <see cref="ScriptResults.LogMessages"/>
        /// 供调试窗口日志区展示，不写主界面日志（避免调试噪音）；工作流执行为 false 时保持写主界面。
        /// </summary>
        [JsonIgnore]
        public bool IsDebugMode { get; set; }

        /// <summary>
        /// 获取引用目录的完整路径（基于工具自身 Assembly.Location 解析，
        /// 跟随 OutDir 迁移，不依赖主程序 AppDomain.BaseDirectory）。
        /// 注：typeof(ScriptParameters).Assembly.Location = 工具 DLL 所在目录
        /// （如 dev/Debug/plugins/SunEyeVision.Tool.Script/），
        /// AssemblyReferenceDir 默认 "ScriptRefs" 拼接后指向同目录下的 ScriptRefs 子目录。
        /// </summary>
        [JsonIgnore]
        public string AssemblyReferenceFullPath
        {
            get
            {
                var assemblyDir = Path.GetDirectoryName(typeof(ScriptParameters).Assembly.Location)
                    ?? AppContext.BaseDirectory;
                return Path.GetFullPath(Path.Combine(assemblyDir, _assemblyReferenceDir));
            }
        }

        /// <summary>
        /// 输入变量配置列表（含 Name/TypeName/绑定关系）
        /// </summary>
        [JsonPropertyOrder(3)]
        public ObservableCollection<ScriptInputConfig> Inputs
        {
            get => _inputs;
            set
            {
                if (_inputs == value) return;
                UnsubscribeInputsCollection();
                SetProperty(ref _inputs, value, "输入");
                SubscribeInputsCollection();
                System.Threading.Interlocked.Increment(ref _scriptRevision);
            InputContractChanged?.Invoke();
            }
        }

        /// <summary>
        /// 输入契约变更通知（新增/删除/重命名/改类型等）— 供编辑器重建 workspace，保证诊断用最新字段类型。
        /// </summary>
        public event Action? InputContractChanged;

        /// <summary>
        /// INotifyOutputContractChanged — 输出契约变更时通知 UI 实时刷新模块结果树
        /// </summary>
        public event Action? OutputContractChanged;

        private void SubscribeInputsCollection()
        {
            _inputs.CollectionChanged += OnInputsCollectionChanged;
            foreach (var item in _inputs)
                item.PropertyChanged += OnInputPropertyChanged;
        }

        private void UnsubscribeInputsCollection()
        {
            _inputs.CollectionChanged -= OnInputsCollectionChanged;
            foreach (var item in _inputs)
                item.PropertyChanged -= OnInputPropertyChanged;
        }

        private void OnInputsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ScriptInputConfig item in e.NewItems)
                    item.PropertyChanged += OnInputPropertyChanged;
            if (e.OldItems != null)
                foreach (ScriptInputConfig item in e.OldItems)
                    item.PropertyChanged -= OnInputPropertyChanged;
            System.Threading.Interlocked.Increment(ref _scriptRevision);
            InputContractChanged?.Invoke();
        }

        private void OnInputPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            System.Threading.Interlocked.Increment(ref _scriptRevision);
            InputContractChanged?.Invoke();
        }

        private void SubscribeOutputsCollection()
        {
            _outputs.CollectionChanged += OnOutputsCollectionChanged;
            foreach (var item in _outputs)
                item.PropertyChanged += OnOutputPropertyChanged;
        }

        private void UnsubscribeOutputsCollection()
        {
            _outputs.CollectionChanged -= OnOutputsCollectionChanged;
            foreach (var item in _outputs)
                item.PropertyChanged -= OnOutputPropertyChanged;
        }

        private void OnOutputsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ScriptOutputConfig item in e.NewItems)
                    item.PropertyChanged += OnOutputPropertyChanged;
            if (e.OldItems != null)
                foreach (ScriptOutputConfig item in e.OldItems)
                    item.PropertyChanged -= OnOutputPropertyChanged;
            System.Threading.Interlocked.Increment(ref _scriptRevision);
            OutputContractChanged?.Invoke();
        }

        private void OnOutputPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            System.Threading.Interlocked.Increment(ref _scriptRevision);
            OutputContractChanged?.Invoke();
        }

        /// <summary>
        /// 输出变量配置列表（含 Name/TypeName/绑定关系）
        /// </summary>
        [JsonPropertyOrder(4)]
        public ObservableCollection<ScriptOutputConfig> Outputs
        {
            get => _outputs;
            set
            {
                if (_outputs == value) return;
                UnsubscribeOutputsCollection();
                SetProperty(ref _outputs, value, "输出");
                SubscribeOutputsCollection();
                System.Threading.Interlocked.Increment(ref _scriptRevision);
            OutputContractChanged?.Invoke();
            }
        }

        /// <summary>
        /// 系统生成的字段声明 partial class（不含用户代码，由 Inputs/Outputs 字段声明生成）
        /// </summary>
        [JsonIgnore]
        public string GeneratedCode => BuildGeneratedCode();

        private string BuildGeneratedCode()
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using SunEyeVision.Plugin.SDK.Scripting;");
            sb.AppendLine("using SunEyeVision.Plugin.SDK.Vision.Contracts;");
            sb.AppendLine("using SunEyeVision.Plugin.SDK.Execution;");
            sb.AppendLine();
            sb.AppendLine("public partial class UserScript : Script");
            sb.AppendLine("{");

            // 输入字段
            foreach (var input in Inputs)
                sb.AppendLine($"    {input.GenerateFieldCode()}");

            if (Inputs.Count > 0)
                sb.AppendLine();

            // 输出字段
            foreach (var output in Outputs)
                sb.AppendLine($"    {output.GenerateFieldCode()}");

            sb.AppendLine("}");
            return sb.ToString();
        }

        public new string GetParameterSummary()
        {
            var dir = AssemblyReferenceFullPath;
            var count = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.dll").Length : 0;
            return $"脚本行数: {ScriptCode.Split('\n').Length}, 引用DLL: {count}, 输入: {Inputs.Count}, 输出: {Outputs.Count}";
        }

        IReadOnlyList<OutputVariable> IOutputContractProvider.GetOutputContract()
        {
            var variables = new List<OutputVariable>();

            // 元信息：执行信息分组
            variables.Add(new OutputVariable { Name = "Status", VariableType = "String", DisplayName = "运行状态", Group = "执行信息", Kind = OutputVariableKind.Data });
            variables.Add(new OutputVariable { Name = "ExecutionTimeMs", VariableType = "Integer", DisplayName = "执行耗时(ms)", Group = "执行信息", Kind = OutputVariableKind.Data });
            variables.Add(new OutputVariable { Name = "CompileTimeMs", VariableType = "Integer", DisplayName = "编译耗时(ms)", Group = "执行信息", Kind = OutputVariableKind.Data });
            variables.Add(new OutputVariable { Name = "LogCount", VariableType = "Integer", DisplayName = "日志条数", Group = "执行信息", Kind = OutputVariableKind.Data });
            variables.Add(new OutputVariable { Name = "CompileErrorCount", VariableType = "Integer", DisplayName = "编译错误数", Group = "执行信息", Kind = OutputVariableKind.Data });

            // 用户配置的输出
            foreach (var cfg in Outputs)
            {
                variables.Add(new OutputVariable
                {
                    Name = cfg.Name,
                    FieldId = cfg.FieldId, IsCollection = cfg.IsAccumulated || cfg.ParamType?.IsArray == true,
                    FixedCount = cfg.IsAccumulated || cfg.ParamType?.IsArray == true ? null : 1,
                    VariableType = cfg.TypeName,
                    DisplayName = cfg.Name,
                    Group = string.IsNullOrEmpty(cfg.Group) ? "输出结果" : cfg.Group,
                    Kind = OutputVariableKind.Data,
                });
            }

            return variables;
        }

        /// <summary>
        /// 从嵌入式资源加载文本内容
        /// </summary>
        private static string LoadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fullName = $"{assembly.GetName().Name}.{resourceName}";
            using var stream = assembly.GetManifestResourceStream(fullName);
            if (stream == null)
                throw new InvalidOperationException($"嵌入式资源 '{fullName}' 未找到。确认 Build Action 设置为 Embedded resource。");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
