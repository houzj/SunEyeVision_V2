# VisionMaster 独立兼容重建

目标：用户授权的内部测试，独立运行、算法自行实现，最终兼容原 VM 操作、方案、模块、相机与SDK。当前处于主框架基础实现阶段，尚不是完整复刻。

## 当前主体

- VisionData：独立基础数据。
- VisionFramework：方案、节点、连接、ROI、资源、模块契约、持久化、验证和DAG执行引擎。
- VisionModules：首批五种内置模块。
- VisionGraphRunner：无界面的通用模块图执行入口。
- VisionReplica.slnx：统一工程。

架构和具体边界见 docs/architecture.md。双线计划和当前状态见 [ROADMAP.md](ROADMAP.md)，框架逐项逆向/实现/验收清单见 [FRAMEWORK-REVERSE-CHECKLIST.md](docs/FRAMEWORK-REVERSE-CHECKLIST.md)。

## 验证主体闭环

双击 Run-Architecture-Demo.cmd，加载 examples/architecture-demo.vmx.json 并执行五节点双链路方案，结果保存到 analysis/architecture-demo-result.json。发布运行器位于 dist/VisionGraphRunner；必须保留整个目录。

```powershell
dotnet build VisionReplica.slnx
dotnet run --project src/VisionGraphRunner -- catalog
dotnet run --project src/VisionGraphRunner -- validate examples/architecture-demo.vmx.json
dotnet run --project src/VisionGraphRunner -- run examples/architecture-demo.vmx.json
```

当前独立格式 *.vmx.json，与原.sol不同；现有 WPF 通用图编辑器已具备部分流程编辑和 Group 内部画布能力，仍有循环、累积输出及完整交互差异未完成。SessionStatus.Success不是检测合格，需检查具体节点输出。

## 测试

- tests/VisionFramework.PersistenceChecks：多流程、保存、ROI和资源。
- tests/VisionFramework.ExecutionChecks：类型/拓扑/错误/取消/超时/生命周期/快照。
- tests/VisionModules.Checks：实际模块行为。
- tests/VisionFramework.DebugChecks：独立模块调试、缓存输入、连续/停止/关闭等待；接口见 docs/DEBUG-SESSION-CONTRACT.md。
- tests/VisionFramework.IntegrationChecks：真实样例的两条数据链和异常传播。
- 其他tests：已有算法、BMP、参数容器和旧固定计划的回归基线。

## 既有实验及证据

Start-Workbench.cmd 是此前的专用找圆验证界面，不是新主体架构的通用编辑器。SolutionInspector保留为原方案只读分析工具。analysis/phase-2.md、phase-3.md记录历史成果；安装清单、SDK契约、REA证据和兼容矩阵同在analysis。

当前拆为框架与工具插件两条线：框架优先补公共契约、Group循环/累积输出及交互边界；插件线逐工具交付，框架线持续集成。原方案往返、设备、SDK及完整兼容须单独逐项验收。
