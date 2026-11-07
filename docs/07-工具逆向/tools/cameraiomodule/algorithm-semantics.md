# 相机 IO 通信 VM 算法语义

- 当前状态：source-partial；C1 communication-management 前置。

## 已确认

- 工具构造 → 参数对象；参数四项通过 ParamItem 读写。
- 主界面通信管理单例 → GetData/CloseComm/ClearAll/ResetParamStateFlag。
- 主界面设备选择 → AlgorithmTabRoot.FindBaseNodeByName 绑定方案/流程/相机通信设备。
- 结果对象包含基础结果和原生 Camera IO 结构。

## 未确认

| 步骤 | 状态 | 取证任务 |
|---|---|---|
| 网关连接、触发、收发和重连 | unknown | Gateway/驱动入口、线程和错误码 |
| CameraIO Process/ProcessAlg | unknown | 工具专属函数体与 ABI |
| 关闭方案、拔线、窗口退出释放 | unknown | 主界面生命周期和句柄/线程证据 |

证据：CameraIOModuTool.cs、CameraIOModuParams.cs、CameraIOResult.cs、GlobalCommunicationMgr.cs、GlobalCommunication.xaml.cs。
