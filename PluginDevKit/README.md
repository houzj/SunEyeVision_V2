# SunEyeVision 外部插件开发包

PluginDevKit 是面向外部工程的发布开发包。外部项目只引用发布目录中的 Contracts、Runtime 和可选 WPF DLL，不引用主仓库源码，也不引用旧 Plugin.SDK。

本开发包同时承载两条彼此隔离的发布边界：`sdk/platform/net9.0` 是对齐 VM Platform SDK 的外部应用合同包；`sdk/tool/net9.0` 是工具作者包的目标目录，当前构建输出仍位于 `sdk/net9.0`。外部应用不能通过 Tool SDK 组装方案，工具作者包也不暴露内部方案执行服务。

## 目录

```text
PluginDevKit/
├─ sdk/platform/net9.0/        Platform SDK 外部应用合同 DLL、XML、manifest
├─ sdk/tool/net9.0/            Tool SDK 发布合同 DLL 与 XML
├─ sdk/net9.0/                 Tool SDK 当前发布目录（迁移期保留）
├─ runtime/                    用于外部调试的正式宿主文件
├─ templates/
│  ├─ PlatformApplication.Template/ 外部应用 Platform SDK 模板
│  ├─ RuntimePlugin.Template/  无界面 Runtime 插件
│  └─ WpfTool.Template/        WPF 参数与结果呈现插件
├─ samples/SamplePlugin/       新合同样例
└─ scripts/
   ├─ prepare-devkit.ps1
   ├─ build-sdk.ps1
   ├─ build-platform-sdk.ps1
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

准备附加调试时可使用 `scripts/launch-debug.ps1 -Detach`。脚本会启动正式宿主并写出 `runtime/external-debug-session.json`，其中包含 PID、宿主路径和附加根目录。该回执用于 Visual Studio 附加定位，脚本自身的启动通过不代表 VS 已完成附加验收。

外部应用模板可直接复制 `templates/PlatformApplication.Template`，并将 `SdkPath` 指向 `sdk/platform/net9.0`。执行 `scripts/build-platform-sdk.ps1 -Configuration Release` 会同时构建并复制 Platform SDK 合同 DLL、宿主适配器及其发布依赖，并写入 `platform-sdk-manifest.json`。模板已在干净验证目录完成“加载 V5 方案→枚举流程/节点和设备身份→单次执行→连续运行一轮并停止→关闭释放”；Visual Studio 实际启动/附加、Group、真实设备连接控制和 WPF 渲染控件仍单独验收，不能把这些未验项写成已完成。
## 与 VM 原厂 SDK 的对应关系

VM 4.4.0 的安装包已确认分为两条产品线：

- **Platform SDK**：面向外部软件，使用 `VM.Core`、`VM.PlatformSDKCS` 和可选 `VMControls`，通过 `VmSolution.Load`、`VmProcedure.Run`、模块结果回调、前端/渲染控件接入方案。
- **MVD Algorithm SDK**：面向算子/工具作者，使用 `MVDSDK.*`、`MVD_Algorithm.Net.dll`、参数控件、算法句柄和部署工具。

本目录当前是第二条产品线的 SunEyeVision 工具作者包，不能冒充外部应用 Platform SDK。外部应用包的目标发布目录为 `sdk/platform/net9.0`，工具作者包的目标发布目录为 `sdk/tool/net9.0`；两者的合同、依赖清单和验收脚本分开维护。VM 静态取证见 [VM原厂SDK发布边界静态取证-20261109.txt](../docs/collaboration/evidence/VM原厂SDK发布边界静态取证-20261109.txt)。

