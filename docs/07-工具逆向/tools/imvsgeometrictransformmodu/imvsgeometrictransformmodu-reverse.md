# 几何变换 工具逆向主文档

- ID `imvsgeometrictransformmodu`；证据状态 `source-partial`；P0 声明式参数页。

## Source-confirmed

`GeometricTransformModuParams` 提供 `MirrorOrientation`（NoMirror/Horizontal/Vertical/HoriAndVert）与 `RotateAngle`（0/90/180/270）枚举 ParamItem；Tool 构造/Clone/Dispose 已恢复。Result 将输出图像缓冲复制到托管内存并由析构释放；ResultIn 暴露 ModuStatus、OutImage。XML/UI、输入输出及资源哈希见 parameters.json/resources.json；通用执行入口为 CProcess.RunOnce→IMVS_PF_ExecuteOnce_V30_CS。

## Unknown

原生变换算法、Process/ProcessAlg ABI、尺寸/像素格式处理、UI 绑定、保存恢复、错误状态和原厂 VM 行为未确认。
