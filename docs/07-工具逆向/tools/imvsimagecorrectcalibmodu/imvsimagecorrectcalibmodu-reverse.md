# 畸变校正工具逆向主文档

- 工具 ID：imvsimagecorrectcalibmodu
- 原厂工具：IMVSImageCorrectCalibModu
- 取证优先级：C0（标定链）
- 当前状态：source-partial

## 已确认

- ImageCorrectModuTool 创建参数对象，Clone 返回新实例，Dispose 方法体为空。
- 参数 LoadCalibPath 写入标定文件路径；CorrectModel 有 Linear、Radial、PersRadial 三种枚举。
- ImageCorrectResult 从原生结构复制校正图像缓冲，析构时释放托管分配的图像内存。

## 证据路径

- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/ImageCorrectModuTool.cs
- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/ImageCorrectModuParams.cs
- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/ImageCorrectResult.cs

## 未决

- 校正算法、标定文件解析、Process/ProcessAlg、输入输出状态和 UI/保存/停止释放仍需补证。
