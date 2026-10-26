# 当前任务：落实O06 Blob与原31类控件方案，交付真实Release

## 写入与版本

- 当前主对话是主工作树唯一产品写者；不恢复旧角色、批次或副本。
- 远端历史可运行检查点为 `b87fb685`；其通过成绩不适用于当前迁移候选。
- 本地已保存 `602cf8a1`（源码整合/发布路径）及 `8a82648b`（命令/语言/ROI基类/原色板消费）。本轮增量为就地样式弹层和数值交互；最后保存点以当前Git HEAD为准，构建日志按基线加增量记录。
- 用户明确要求：构建通过前只保留本地，不推送主仓。不重问或重试此前被拒绝的推送。

## 本轮实际代码

- `DisplayObjectField`由独立子modal改为就地弹层，同一DeclaredFieldEditor实例直接写调用方窗口草稿；外层ProductionEditorContext/窗口确认或Discard承担保存撤销。
- 主区按原ImageViewConfig_subContent承接260宽、102列起点、四行、原渐变及-90横偏移。已有十字大小/线型收在更多，不宣称VM原面板有这两个扩展。
- 父层不依赖单一鼠标捕获；按Popup.PlacementTarget/TemplatedParent归属处理外部点击。逐层Escape、卸载/窗口失活关闭、输入监听和窗口事件解除已有代码，尚未真实运行验证。
- `ColorField`消费VmColorPicker组合原70色色板、原默认/更多模板与高级色谱。选色写外层草稿，无独立确定/取消；Escape先收子色板并回焦，下一次交给父层。源scRGB公式保留，重开基色同步/冻结渐变Clone为明确适配。
- `NumericUpDown`有效键入即时同步Value/滑条且保留编辑文本；有效Enter确认后失焦收起，非法Enter保留。仅控件内聚焦允许滚轮改值；初始显示与用户改值分开，DeclaredFieldEditor拦截数值校验失败并让外层验证看到待校验错误。
- 原31行来源/职责/去留和本轮4项实际文件集中更新在 `evidence/O06_CONTROL_REORGANIZATION_20261026.json`，原方案/预览共用；原目标文件和退出门保留，不把已有实现改名当作架构完成。

## 验证事实

- 最新全量：`dotnet build SunEyeVision.slnx -c Release --no-restore`，日志 `Release/_validation/O06-RoiCore-20261026/build-vm-library-integration-027.log`：9错误、132警告、27.96秒。当前候选未编译通过，未可试用；正式目录已有部分基础工程新输出，不当作历史b87的完整二进制快照。
- 6个当前C#文件使用可用Roslyn 5.0解析无语法错误，记录 `popup-numeric-syntax-check-003.json`；不是完整语义编译或运行验收。额外加载SDK解析器曾绑定冲突，未计通过。
- 现有O06WindowPresentationCases已补同实例草稿、嵌套逐层Escape、数值同步/Enter、卸载关闭及真实ProductionEditorContext颜色不污染正式节点/Discard检查；本候选尚未执行这些用例。
- 预览资源按.gitattributes统一LF，SHA版本链接和连续再生稳定性检查通过；不是渲染/视觉验证。
- 原HTML已含数值双向同步、数量滑条、Enter失焦、就地样式/二层色板及内侧焦点提示；浏览器file协议被策略拒绝，未绕过，不称浏览器视觉通过。
- 基础三工程Release路径及Tool.Kernels解决方案归属已修；实际日志确认正式Release路径。新候选不复用旧构建/回归PASS。所有后续运行/验收基于D盘Release。

- 本轮导入器纠正C#/XAML及依赖准入闭包；50份源码保留为.pending，正文与HEAD逐份核对一致，证据composition-source-preservation-001.json。这些仍是迁移待办，不计作已完成功能。PolygonPoint保留原坐标/通知正文，脱离未使用的厂商XML基类；运行未验。
- 027剩余诊断集中图像视图接口与转换。三处ROI日志已接Trace；无引用的旧XAML CLR命名空间已清理，整合器重跑0变更。不得用空接口或旧全局模型凑编译。

- 静态核对：实际Byte2ImageSource在Apps.BaseData，不是旧VM.PcUI版本；IImageViewAddition经3D扩展涉及IVmModule/IVmIO，Apps.Data.ImageData涉及厂商XML/VisionDesigner。继续做有限视觉/图像缓冲适配，未复制厂商DLL、Ioc或工作流模型。6份适配哈希/169份已跟踪变更XAML解析通过，source-integration-check-027.json；不是运行验收。

## 下一步与未完成

- 根继续处理厂商视觉/参数依赖与源码-XAML配对，不能制造旧工作流/变量模型或空接口来凑编译。
- 图形透明度/粗细的声明范围、步长和存量值兼容仍未完整按VM源对齐；须保真承接，不悄悄截断保存值。
- 完整typed调试同run、原31项提取/重组及旧SDK消费者退出、VM同状态截图、VS设计时、真实运行/关闭释放与用户人工验收均未完成。旧类需消费者/资源/新合同承接及回归通过后再删。
- HALCON沿用加密狗，不单独检查许可；不恢复旧变量收口。构建通过后先核发布依赖，再运行组件/Blob完整链及关联回归，集中交付正式Release供用户试用，不代用户通过。

此前详细记录保留于Git检查点及 `history/CURRENT_TASK_before_four_lane_import_20261026.md`；原方案是统一实施/验证入口。
