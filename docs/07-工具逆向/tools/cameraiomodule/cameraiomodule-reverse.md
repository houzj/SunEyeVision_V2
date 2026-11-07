# 相机 IO 通信工具逆向主文档

- 工具 ID：cameraiomodule
- 原厂工具：CameraIOModule
- 组装类别：扩展控件与专属面板
- 取证优先级：C1（通信管理与测量/标定链前置）
- 当前状态：source-partial

## 已确认语义

- CameraIOModuTool 构造时创建 CameraIOModuParams；Clone 返回新工具；Dispose 方法体为空。
- 参数包含 CameraType（Common/Smart）、IODurationTime、IOEnableElectricalLevel（High/Low）和 IOOutType（Ok/Ng）。
- CameraIOResult 携带基础模块结果和原生相机 IO 结果结构。
- GlobalCommunicationMgr 是单例管理器，GetData/CloseComm/ClearAll/ResetParamStateFlag 转发到主界面通信入口。
- GlobalCommunication 暴露方案加载、流程控制、相机响应三类通信设备选择，并通过 AlgorithmTabRoot 从模块参数装配。

## 证据路径

- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/CameraIOModuTool.cs
- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/CameraIOModuParams.cs
- analysis/decompiled/iMVS-6000PlatformSDKCS.PublicAudit/iMVS_6000PlatformSDKCS.ObjectPlatformSDKCS/CameraIOResult.cs
- analysis/decompiled/VMControls.WPF/VMControls.WPF.GlobalUI/GlobalCommunicationMgr.cs
- analysis/decompiled/VMControls.WPF/VMControls.WPF.GlobalUI/GlobalCommunication.xaml.cs

## 未决

- Process/ProcessAlg、网关协议、连接/重连/触发时序、物理 IO 行为和释放链仍需继续取证。
