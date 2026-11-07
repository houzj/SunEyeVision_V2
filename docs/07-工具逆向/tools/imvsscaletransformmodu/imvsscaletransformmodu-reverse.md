# 单位转换 工具逆向主文档

- 稳定工具 ID：`imvsscaletransformmodu`
- 原厂工具：`IMVSScaleTransformModu`
- 原厂类别：Calculation；UI 类别：声明式参数页；优先级：P0
- 证据状态：`source-partial`（托管对象 SDK 参数/结果/生命周期已恢复；原生算法与WPF事件链未知）
- 原厂资源目录：`D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Calculation\IMVSScaleTransformModu`

## 已确认的源码证据

- `ScaleTransformModuParams : ModuParamsBase` 构造时创建 `ParamItem(this, "LoadCalibPath")`；属性 getter/setter 分别调用 `GetStringValue`/`SetStringValue`。来源：`analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/ScaleTransformModuParams.cs`。
- `ScaleTransformModuTool : CModuleBase` 构造时以 `CVmObjectBase.handle, ModuleID` 创建 Params；`Clone()` 返回新 `ScaleTransformModuTool`；`Dispose()` 为空。来源：同目录 `ScaleTransformModuTool.cs`。
- `ScaleTransformResult` 暴露 `stBasicModuInfo` 与 `stScaleTransformModule`；`ScaleTransformResultIn` 暴露 `ModuStatus`、`PixelScale`、`PhyDist`、`PixDist`（internal set）。来源：`ScaleTransformResult.cs`、`ScaleTransformResultIn.cs`。
- `CommonDefineClass` 将逻辑名 `ScaleTransform` 映射到 `IMVSScaleTransformModu`；`CVmObjectBase` 模块索引表记录该原厂名。来源：`CommonDefineClass.cs`、`CVmObjectBase.cs`。
- `CProcess.RunOnce([commandString])` 通过 `IMVS_PF_ExecuteOnce_V30_CS(handle, processId, command)` 执行并等待 `mProcStateList` 清空；超时/崩溃抛 `VmException`。此为通用流程入口，未证明该工具专属调用链。来源：`CProcess.cs`。
- XML 资源、SHA256、控件和输入/输出声明已登记于 `resources.json`、`parameters.json`；基本页含 `TransferParam`、`LoadCalibPath`、`RefreshSignal`、`CorrectInfo` 控件及 `ImageSourceGroup`、`BottomCommandGroup`。

## 参数、绑定与执行边界

已确认：`LoadCalibPath` 是唯一托管参数；XML 声明 `InputImage`（图像及宽高/像素格式）、`ImageDist`、`RefreshSignal`、`PixelEquivalentCorrectInfo` 输入，以及运行时、状态、像素比例、物理/像素距离和变换分量输出。`parameters.json` 未声明 binding/condition，不能据此推断事件更新策略。

## 未决项（unknown）

原生 DLL 的 Process/ProcessAlg 符号与参数 ABI、标定文件解析、输出计算、错误码/状态转换、XML 控件事件到 ParamItem 的绑定、保存/恢复序列化、结果句柄所有权，以及释放时序均未从当前托管反编译证据确认。未运行原厂 VM。
