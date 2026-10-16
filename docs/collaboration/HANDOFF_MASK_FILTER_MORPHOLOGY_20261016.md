# 剩余屏蔽阈值与形态学修复交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm；当前执行者：Codex，单写入；保留并存修改，无提交推送。
- 任务目标：修复滤波灰度类型回归，补齐屏蔽和阈值缺陷，再推进形态学；保留绑定、输出及屏蔽，标定不动。
- 允许修改目录：HalAdapter、ImageFilter、ImageThreshold、Morphology及本地协作文档。
- 已修改文件：HalconLinearFilters.cs、HalconThresholdOperations.cs、ToolRegionHelper.cs、ImageFilterTool.cs、ImageThresholdTool.cs、MorphologyTool.cs、本地全算法方案及CURRENT_TASK.md。
- 已完成：卷积输入转real工作类型，计算后恢复原类型，无8位归一化；支持byte/int1/int2/uint2/int4/real，int4超过±16777216明确拒绝。新增实际输入类型和卷积上下文诊断；原始错误未包含类型，不把适配缺陷推断当作已确认的实际输入类型。
- 屏蔽：部分扇环使用原生GenCircleSector减内圆，屏幕角度[start,end]转[-end,-start]，整圆仍GenCircle；测量使用SDK亚像素包含。保留本地/订阅/拼装运行时解析、组级活动输入和已有圆环剥转，不改SDK契约或校准算法。
- 阈值：基础模式使用原生min/max开放端点，灰度输出底图保持源类型；参数不缩放。Triangle保留兼容模式，修正只使用右尾及距离公式，限定byte并明确拒绝隐式位深压缩；常量和空直方图有退化分支，不替换成Otsu。
- 形态学：十字由完整横纵条带并集构成；椭圆使用独立长短轴，单像素轴为线/点；结构元素非负坐标，定位遵循原生质心规则。所有操作使用Iterations，开闭为先n次再n次的原生腐蚀/膨胀组合，梯度及顶帽/黑帽用原生区域差集；最终与有效域相交后统计和输出。原固定128二值化及全部字段不变。
- 兼容影响：输出类型契约、路径及枚举不改，灰度量化、Triangle阈值及核形状/迭代纠错可能改变数值；保留参数不意味着保留错误结果。屏蔽限制输出域，不承诺邻域不读域外像素；栅格和亚像素边界存在表示差异。
- 未完成：BlobAnalysis、ShapeMatch、Triangle原生等价替换、任意卷积核编辑、独立标定；原图滤波错误和扇环/形态学效果待人工验收。
- 已执行构建与测试：首轮全量Release1155警告3错误（108.75秒），HRegion构造歧义已改显式double；最终全量Release855警告0错误（85.60秒），独立UI Release1065警告0错误（20.55秒）。HalAdapter/SDK/UI和ImageFilter/ImageThreshold/Threshold/Morphology主DLL均与Release编译中间DLL哈希一致。发现PointLineMeasure目录残留旧HalAdapter，按现有插件构建规则单独Release刷新（0警告0错误，4.42秒）后哈希一致；加载器已优先宿主，未修改加载或发布规则。diff检查通过，仅既有换行提示；标定源码无diff。未新增/运行测试，未启动程序/VS设计器。
- 人工验收结果：暂无；未操作用户方案或设备。
- 已知风险或禁止改动：不得暗降阈值、增加隐藏支持率门槛或扩大屏蔽；不修改标定数据、源码、绑定与调度。real恢复整数类型存在原生量化及饱和截断；不支持的源类型明确失败。
- 下一步：继续BlobAnalysis/ShapeMatch；人工确认原图滤波、部分扇环跨零角及屏蔽后形态学输出。发布程序位于Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe。
