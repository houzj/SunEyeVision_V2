# 点点测量工具逆向主文档

- 工具 ID：imvsp2pmeasuremodu
- 原厂工具：IMVSP2PMeasureModu
- 取证优先级：C0（测量链）
- 当前状态：source-partial

## 已确认

- 工具构造、Clone、Dispose 入口；参数通过 ParamItem 管理角度、距离、中点 X/Y 的启用和上下限。
- 结果包含基本模块结果与原生点点测量结果。

## 证据路径

- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/P2PMeasureModuTool.cs
- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/P2PMeasureModuParams.cs
- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/P2PMeasureResult.cs

## 未决

- 两点输入、计算/判定、Process/ProcessAlg、UI、保存恢复和释放仍需补证。
