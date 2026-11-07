# 主线与工作流开发基线同步

用户要求先同步，统一后各自继续；当前控件与工作流新增功能均冻结。

- 主线输入6e43baed，工作流输入5552fae8，共同祖先a6f58334。工作流独立工作区已封存清洁且验收程序退出。
- 在现有主仓工作区的codex/baseline-sync-20261027分支三方合并，不复制仓库/算法/程序。主线是唯一集成写者，三路只读评审。
- 用户BlobParameterEditor.xaml索引2改动保持未提交，输入SHA另存BASELINE_SYNC_INPUTS_20261027.json；不以暂存或覆盖丢失用户内容。

## 合并设计

1. 保独立Runtime/Wpf组件发现/配对/准入/注册与缺Runtime拒绝、缺Wpf公共视图回退；并入工作流目录、可见性、分类排序与延迟准备，不恢复退休Blob SDK包。发布目录和清单在SHA封存前统一生成。
2. Registry的HasRuntime/CreateRuntime/CreateSettingsView/CreateResultView与SDK入口共享同一目录准备协议；目录可见不等于Runtime已注册。
3. ProductionEditorContext合入多草稿ValidateBatch/CommitBatch，同时保Runtime配置验证和PrepareView；不将Runtime草稿改投旧ToolParameters。
4. WorkflowNodeFactory先准备Runtime或当前SDK生产工具的合法配置，成功后取序号；保RuntimeConfiguration.Copy及工作流partial/Group展示初始化。
5. 保稳定FieldId/循环作用域和运行绑定图、Runtime输出图像读取。循环来源getter动态读取当前reference，拒绝已清空旧身份复活。保留当前SDK工具Group曝光；Runtime曝光尚未接入并继续冻结，后续只能接正式Settings字段合同，不给Blob造SDK影子参数。
6. 保声明结果创建/调试身份门禁/订阅和关闭Dispose；合当前画布/根执行详情/参数窗关闭门禁。UI项目保新Blob工程路径，同时加工作流图标/目录/ToolGroups资源。
7. 原方案与协作记录保双方历史和本次状态，不整文件选一边，不把Git无冲突、旧构建或旧截图当合并验收。

## 验证与分支收口

- 合并后按规定串行UI Release和全量Release，正式主仓产物运行。
- 运行主线受影响的窗口/字段/Runtime编辑/设计资源/实际HALCON整链/组件与运行内核/文档回归；再跑工作流工具箱、当前画布与Group/计时/小地图相关验收。按实际代码接缝确定精确矩阵；缺手工VS和VM外观明确保留未证。
- 核代码增量、发布清单和实际加载SHA，错误修复后重做受影响验证，不套双方旧成绩。
- 验证通过后以同一个集成提交快进main和workflow-toolbox-dataflow；记录双方HEAD和未提交状态，再通知独立工作流聊天恢复。后续各自独立分支开发，不合并未获验收的私有WIP，不推送。

## 本批具体决策与接缝

- Runtime保留既有正式组件预加载准入，SDK保留目录懒加载；统一目录显式区分生产者，Runtime不生成伪SDK清单。Blob分类沿工作流封存源码明确的“定位”，复用现有分类与图标，不增加第16类。
- 根参数订阅消费者直接接控件库ReferenceFieldOptions；旧modal ReferenceSelectionControl不恢复。重建与关闭释放字段编辑器。
- 循环引用描述读取当前身份，清空/替换不复活旧GUID；Runtime图像Port读取拒绝伪造SourceFieldId。
- 真实整链发现连线先发布历史、自动来源后写导致编辑基线失效；两者集中归入AddConnectionCommand及before/after配置快照，含UndoRedo与通知抛错回滚。
- 合并候选UI003：0错误/1232警告/102.64秒；全量002：0错误/6507警告/66.88秒。此后新增上述连线修复，旧成绩仅作为定位记录，待集成提交后的最终同版验证。
- 当前候选已通过窗口42、字段31、设计资源28、Runtime窗口10、图像8、工作流205、真实宿主1、SDK19、目录5、真实主题1；FullChain发现实际接缝而未通过，不得用其前三组冒称整链完成。最终结果另记统一交接。

## 最终结果

产品合并8a98b488，最终UI及全量Release均0错；13轨道和18部署项通过。详见HANDOFF_BASELINE_SYNC_20261027.md与evidence/BASELINE_SYNC_RESULT_20261027.json。后续交接提交仅文档和已运行验收夹具，不重记为产品构建。两分支共同快进后各自开发；不推送，用户XAML保留未提交。
