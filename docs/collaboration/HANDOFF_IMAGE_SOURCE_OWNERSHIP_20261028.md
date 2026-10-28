# 图像源在途候选与独立分工交接

- 基线提交：主线72d29d1b，合并工作流ee086776候选；工作流另有纯设计3b88c79c（含7b8bb461）待共同纳入。包含本单的中文提交作为候选交接基线，最终提交号随交接消息发送。
- 当前分支：main；接收方codex/workflow-toolbox-dataflow，独立worktree 87f4。
- 当前执行者：主线Codex唯一产品写者；交接确认后工作流线在独立worktree写图像源整链，主线回到Plugin.Wpf通用控件及Blob原两阶段。
- 任务目标：依据用户已明确的独立分工及本次优先级讨论，集中图像源业务基础和周边开发，减少跨线反复修补。本单是未完整验收候选交接，不代表D1、控件库第一阶段或S4完成。

## 优先级与职责

1. P0工作流线收口本次在途图像源基础链：权威配置、正式历史、三游标、明确选择、取消、保存重开及资源版本；先集中设计和验收，不在修复过程中扩展设备平台或完整结果架构。
2. P1两线并行：主线完成Blob必需通用控件及完整组合；工作流线完成公共图像源条与主界面/工具调试的同配置接入。Blob输入源选择仅订阅上游标准图像输出，不承担相机管理或取图工具调试。
3. P2主线在控件准入后完成Blob原型整链；工作流线沿已有设备服务核实际能力，完成已有可用设备与图像源调试/正式执行一致的真实闭环。
4. P3按真实需求补更多厂商驱动及周边体验；无关框架重构、全库迁移不作为当前两条原型的前置条件。多厂商边界现在明确，具体厂商能力须实际证据，不推断VM厂商支持范围。

驱动封装发现、连接、触发、采集、像素格式、断线和释放；相机管理拥有设备身份/能力与统一服务；图像源Runtime按冻结配置引用设备或读本地文件；Wpf只编辑、展示、试采集及导航；执行层拥有调度、运行游标、取消停止。正式编辑入口和采集连接保持唯一，不因界面入口不同建第二套配置或连接。

## 写入边界

- 工作流线：图像源/相机相关UI、ImageInputSource/ImageInfo、缩略图资产服务、ImageResult/旧ImagePreview消费者、图像源工具及调试、现有设备适配和本次Workflow执行/编辑历史消费者。仅沿真实需要改共享框架，不扩无关Group/通讯或循环语义。
- 新公共源条仍归Plugin.Wpf，可在独立分支新增中立专用目录：Controls/Native/Composition/ImageSources、Themes/Native/Composition/ImageSources、Themes/Assets/ImageSources、Adapters/Input/ImageSources、Presenters/Input/ImageSources、Design/ImageSources。不复制完整原厂业务服务，不依赖UI宿主/Workflow/SDK可视控件。
- 主线：现有通用数值/范围/开关/枚举、输入来源选择/订阅、ROI、颜色、C13文本/图形样式、C14公共结果合同与原图像帧/唯一ShapeList场景、后续Blob重组。
- Catalog/controls.json、既有共享主题/资源入口、Plugin.Wpf项目及公共合同由主线统一；工作流线提供必要条目与证据变更，由主线核收。共享API先冻结签名和实际消费者，再各自成片实施、共同基线集中集成。主线交接后不再直接修改上述图像源UI/执行业务文件；相交消费者由工作流线改。
- 正式Release目录只有主仓一套。两线可独立改源码，但构建部署/运行窗口必须互斥；工作流分支不得向主仓Release写入旧基线产物。

## 本次候选实际修改

- 完整ee086776图像源正式编辑/配置持久化/执行快照与资产缓存候选。
- 修局部变量遮蔽、设置页私有会话访问；ImageSourceConfig不再当结构签名，结构历史重建深复制历史源；正式SolutionSession.History回放回归。
- 每root/source复位epoch进入执行定义，旧Complete不能覆盖复位，其他来源可继续；物理资源基目录冻结，成功保存后逐项纯地址复核并退役旧投影。
- 非结构编辑不清空重建同一集合；formal起点、display及执行游标分离。ImageSourceEditProposal(Configuration,SelectStart)替换旧writer，明确选择才reset；Commit/Load按对象或reference重复序号重映射展示。
- 预览索引OneWay，明确左键/键盘请求携不可变source/version/item/index；正式接纳前后校验方案/root/tab/node/source/item，拒绝恢复正式展示；执行链固定expected tab/session及来源版本，通知、日志、状态后重新核验，不临时改读另一SelectedTab，不再靠PublishRuntimeInput旁路假定选图已进入正式plan。
- 同值source配置仍按未修改的工具参数判断，不能因为一次明确重选就要求伪造ImageLoad运行路径；真实参数变更继续校验。

## 已执行与未执行

- 构建/失败全保留于artifacts/image-source-baseline-*。UI001/002编译失败已修；UI003—008及Release001—004均为不同中间源码的零错误记录，不能套当前候选。当前源码UI009为0错误、6538警告、103.80秒，进程已终止；尚无当前源码全量Release及测试helper/新合同回归。
- ImageSources001=40过2败；002=42过；003=42过1败，已修Replace明确选择后未在最终新合同产物重跑。原失败未删除，未降低非法选择断言。
- WorkflowHost首轮页签溢出断言失败；诊断复跑原行为通过，测试增加有界原生尺寸/Arrange等待。实际图像源/设置/保存A-B检查已在该中间产物走过，仍须新合同重跑。
- 新合同新增的标量/展示/执行cursor对照、明确同值选择/接纳通知换source、实际预览请求用例，以及最新全量Release/10轨/EXE、同源SHA核对尚未完成。新测试helper需重新编译，不可加载旧helper签名。
- 真实VS、VM同状态外观、真实用户、多厂商硬件、相机断线/重连等没有本候选验收记录，不代签、不以模拟取代。
- 本次所有源变更及用户XAML的SHA另存evidence/O06_IMAGE_SOURCE_HANDOFF_SOURCE_20261028.json；该证据是源码候选审计，不是正式发布同源通过证明。

## 下一步

接收方先对新合同做整链审查，核明确选图的所有入口、非选择保展示/执行位置、旧回调和通知重入；完成规定UI/全量构建及ImageSources/真实WorkflowHost/关联轨/正式EXE后登记同源证据。基础链稳定后再按完整设计推进公共源条和相机工具调试，不每发现一处就扩大公共框架。主线从同一候选基线恢复C13；Blob业务继续冻结至第一阶段准入。

用户BlobParameterEditor.xaml页索引2及SHA118128fb08ea3163b353620a91359b08b4d5cb1d36a58e37eaa9bf5c83da67ed保持原样，不纳提交、不推送。旧失败、原始逆向证据及7b8bb461设计文档必须保留。交接后不以候选提交冒充验收完成。
