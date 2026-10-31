# VM 原范围控件：整批证据、实施与验收

状态：静态证据与整批方案；本方当前源码候选未经构建、运行或同状态验收。VM 是第一基准，未证行为不按本方猜测冻结。用户试用期间不重建、关闭或替换正式程序。

## 原件入口与依赖闭包

`Apps.XmlUI/styles/paramusercontroldatatemplate.xaml:576–603,821–845` 的整数/浮点范围组合分别使用 `VM.PcUI` 的 `SliderArrange` / `SliderArrangeFloat`，`SliderType=between`，两个 `CurValueA` 双向绑定 Start/End，独立绑定两端 `IsSubA` 和 `SubData`，并分别转发数值与类型变化命令。基础行高32；整数模板显式开启XML极值标志，浮点模板未显式开启，但浮点参数模型仍有完整范围检查。

完整依赖包括 `SliderArrange.xaml/.cs`、`SliderArrangeFloat.xaml/.cs`、`SliderArrangeBase.cs`、`SliderArrangeDictionary.xaml`、原开关样式、`NumericControl.xaml/.cs`、`RangeRule.cs`、箭头和公共资源，以及 `ButtonSelecter_sub`、`ValueBase/SubData`、绑定操作和 `Integer/Float/IntegerBettween/FloatBettween/ValueBettween`。必须按程序集区分同名原控件，Catalog保留原身份和资源SHA，产品命名按本方约定中立化。

原件根目录 `D:/MyWork/REA逆向测试/analysis/decompiled/`。关键身份：`VM.PcUI/HikPcUI.ListView/SliderArrangeBase.cs` SHA `CDCE54E608EEE5421A2863C700B28DDBE89242514EF44690B7B1EC83544C8FF2`；`SliderArrange.xaml.cs` `40E9DDC63A2E3CFD5B9670DD59E1FBEEA249590AD74C5B540418B8EAB17C8BE3`；`SliderArrangeFloat.xaml.cs` `88A03646B37396016244A42224773301B10F318620AAD5D114905CBEBDA7FF80`；`Apps.XmlUI/styles/paramusercontroldatatemplate.xaml` `96B98F06FBAF9BC2CF1F481B2A24323ECFC11831C05698E9FA5B18EC294E0F63`。

## 已证行为及未知

| 部分 | 原件证据 | 本方批次处理 |
| --- | --- | --- |
| 两端订阅 | Base:796–875 左右各自切换本端输入/订阅内容；任一端订阅时整数整体滑条隐藏，浮点还受 `NeedSlider` 控制。 | 保持真实原模板和两端状态；核对四种模式、选择、解绑、重开。 |
| 输入和滑条 | NumericControl文本源更新为失焦，箭头按Increment和边界钳制；滚轮仅聚焦且非只读。浮点按声明位数格式化，整数模型还做步长归一。 | 一次实现并核数值、箭头、滚轮、滑动、边界、精度、焦点、输入同步，不能把箭头步进当作直接输入行为的证据。 |
| 跨端规则 | Base:1032–1098、1184–1218 直接比较两端并夹值，无对端订阅判断；原文本边界绑定及 Integer/Float.Check 也使用另一端当前值。 | 已从本方原控件候选中撤回六处跳过夹值条件；Presenter对订阅端忽略旧数值的候选仍需随订阅回读整链核定。未构建或运行，不得称等价。 |
| 弹层关闭 | 两个原 `.xaml.cs` 的 Popup Closed 均 `ForceUpdateSource` 两端并发两端变化通知。 | 分别核模式切换、选源、数值失焦与滑条弹层关闭；不得将“任一操作都绝不处理另一端”归因于VM。 |
| 来源与回读 | ValueBase 切订阅初始化绑定表达式；选源操作调用 ParamSubManager，未直接写 CurValueA。GetParamUnitByZmq 存在模块读回链。 | 运行后/页面刷新何时回读、回读解析值或保留值仍未证；不猜测。 |
| 退出清理 | 本控件源码未见独立 Dispose/Unloaded；原宿主外层释放仍须追踪。 | 本方会话、弹层、绑定和旧回调由非视觉适配负责，确保与原可见交互一致并正确释放。 |

## 整批实施范围

1. 对照真实原件和状态，核本方原控件、模板、原依赖、资源、设计器与真实窗口组装，纠正无证据的原件行为变更；入口、属性、显示与回调按同一批次处理。
2. 在既有 `Plugin.Wpf` 字段/订阅/范围 Presenter 接入正式字段与引用，保持唯一编辑草稿；本方必要的拒绝、取消、重入、代次与资源释放列为宿主适配，不宣称为VM内部机制。
3. 统一处理完整捕获、宿主节点候选身份、合法与非法未提交文本、引用准入、取消/重开/撤销及图像源共享宿主影响；现有代码仍是候选。`VM_RANGE_DRAFT_CONTRACT_20261029.md` 仅供本方事务边界，不覆盖原件未知交互。
4. 完整检查已导入原文件与Catalog原身份，厂商名仅保留为取证元数据；名称变更须随完整依赖和真实消费者整批处理，不能新增旧名双轨。

## 集中同态验收与最小补证

- VM实机对 `[100,200]`：右端待选订阅、订阅值500、订阅值50分别操作左端300，记录键入、失焦、弹层关闭、运行前后和切回常量；左右镜像、双端订阅，整型浮点各覆盖。订阅何时回读和最终约束以观察为准。
- 双端分别有未提交原文后关闭滑条弹层，核两端模型/SDK、异常、输入原文与焦点；覆盖固定小数、整数步长、边界与非法输入。Enter失焦需结合原宿主或实机确认。
- 单端/双端及跨旧边界完整捕获，真实来源、动态/固定索引、无效绑定、宿主拒绝、重绑重入、取消、保存重开和释放；未确认的运行条件不代签。
- 独立真实视图的VS设计器与运行，正式同源Release的UI/全量构建及回归；对照VM同页面、同状态、同操作的样式、焦点、数值和行为。全部结果按实际记录，不把静态源码或旧PASS称为最终验收。
