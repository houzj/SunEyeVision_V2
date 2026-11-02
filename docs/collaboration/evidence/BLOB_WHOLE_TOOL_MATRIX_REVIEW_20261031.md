# Blob整工具逐项矩阵补充审查

本记录仅为统一实施设计输入，不批准逐参数实施，不代表整工具或原厂等价验收。主仓产品基线c59a5582；候选仅修改证据矩阵与本记录。

矩阵保留96直接项（基本8、运行46、结果42）及8共享组。共享组递归展开54个带Name结构成员，其中包括组/类别等结构节点，不是54个独立业务字段。每项及每成员均补证据确定程度、现有消费/保护、整批决定、测试入口ID和缺失验收；unknown不计作对应完成。

## 生产接线审查

- 正式OutputBinaryImage与SortMode已有模型和算法，但BlobRunUi隐藏正式行并替换成禁用UI-only项；是本批接线候选，须先统一原语义，不将底层字段存在算页面完成。
- OutputGrayImage不是原BolbImageEnable连通域绘制图；OutputBoundary布尔不是原BlobContourType三态。不能沿相近名称接回原行。
- BlobDisplayUi隐藏全部正式结果判定，换成NativeJudgmentUiDraftControls。真实计数、总/逐体面积、圆度和矩形度是可复用值；原聚合、空集、启停、默认和端点不因此已证。平均值等本方扩展单列。
- 九数值及模式显示已有正式链，原Double/None旧配置可显示保值但不能新确认执行。三种额外模式仅展示；软阈值整组不因已有数值控件就具备算法。
- ROI/位置/图文共享控件独立过测，不等于Blob正式owner、保存冻结、资源物化及同次消费完整。整窗保护保持。

## 测试入口解释

矩阵acceptanceEntryCatalog记录实际Runner的Stage/Track、源码和范围。入口ID是已有可复用轨道，不是新增功能已经有断言，也不是本轮执行回执。missingAcceptance逐项列需要补的真实整链测试。Candidate运行须使用本候选构建产物；Formal验收由主线统一执行。

范围关系：BlobParameterProduction验证现九字段，BlobRangeLayout验证已有范围，NumericIndexProduction验证显式引用政策，ImageInputSelection及CrossDocument验证现图像选择器，BlobResultWorkspace验证现同次publication；均不能覆盖新增原参数或证明VM语义。WindowPresentation是公共控件组装，不替代真实Blob接入；DesignTime自动轨不等于实际VS设计器。

## 仍不能由静态资源确定

CustomVisible/触发操作不能单独证明最终运行可见性；父组、初始化与刷新顺序须区分。CurValue、子项DefaultValue、父组DefaultValue分别保留；不推断加载既有配置后的终态。隐藏项保值、注入、执行及持久化不能仅由UI声明推断。

原阈值相等及像素边界、量化后比较、筛选排序限数/填孔顺序、空集聚合、Blob绘制图像素/Domain、角度轴长及原生内存所有权未由本矩阵证明。原引用资格和动态多值消费、偏好保存/Dirty也不能外推。未知需要具体证据或明确自研假设边界，不以矩阵条目完整凑数。

## 本轮验证

仅JSON结构检查：96项、8组、54结构成员均有新增分类与入口字段；未构建、未运行GUI/VM。未修改产品和主仓文件。主实施线应继续将每个交付候选落实为精确正式字段、冻结/执行/结果消费者和新增测试断言；本记录不替代该决定。
