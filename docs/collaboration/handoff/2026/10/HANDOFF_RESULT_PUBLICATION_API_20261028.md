# 结果发布公共合同接线交接

- 基线提交：35a7960b（主线bb5bab8a与工作流30e05a51集中合并）。
- 当前分支/执行者：main / Codex，公共库唯一写者；工作流独立worktree写UI消费者。
- 目标：树表共享同一次输出声明/运行结果、唯一SDK Reader及显式发布身份。
- 公共源码：Plugin.Wpf/Presenters/Results/ResultPublication.cs。IResultPublication:IDisposable提供Stamp/Schema/State/Diagnostics/DisplayNames/DisplayedPorts/IsCurrent/Borrow；具体ResultPublication构造按源码签名，outputs-only Schema。成功/部分成功有Values，其他状态无Values；Declaration无Invocation/Revision。owner只在构造成功后转移。
- 生命周期：宿主AcquireReader一次，ResultPublication持owner引用，Borrow只引用计数；owner.Dispose先退休（新Borrow拒绝、IsCurrent=false），最后借阅释放时锁外归还reader。已安装视图不拥有publication owner。树持惰性lease，表只在Prepare短借，不重查latest。
- IToolResultView：Present(IResultPublication)、Clear()及EventHandler<ResultSelection>，不留Update旧重载。ResultSelection清选择也携旧stamp和空Targets；稳定元素ID仅string/long；多ResultHighlightTarget每组含ImageFrame/Coordinates/不可变Geometry。
- GeometrySource：ResultColumnDescriptor新增尾参，显式同记录Region或顶层单项Image来源；纯Geometry无来源只展示不高亮。来源只在借阅内读取，Region读租约/图像Domain实际坐标/完整物理Frame核对，不能贴当前选图身份。Raw Region/Record绘制要求独立renderer，普通表不会静默吞掉。
- 必要Blob机械同步：Present/Clear/Selection签名、只读行集合与从region冻结SourceFrame，显示声明显式关联region；未改算法/默认值/用户Blob参数XAML。不据此称Blob阶段二完成。
- 本批编译：Plugin.Wpf独立层002=0错5340警告27.39秒；Blob.Wpf001=0错5352警告43.05秒。主UI尚未同步新接口、未全量最终构建/回归，不能作为正式交付。集成发现的ResultShell原IsEnabled误改11处及外层x:Name字段已修；保留首次MC4109及CS0103失败日志。
- 接收方：工作流线负责ToolResultViews/ResultPresentationPresenter/Main全部真实消费者和专属PropertyList适配/图像源工厂；同publication对象驱动树表、声明/生产/调试/历史唯一入口、Dispatcher内复核context/node/generation与失败清理。Root负责S3/Design/Window脚本测试同步并最后集成。
- 当前无本线live构建。未推送；用户BlobParameterEditor.xaml不暂存。真实VS/VM/图像viewport旧SDK退出仍待完成，不套旧测试。

- 后续审查修正：InputValues公开只读Declarations元数据，Publication只校声明身份而直接借值，保Poison资源惰性和数组不重复复制；原表图像来源再比像素Domain与image.EffectiveDomain；新回归脚本尚待UI消费者整合后正式运行。
