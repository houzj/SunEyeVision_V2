# 点集对位工具逆向主文档

- 工具 ID：imvsmultipointalignmodu
- 原厂工具：IMVSMultiPointAlignModu
- 取证优先级：C0（测量/标定定位链）
- 当前状态：source-partial

## 已确认

- MultiPointAlignModuTool 构造时创建 MultiPointAlignModuParams(handle, ModuleID)。
- Clone 返回新工具实例；Dispose 方法体为空。
- 参数类当前只有基类构造，没有专属托管字段。
- 结果包含 IMVS_PF_BASIC_MODULE_RESULT_INFO 与 IMVS_PF_MULTIPOINTALIGN_MODU_INFO。

## 证据路径

- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/MultiPointAlignModuTool.cs
- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/MultiPointAlignModuParams.cs
- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/MultiPointAlignResult.cs

## 未决

- 点集输入、匹配/变换算法、Process/ProcessAlg、原生 ABI、结果字段和状态码尚未恢复。
- UI 绑定、显示、保存恢复和释放链仍需通信/图像/ROI前置证据与受控运行。
