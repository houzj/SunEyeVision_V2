# 显示原件族中立命名整体设计与并行边界

源码基线fd94c9841（默认原批量产品c418fe5fb，六轨候选自测已通过）。本设计为同显示单元的最终产品命名整改，不改变原件行为，不扩大到全Numeric/Input库，不等待或中断工作流独立线。

## 原因与范围

用户明确产品目录、命名空间、类型及资源文件按SunEyeVision职责中立命名；原程序集/类型/MVID/源码资源SHA与许可留Catalog和证据。显示族尚有VMBaseControls、VMControls、HikVision.Styles和HikMessageBox实际产品名称，不能称已完成该约束。只读文本扫描上界709个文件，含通用using；Controls两显示族410文件（371个cs）、对应主题101xaml、资源41文件、直接RenderInterface定义101cs，不是709项行为重写。

## 固定方向与完整闭包

- Native.Display.VMBaseControls → Native.Display.ImageView；对应Controls、Themes、Resources目录同改ImageView，保持族独立，不按类名合并其他原件。
- Native.Display.VMControls → Native.Display.Primitives；相应目录同改Primitives，保留通用控件、PropertyGrid、转换器各原子族。
- Native.Dependencies.VMControls.RenderInterface → Native.Display.Contracts；迁的是原图形/ROI接口族，不改Plugin.Contracts算法与参数模型。
- 本显示族实际Hik/VM品牌类型与文件一并去品牌：HikMessageBox可命名NativeMessageBox，HikVision.Styles按本族Styles职责命名；其余需先列精确实际定义与消费者，不对其他Numeric家族同名类型作全局误替换。
- 同步C#、XAML x:Class/clr-namespace/类型及品牌别名、pack URI、resx ResourceManager基名、NativeResources.props LogicalName、嵌入光标与图片路径、真实UI/数值消费者及测试。资源字节不改；API/契约行为、布局、交互、算法、字段和方案格式均不改；不留旧名称兼容桥或别名。
- 原件出处/许可、原程序集/类型/MVID/原始哈希、历史回执和原始source字段保留。Catalog及原迁移/资源清单由主线唯一维护；实施线只交精确旧→新路径/产品类型/资源名及新目标SHA映射，不能粗暴重写历史来源。

## 唯一写权与协作

新实施线在一个独立临时worktree负责该命名闭包的产品、资源和实际消费者，以及仅必要的测试引用迁名；不得改业务行为、Catalog、原O06、CURRENT_TASK主工作区或其他分支。本线已冻结默认批量产品，主线只做正式集成验证和文档，不与实施线同时修改这些产品文件。确遇功能修复由主线给明确问题后由当前唯一产品写者处理，不能各改一份。

工作流另负责文档唯一事务/原件编辑窗口、严格V3、输入及资源合同、后续WorkflowNodeBase旧Parameters/ParamSettings退出。本批只迁显示命名，保留RuntimeConfiguration/ToolSchema/TypeRef合同，不恢复旧模型。其独立分支使用现有名字继续开发，集中集成时按精确映射统一新消费者，不要求日常停工/反复同步。

## 验证与交付

自建worktree源码真实restore/build，HALCON沿用-p:HalconSourceRoot=D:/MyWork/SunEyeVision_V2/libs/halcon，禁止复制现成Release或运行库冒充构建。候选UI及全量Release，真实WPF视口33、显示44、十字7、工作区4、B08整窗12、ROI预览24相称回归；代码与资源依赖闭包、新URI/光标、被迁名资源字节、无产品旧入口分别检查。候选与主仓正式回执分开；实际VS及人工仍另记。提交中文，交精确提交、构建日志、实际加载SHA、未验项和中立映射JSON；主线维护Catalog并集中正式验收后才报告本批完成。