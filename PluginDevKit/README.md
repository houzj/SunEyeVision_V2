# SunEyeVision 外部插件开发包

PluginDevKit 是面向外部工程的发布开发包。外部项目只引用发布目录中的 Contracts、Runtime 和可选 WPF DLL，不引用主仓库源码，也不引用旧 Plugin.SDK。

## 目录

```text
PluginDevKit/
├─ sdk/net9.0/                 发布合同 DLL 与 XML
├─ runtime/                    用于外部调试的正式宿主文件
├─ templates/
│  ├─ RuntimePlugin.Template/  无界面 Runtime 插件
│  └─ WpfTool.Template/        WPF 参数与结果呈现插件
├─ samples/SamplePlugin/       新合同样例
└─ scripts/
   ├─ prepare-devkit.ps1
   ├─ build-sdk.ps1
   ├─ build-runtime.ps1
   └─ launch-debug.ps1
```

## 准备开发包

在主仓库根目录执行：

```powershell
.\PluginDevKit\scripts\prepare-devkit.ps1 -Configuration Release
```

脚本会构建并复制 Contracts、Runtime、WPF 和宿主。`Release` 配置复制正式发布宿主，`Debug` 配置复制 `dev/Debug` 外部开发宿主。生成的 SDK 目录至少包含：

- `SunEyeVision.Plugin.Contracts.dll`
- `SunEyeVision.Plugin.Runtime.dll`
- `SunEyeVision.Plugin.Wpf.dll`
- `SunEyeVision.Foundation.dll`

## 外部工程接入

Runtime 工程从 `RuntimePlugin.Template` 开始，使用 `IToolDefinition<TSettings,TOutput>`、`IToolSession` 和 `ToolRuntimeExecutor`。

WPF 工程从 `WpfTool.Template` 开始，使用 `IToolPresentationFactory`、`DeclaredSettingsView` 和只读结果视图。窗口打开、Tab 切换、关闭退订和资源释放由正式宿主负责。

外部工程验收需要在干净目录完成以下链路：编译、加载、参数快照、执行、结果发布、重复打开、Tab 切换、关闭释放，以及从 Visual Studio 启动或附加 Debug 外部开发宿主调试。正式 Release 宿主继续按发布清单准入，不把外部开发组件写入产品清单。验收记录必须包含 SDK DLL 清单、版本合同、宿主版本、依赖和运行日志。

