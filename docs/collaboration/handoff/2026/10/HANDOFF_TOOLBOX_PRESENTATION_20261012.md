# 工具箱分类排序与矢量图标交接

- 基线提交：a0f92a8732f31a2ffe20e10fcc6feb7c792a093f。
- 当前分支：feature/comm。
- 当前执行者：Codex，唯一写入者；保留前阶段未提交实现和文档。本轮未发生跨AI交接，未提交或推送。
- 任务目标：按用户确认的9类、明确顺序及一致线框图标优化工具箱、画布和标准配置标题；更新通信项目Release。
- 允许修改目录：本通信工作区SDK／插件展示元数据、Plugin.Infrastructure分类排序、相关UI／测试和文档，不改主项目工作区或工具算法。
- 已修改文件：SDK Core/ToolCategories.cs、ToolAttribute.cs、Metadata/ToolMetadata.cs、UI/Controls/Icons/ToolIcon.cs、DefaultDebugWindow.xaml及代码；Plugin.Infrastructure ToolRegistry.cs；UI工具箱XAML／ViewModel、画布XAML及4个分类适配器、MainWindowViewModel标题图标注入、App.xaml.cs工具箱探针；22个工具类型所在源码的Tool特性；ToolboxPresentationTests.cs及方案／当前任务记录。
- 已完成：9类编排、分类内Order、22个已注册工具统一矢量图标、旧节点从当前工具元数据获取图标、紧凑分类红点移除、标准标题图标及子程序点击行为保留。SDK共享影响限元数据、呈现和探针，执行算法、参数和调度不变；产品版本及开发契约不变。
- 未完成：接收／发送插件仍未实现，没有加入占位工具；VS设计器和主窗口人工验收待完成。
- 已执行构建与测试：专项32通过（工具箱5＋协议工具27）；扩展UI258通过；SDK专项76通过。全量Release0错误1145警告、标准UI Release0错误947警告。实际Release工具箱探针9类22工具顺序正确，退出0；浅深色控件渲染图已检查。证据artifacts/validation/toolbox-presentation-20261012，详见方案基线。
- 人工验收结果：用户已关闭Release允许部署；尚未反馈新界面验收。已有STA布局／绑定自动测试，不冒充VS设计器或用户验收。
- 已知风险或禁止改动：共享SDK／工具须同时构建；未知图标键仅中性图形，不自动复用Emoji；不删Threshold／ImageThreshold，不改标定算法，不更新Debug或覆盖主项目。前阶段完整SDK两项失败未在本轮处理或重跑。
- 下一步：用户在通信项目Release复核9类顺序、悬停提示、两种工具箱模式、旧方案节点图标及标准配置标题；随后继续接收工具及无UI通信运行服务接入。

方案基线：docs/01-方案设计/2026/10/工具箱分类排序与图标基线-20261012.md。
