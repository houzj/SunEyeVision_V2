# 圆圆测量工具逆向主文档

- 工具 ID：imvsc2cmeasuremodu
- 原厂工具：IMVSC2CMeasureModu
- 取证优先级：C0（测量链）
- 当前状态：source-partial

## 已确认

- 工具构造、Clone、Dispose 入口存在，参数对象为 C2CMeasureModuParams。
- 参数包含圆选择、两组边缘搜索/极性/阈值/宽度/拟合和多项角度/距离/交点限制 ParamItem。
- 结果构造从原生结构复制两组 caliper region 指针数组，析构释放两组分配缓冲。

## 证据路径

- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/C2CMeasureModuTool.cs
- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/C2CMeasureModuParams.cs
- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/C2CMeasureResult.cs

## 未决

- 圆拟合、卡尺采样、交点/距离输出、Process/ProcessAlg、UI/保存/释放仍需补证。
