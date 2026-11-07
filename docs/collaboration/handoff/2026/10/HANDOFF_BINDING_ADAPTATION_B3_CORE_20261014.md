# B3绑定配置事务核心首批交接

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，未使用并行写入或跨AI交接，没有代理提交或推送。
- 任务目标：继续统一绑定适配方案的B3，先实现与设计对象隔离的配置事务核心。
- 允许修改目录：D:/MyWork/SunEyeVision_V2-comm；保留用户与B1／B2既有修改，不操作用户设备或另一项目。

## 已修改文件

- src/Workflow/BindingConfigurationStore.cs：独立绑定配置投影、版本发布、完整历史和失效范围。
- src/Workflow/OutputBindingChangeService.cs：候选验证器继承旧正式契约与输入策略基线，引用委托只作用候选参数。
- src/Workflow/ConnectionBindingPolicy.cs：自动图像来源清理同时覆盖持久化ImageSourceRef。
- tests/SunEyeVision.UI.Tests/BindingConfigurationStoreTests.cs：13项新增事务回归。
- 既有主方案、通信实施计划、CURRENT_TASK及本交接。

## 已完成

独立草稿深拷贝工作流、节点、插件运行类型参数、ParamSettings、图像引用、连线及全局变量关系，保留稳定身份，不带工具实例或运行结果。草稿归属和基准版本校验拒绝其他发布器或过期草稿；候选副本先完成适配、解绑及全方案依赖编译，全部成功后才发布一代配置与依赖。循环提交失败不改变旧正式代、历史或用户草稿。

核心撤销和重做恢复整个已接受投影，包括源参数、消费者引用／重配状态、节点／连线及变量生产／消费关系；恢复生成新版本，不重新启用旧版本号。快照只通过深拷贝暴露可编辑模型，旧快照与已提交草稿不会改写新代。并发提交相同基准版本只接受一份。

节点配置变化、全局初值变化与连线变化生成同次发布的结果失效清单，并沿旧／新依赖传播。删除连接保留合法显式绑定，仅清该连接拥有的自动图像来源；持久化图像引用也能删除与完整撤销恢复。

## 未完成与接入顺序

1. 核心尚未作为宿主正式配置权威。工具窗口仍使用B2原链路，必须改为编辑草稿，并让正式提交失败保留用户草稿而不是留下实时模型的半次修改。
2. 接入单节点、工作流、主界面、图像预览及连续运行入口；同一正式代生成运行参数和依赖。无实际变更的编辑提交不应反复堆积历史。
3. 接入保存／加载／导入、变量生产／消费编辑和拓扑命令；CommandManager需将整个配置事务作为历史，不能继续只撤销画布后复查。
4. 宿主应用投影时保留工作流／节点UI身份并刷新编辑器，不替换设备／通讯配置；运行中的全局数据与图像结果空间不作为设计撤销内容。此投影不能用于完整Solution导出。
5. 运行结果携带配置版本并拒绝进入不匹配的设计结果空间。当前IsCurrent仅提供核心判断，宿主结果发布尚未调用。
6. 完成上述接入与生命周期测试后再进入B4增量索引及B5验收，完整U1至U3和P4／编码／P5／P6保持既定顺序。

## 已执行构建与测试

- 最终专项127通过、0失败，其中新增核心13项；证据artifacts/validation/binding-adaptation-b3-20261014/b3-regression.trx。覆盖B1／B2、原提交控件、通信收发、图像保存、数值控件和依赖图。
- 标准全量：dotnet build SunEyeVision.slnx -c Release --no-restore，0错误1152警告。
- 标准UI：dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore，0错误1064警告。
- 常用Release已构建更新，实际隐藏启动工具注册探针退出0，24工具，ReceiveData／SendData／ProtocolParse／ProtocolAssemble／Script／Subroutine正常。证据artifacts/validation/binding-adaptation-b3-20261014/release-toolbox.json；探针未初始化硬件通信。
- git diff --check通过，无空白格式错误；既有CRLF提示不属源码错误。

## 人工验收与边界

本批没有切换现有窗口与执行入口，因此不要求用户按B3完整行为进行验收，也不能宣布整个B3完成。真实Visual Studio设计器、高DPI以及全入口人工验收待接入后记录。当前构建存在项目既有警告，未开展无关清理；文档维护技能用于同步真实实施状态。
