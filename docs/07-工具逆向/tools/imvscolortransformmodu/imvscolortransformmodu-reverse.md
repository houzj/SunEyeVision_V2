# 颜色转换 工具逆向主文档

- 稳定工具 ID：`imvscolortransformmodu`
- 原厂工具：`IMVSColorTransformModu`
- 原厂类别：ColorProcessing；UI 类别：声明式参数页；优先级：P0
- 证据状态：`source-partial`（托管对象 SDK 参数/结果/生命周期已恢复；原生算法与WPF事件链未知）
- 原厂资源目录：`D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\ColorProcessing\IMVSColorTransformModu`

## 已确认的源码证据

- `ColorTransformModuParams : ModuParamsBase` 构造时创建六个 `ParamItem`：`ColorTransformType`、`ShowChannel`、`RGB2GrayType`、`Rratio`、`Gratio`、`Bratio`。枚举值明确：转换类型 RGB2GRAY=1、RGB2HSV=2、RGB2HSI=3、RGB2YUV=4；显示通道 0/1/2；灰度策略 GeneralRatio=1、AverageRatio=2、ChannelMin=3、ChannelMax=4、UserRatio=5、RChannel=6、BChannel=7、GChannel=8。读写分别走 `GetEnumValue`/`SetEnumValue` 与 `GetIntValue`/`SetIntValue`。来源：`analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/ColorTransformModuParams.cs`。
- `ColorTransformModuTool` 构造时创建参数对象；`Clone()` 返回新工具；`Dispose()` 为空。来源：同目录 `ColorTransformModuTool.cs`。
- `ColorTransformResult` 的内部构造函数复制 `stColorTransOutImg` 数据到 `Marshal.AllocHGlobal` 缓冲；析构函数在构造类型非零且长度大于零时 `Marshal.FreeHGlobal` 并清零指针。`ColorTransformResultIn` 暴露 `ModuStatus` 与 `OutImage`（internal set）。来源：`ColorTransformResult.cs`、`ColorTransformResultIn.cs`。
- `CommonDefineClass` 映射逻辑名 `ColorTransform`→`IMVSColorTransformModu`；模块索引表亦记录该原厂名。来源：`CommonDefineClass.cs`、`CVmObjectBase.cs`。
- 通用 `CProcess.RunOnce([commandString])` 通过 `IMVS_PF_ExecuteOnce_V30_CS` 执行并等待流程状态清空；超时/崩溃抛 `VmException`。来源：`CProcess.cs`，未证明为该插件专属入口。
- XML 资源、SHA256、控件和输入/输出声明已登记于 `resources.json`、`parameters.json`；基本页控件包括三个枚举、`ExpertParam` 展开、三整数比例和三浮点比例。

## 参数、绑定与执行边界

已确认：XML 声明 `InputImage` 图像及宽高/像素格式输入，`OutputImage` 图像及宽高/像素格式输出，并有运行时、状态输出。`RRatioNs`、`GRatioNs`、`BRatioNs` 在 XML 参数清单中存在，但托管 `ColorTransformModuParams` 未暴露对应属性；其绑定和生效条件未知。`parameters.json` 未声明 binding/condition，不能推断控件触发或参数联动。

## 未决项（unknown）

原生 DLL 的 Process/ProcessAlg 符号、图像转换算法、参数默认值与范围校验、通道显示副作用、XML 事件绑定、保存/恢复、错误码/状态映射、跨托管/原生图像缓冲的完整生命周期均未从当前证据确认。未运行原厂 VM。
