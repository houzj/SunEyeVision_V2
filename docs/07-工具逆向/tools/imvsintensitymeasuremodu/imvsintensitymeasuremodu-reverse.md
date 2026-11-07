# 亮度测量工具逆向主文档

- 工具 ID：imvsintensitymeasuremodu
- 原厂工具：IMVSIntensityMeasureModu
- 取证优先级：C0（测量链）
- 当前状态：source-partial

## 已确认

- 工具构造、Clone、Dispose 入口；参数类通过 ParamItem 管理最大值、最小值、均值、标准差、对比度的启用和上下限。
- 结果结构包含基本模块结果与原生亮度测量结果。

## 证据路径

- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/IntensityMeasureModuTool.cs
- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/IntensityMeasureModuParams.cs
- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/IntensityMeasureResult.cs

## 未决

- 图像/ROI、统计计算、判定、Process/ProcessAlg、UI、保存恢复和释放仍需补证。
