# 框架公共数据契约 v0.2（main；v0.2数据接口）

日期：2026-10-16。范围：F-01 的整数/数值/文本数组基础，F-02 的调试命令入口审计。不是原生 Group 循环或 SDK ABI 的完成声明。

## 已核对的原始代码

| 证据 | 确认内容 | 仍不能确认 |
|---|---|---|
| analysis/decompiled/VM.Core/VM.Core/DynamicOutput.cs：GetOutputInt/Float/String | 按 nParamType 1/2/3查询；用 nValueNum 复制 int[]/float[]/StringData[]；无结果可能返回默认结构 | 数组是否来自循环、累积/展平/中断次序、执行引擎如何生产结果 |
| analysis/decompiled/IMVSGroup.WorkflowAudit/IMVSGroupCs/GroupResult.cs | Group公开整数/浮点/字符串数组读取接口，并转发 DynamicOutput | 完整原生调度实现 |
| analysis/decompiled/DynamicObjectConfigControl/DynamicObjectConfigControl/VmIODecorate.cs：IsAccumulatedOutput/ParamValueChanged | nMode=0累积、1普通；复合IO遍历叶子，最多1024项；SetGroupIOReportMode写入SDK | 每轮边界、错误/中断时的结果保留及清空策略 |
| analysis/decompiled/DynamicObjectConfigControl/DynamicObjectConfigControl/DynamicParamsTabControl.xaml.cs：ContinuousExecuteBottom/StopExecuteBottom/ExecuteOnceBottom | 分别绑定 ContinuousSelfExcuteModule、StopSelfExcuteModule、SelfExcuteModule；Dispose清空ImageDecorate | 命令名称不等于已恢复其完整调度语义；不能断言是否隐式计算上游或如何等待停止 |

## v0.2实际接口

在原有基础类型上增加 ValueCardinality.Scalar / Array：

- PortDescriptor.Cardinality、WorkflowInterfaceItem.Cardinality、WorkflowCallPort.Cardinality，默认均为Scalar，原有构造函数调用保持有效。
- PortValue.Cardinality随值传播；IntegerArray(IEnumerable<long>)、NumberArray(IEnumerable<double>)、TextArray(IEnumerable<string>)复制并冻结输入集合。
- 使用 value.As<IReadOnlyList<long>>() / double / string读取；数组不能As<long>()，也不会自动取第一项。
- 空数组是有效、已产生的输出；缺失输出是没有键；null数据/字符串元素、非有限数值被拒绝。
- 基础类型相同但基数不同，连线/订阅/工作流参数仍判为不匹配；参数及局部变量保持标量。原有Components列表是复合值，不能误当作本次基础类型数组。
- 注册阶段拒绝当前未支持的图像/几何数组、未知基数。属于宿主支持范围限制，不是断言VM不支持这些数据。
- Group接口快照、签名检查、配置保存重开和上下游执行都保留Cardinality；主窗口输出格式显示数组元素。

示例（插件线可采用，需先取得framework提交）：

```csharp
var output = new PortDescriptor("values", DataKind.Integer) {
    Cardinality = ValueCardinality.Array
};
var values = PortValue.IntegerArray(new long[] { 2, 4, 6 });
var items = values.As<IReadOnlyList<long>>();
```

这是一份独立托管运行契约。VM的int/float原生数组是32位，当前独立Integer/Number沿用long/double；本次没有实现二进制结构、指针、编码/精度或SDK的互操作等价，也没有把本项目Cardinality枚举解释为原生字段。空数组做托管规范化，不保留原生空指针布局。

## 兼容与集成

- 旧 .vmx.json 没有Cardinality时按Scalar读取。新数组声明需要v0.2读者；不能假设旧严格JSON读取器能读取新字段。
- 原有 IVisionToolPlugin / IWpfToolEditorFactory 及ToolEditorContext构造参数保持；后续v0.3在Context新增可空Session，不要求插件线重写在途插件。
- 现有类型选择器还未完整提供数组形态选择/原生参数页交互；不能把运行层支持当成完整UI完成。
- Accumulate配置标志仍没有执行逻辑；本次数组传播不等于循环累积输出。
- 不自动拉取/合并插件分支，也不修改插件worktree。框架线记录契约与提交，插件交付时再集成。

## F-02后续实施

WPF普通模块宿主已接入独立调试会话；原有构造函数和Run委托保留，新增可空Session。单次/连续/停止及关闭等待的证据、缓存输入策略和未恢复原生边界见 [DEBUG-SESSION-CONTRACT.md](DEBUG-SESSION-CONTRACT.md)。Group调试入口、设备释放及全部插件UI仍未完成。

## 验证

tests/VisionFramework.ArrayChecks：真实独立执行器运行数组产生→求和，订阅链、所属Group输入输出→下游求和结果12，保存重开重跑、空数组与错误形态；这些是测试模块，不是已经逆向出的VM算法插件。
本地详细结果：analysis/full-audit/array-contract-runtime/results.json；源码哈希：analysis/full-audit/array-contract-source-files.json。测试总数和兼容回归记录见对应日志及交付记录。
