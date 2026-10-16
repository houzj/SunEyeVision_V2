# 屏蔽阈值与滤波修复交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm；当前执行者Codex，单写入，无提交推送。
- 任务目标：先修屏蔽运行时解析及坐标，再纠正阈值和滤波行为，保留绑定、输出字段和既有功能，标定不动。
- 允许修改目录：HalAdapter、LineDetect/CircleDetect、Threshold/ImageThreshold/ImageFilter及其必要UI和协作文档。
- 已修改文件：ToolRegionHelper.cs；新增HalconThresholdOperations.cs、HalconLinearFilters.cs；两个查找工具的Tool.cs；两个阈值Tool.cs及ThresholdParameters/调试XAML；ImageFilterTool/调试XAML；本地全算法方案与CURRENT_TASK.md。
- 已完成：测量屏蔽本地/订阅/拼装统一运行时解析，保持组级输入与上游坐标归属；本地修正一次，订阅不二次修正，输出屏蔽几何为本轮修正后坐标。原三参数API保留，新增上下文重载；点过滤仍保持亚像素几何判定。图像处理域新增完整圆环差集，未支持类型显式失败。
- 阈值：共享有效域反转，BinaryInv修正，MaxValue输出亮值与0/255选区掩码分离；截断/置零输出真实灰度。自适应为DynThreshold比较原图和局部图，偏移-C；Threshold新增默认关闭UseAdaptive及AdaptiveOffset，已有Type/AdaptiveMethod/BlockSize等字段保留。局部ThresholdUsed表示偏移，不是全局灰度阈值。Triangle仅修直方图定义域，Otsu保留dark方向。
- 滤波：HALCON卷积核元组含核尺寸和归一化权重；均值/高斯核宽高及Sigma真实生效，自动Sigma规则为max(0.5,(尺寸-1)/6)，属于明确初始化约定。中值用原生矩形核且补高度UI。固定锐化仍3×3，界面不再虚称可编辑任意卷积核。边界Reflect/Replicate/Constant0/Wrap分别映射mirrored/continued/0/cyclic，其余明确报错；双边只使用原生Sigma控制，旧直径隐藏且不改存储，结果核宽高0表示自动。
- 屏蔽语义：邻域计算保留完整像素上下文，输出/比较/统计限制有效域；不涂黑屏蔽像素，不保证邻域完全不读区外像素。点屏蔽与栅格区域边界存在亚像素/像素表示差异，未擅自更换原有点包含口径。
- 输出兼容：工具/节点ID、原枚举数值、绑定字段、图像接口和数组形状不改；部分错误数值行为纠正后不可能逐像素保持旧结果。BinaryMask保持0/255阈值选区，灰度模式OutputImage保留灰度，选区统计仍按原图选区计算。空选区统计返回0，避免NaN。
- 未完成：部分扇环统一支持、Triangle兼容算法替换、任意卷积核编辑、第三批及独立标定。不宣称这些已全部标准化。
- 已执行构建与测试：首次全量Release0错误1162警告；最终曾发现AreaCenter重载歧义，显式声明HTuple后全量0错误801警告。保留旧API后的最终全量Release0错误858警告（56.70秒），独立UI Release0错误1065警告（17.23秒）；五插件及SDK/HalAdapter/UI发布DLL哈希均与编译中间DLL一致，diff检查通过（仅既有换行提示），标定源码无diff。未新增/运行测试、未启动程序/VS设计器，遵前序限制。
- 人工验收结果：暂无；未操作真实方案或设备。建议重点核对非零平移旋转下的本地/订阅屏蔽、反转后屏蔽不复活、MaxValue非255、灰度模式、C与算子偏移-C的符号、高斯非方核及非零Sigma、矩形中值和固定锐化。
- 已知风险或禁止改动：不得降低卡尺阈值、恢复隐藏支持率、把ReduceDomain当Metrology屏蔽；保留工作目录其他修改；标定源码与用户参数不改。
- 下一步：原图人工验收；按剩余能力边界继续专项，再进入第三批，不擅自扩展标定。

## 原生接口依据

- https://www.mvtec.com/doc/halcon/2405/en/dyn_threshold.html
- https://www.mvtec.com/doc/halcon/2405/en/convol_image.html
- https://www.mvtec.com/doc/halcon/2405/en/median_rect.html
- https://www.mvtec.com/doc/halcon/2405/en/smooth_image.html
