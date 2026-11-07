# 结果绑定与原栅格集中批交接

- 基线提交：正式产品 `5780c062`；D06整批 `24615965` 合为 `e258a862`，原栅格依赖候选 `70a6036f`。
- 当前分支：main；执行者：Codex，主仓唯一产品写者。工作流聊天在独立工作树修其消费者与测试，不生成私有Release。
- 任务目标：按VM设计，结果树和结果表共用SDK正式输出声明及同一次运行publication，继续打通绑定、图像及显示生命周期。两阶段总目标不缩减。
- 已完成：原ImageLayers精确RLE显示、Domain私有alpha与原像素读取、唯一原view拥有者及分层租约；跨批完整读取、预算整批拒绝、模板重建、异步故障隔离、读像素pin及旧清理/释放重入。独立设计样例沿同一路径，不依赖Runtime或算法。
- 已执行构建：UI002零错误6581警告96.54秒，全量Release001零错误6621警告64.58秒；正式EXE探针PID57888正常退出。795发布文件与113加载记录SHA一致。
- 已执行测试：原结果视口10/0、DesignTime91/0、RuntimeEditor10/0、FullChain8/0、WorkflowHost1/0。GlobalBinding237/20、ImageSources174/2真实失败仍保日志，不认整链通过。
- 权威证据：`evidence/O06_RESULT_BINDING_RASTER_INTEGRATION_20261028.json`；原来源/早期候选记录 `evidence/O06_NATIVE_RASTER_LAYER_CANDIDATE_20261028.json`。Catalog记录当前依赖hash与实际范围，旧验证保其原基线。
- 未完成：工作流线集中修20+2失败；旧SendData页是否绕Host编辑/历史链需补齐。D07还缺Geometry/Text/Selection/ROI统一owner及原Shell主图生产接入、SDK主图退出。C09/C17、原阶段一其他项及真实VS/VM人工仍待，Blob业务冻结。
- 人工验收结果：本批无真实VS/VM或用户签收。91组独立组装不代VS；10组窗口自动交互不代人工外观。未跑本批全部署轨，不套上一批18项。
- 禁止改动：用户 `tools/SunEyeVision.BlobAnalysis/Wpf/Views/BlobParameterEditor.xaml` SHA256 `118128fb08ea3163b353620a91359b08b4d5cb1d36a58e37eaa9bf5c83da67ed`；不修改、不暂存。不开第二图像路径，不把两个自拥有viewport套到一原view，不加SDK视觉后备。
- 下一步：收工作流固定修复提交，集中合入和规定构建/关联新回归；随后提取既有Text投影及ROI attachment，由NativeResultViewport唯一合成ShapeList/ROIList，保原绘制/历史/取消及同Frame守卫。当前所有构建和测试已终态，无live。
