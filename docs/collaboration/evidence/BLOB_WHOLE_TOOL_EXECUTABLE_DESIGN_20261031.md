# Blob 整工具统一实施设计（候选，2026-10-31）

基线：主线349ece68/8c43eb92；候选89024ebb产品与主线c59a5582一致。既有a3f79398及4/8未提交草稿仅并入本整工具批，不独立交付。原96直接资源/8共享组/39输入节点/95输出节点保留，不按本方能力缩清单。

## 证据与解释约束

原资源和整链证据采用主线BLOB_WHOLE_TOOL_VM_20261031、BLOB_WHOLE_TOOL_THREE_PAGE_MATRIX_CANDIDATE_20261031逐项索引，原件SHA在其中。XML声明证明名称、枚举、默认、类型、范围和触发器，不证明大函数内部比较、空集或处理次序。connectivity=4/8默认8及0x060B已证；CustomVisible=false的最终可见状态、原生像素邻接未证。现有高级布局保留为本方界面适配，不称原厂最终可见。原控件、模板和三页结构继续复用。

## 整体决策矩阵

|原项类别|正式能力与依据|本批行为|未实现/差异和保护|
|---|---|---|---|
|基本页图像来源|现有Image输入、正式来源声明、真实快照及生命周期|完整复用，保留身份/错型/失效拒绝|IMAGE专属CanSubscribeA/Visible资格基础仍缺，不扩Contracts|
|ROI/屏蔽/继承/多形状及位置三方式|Region引用与Affine2D可执行；公共Session/Editor/Viewport已具备|复用正式资源输入与公共控件，不创建假owner|本地定义/几何保存/同帧编辑租约/部分环物化/三方式转换缺基础；UI-only禁用并保留全窗保护|
|六阈值模式及参数显隐|原六模式结构已证；正式仅SingleThreshold|复用九参数、模式准入和五整数引用政策；原其他模式显示未实现|不开放其他模式；Single比较方向/等号/端点是本方明确假设，不称VM等价|
|填孔、其他高级筛选、轴比和内接矩形|原参数已归档；执行次序/特征语义未闭合|保持原位置、禁用及未实现说明|不按名称造算法，不把现有Bounds代原旋转矩形/轴|
|面积/圆度/矩形度筛选|现有真实HALCON筛选和数值准入|整链复用，禁用值仍检查绝对范围；原缩放/量化证据单列|不将原float缩放直接等同本方Float64筛选；最终比较未证|
|排序特征与方向|原枚举拆分及默认Area/降序已证；本方五特征真实输出已有|替换合并枚举为单一“特征×方向”模型，支持已有Area/Circularity/Rectangularity/CentroidX/Y；Perimeter有真实已消费特征纳入同一明确本方排序；稳定同值顺序、排序后限数|其他7项无可靠几何/单位，13项UI保留且不可选择；原原生排序次序/同值规则未证，本方策略显式记录；不留旧合并模型或别名|
|4/8连通|原枚举/默认/托管访问已证|同一设置冻结后给线程局部HALCON作用域；非法枚举拒绝，异常取消恢复|HALCON4/8为明确本方适配，不称VM像素等价，不改全局neighborhood/max_connection|
|原二值输出开关|原默认True；本方BinaryImage真实资源链已存在|正式字段接回同一草稿，默认按原声明True；移除重复UI-only|本方有效域外黑/全图Domain为现行策略，原像素/Domain未证；不能称VM等价|
|原Blob图像/轮廓三态|原Blob绘制不同于GrayImage已证|保留原Blob图像和三态轮廓禁用占位；正式灰度名称及来源保持真实|不生成原BlobImage假源；有序/无序语义未证，不把Boundary布尔当三态|
|结果判定42原条目|本方12种判定有真实消费，但原空集/逐项聚合/端点未闭合|不凭字段名开放原判定；明确原组合未实现并禁用，不将output-not-empty取反冒充OKWhenNumIsZero；本方现有正式非UI配置/运行合同继续保留并测试|结果页旧临时控件不再允许伪编辑；原全部判定逐项保留，现有额外6判定不是VM映射，不伪装同页已接通|
|图文9组与文本设置|公共原控件已迁；Blob同次owner/冻结/图层消费者缺|原组合保留明确未实现/禁用，不造只保存无消费配置|不扩图文基础，不把公共样例过称Blob接通|
|九项原结果表、图像/区域|六真实特征/两派生图/Region/element-id+Stamp已有|复用真实publication/高亮/两轮退役/租约；排序使用同一最终数组|Angle/LongAxis/ShortAxis仍显式未实现且不可订阅；原始上游坐标域proof缺失继续拒绝|
|执行底栏/关闭|正式单次调试、取消owner和关闭退役已有|复用生产调度及关闭，不新增UI循环|连续调试会话基础缺失；不解除整窗保护|

## 单一生产调用链与文件边界

`BlobParameters.xml→BlobSettings/BlobParameterDefinition→ToolRegistry/BlobEditorFactory→正式ToolSettingsDraft→RuntimeNodeConfigurationControl→NodeConfigurationEditHost事务→RuntimeToolConfiguration→SolutionSnapshot/Repository→WorkflowPublishedConfiguration/WorkflowRunConfiguration→WorkflowBindingContext/RuntimeDebugInvocation→ToolRuntimeExecutor→BlobSession→BlobOutput/BlobSchema→DeclaredOutputSnapshot→BlobResultView→MainWindow publication→ResultWorkspacePresenter`。

复用所有公共机制，不修改Contracts/Workflow/UI主窗、工作流独立工作区或用户文件。产品仅修改Blob Runtime Parameters/Resources/Algorithms、BlobRunUi与BlobDisplayUi；必要显示适配只消费已有DeclaredChoiceDisplayOptions，不新增接口。移除旧合并排序模型及重复二值/排序UI-only，不保留兼容入口；所有现行测试消费者同步。

## 一次验收矩阵

1. 原96项与8共享组逐类保持索引，真实字段只接已有消费；未实现不能写入草稿或出现在来源树。三页原模板、六模式、不可选项、基本/高级状态和全窗保护分别验证。
2. 默认与枚举：单阈值/原九参数、Area降序、4/8默认8、Binary默认True；非法枚举/未就绪模式/错型/越权不半写。
3. 同一正式编辑会话：组编辑、确认/取消、已有绑定当前值与顺序、冻结后修改、复制、撤销重做、保存重开；重新加载无旧兼容模型，运行消费新字段。
4. 真HALCON：对角4/8及调用者4/8恢复、安装后异常/取消、全局截断拒绝；六特征×升降/不排序、同值稳定、排序后限数；像素掩膜/非矩形Domain/ROI引用/双孔/NG/空结果继续复用。
5. 工作流与调试冻结同源设置，正式二值输出开关和下游强制请求独立；结果表/数组/element-id/图像坐标/Stamp保持同次，重跑旧选择拒绝、失败清理及最后读者释放。
6. 同候选源码UI与全量构建；RuntimeAlgorithm、RuntimeKernel、RuntimeWorkflow、BlobParameterProduction、ParameterValues、BlobResultWorkspace、必要FieldEditing/RuntimeEditor/独立设计轨串行；仅新失败定位补测，不用数量代替整工具结论。

## 未验及交付界限

本批为整工具统一迁移当前可实现范围；上述未实现原项仍在原清单，不能称完整VM等价或全部Blob功能完成。候选与正式回执分开，加载程序集哈希锁同版。实际VS设计器、其他DPI、原机差分与人工验收另记。原SDK不启动；GUI先检查用户进程并协调，不干扰其他工作区。
