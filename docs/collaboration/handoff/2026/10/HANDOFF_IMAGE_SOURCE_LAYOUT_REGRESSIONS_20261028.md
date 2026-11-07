# 图像源与结果组合正式回归布局异常交接

- 基线提交：536520b3。
- 分支：codex/image-source-layout-regressions。
- 当前执行者：source_editor，独立 workflow-overview worktree。
- 任务目标：按正式 imagesources001 失败修复真实组装链，不跳过窗口显示或弱化用例。
- 已修改：ResultShellStyles.xaml 的六个折叠/展开图标 URI。原路径残留 Apps.Style 程序集及空格名称；改为现 Plugin.Wpf 已部署的 Apps.Style 原件资源及实际连字符文件名。六个目标均已核存在。
- 已定位但未越界修改：ColorThemesResourceDictionary.cs 的静态进程单例持有未冻结主题画刷，ColorResourceBinding 从该字典取对象并绑定原参数宿主。不同 STA 的设计窗口复用该画刷，在 Show/RenderClose 抛父子 Freezable Dispatcher 不同。建议按 Dispatcher 弱表持有独立字典，保留动态主题切换，不用把所有测试挪到同一 STA 掩盖问题；共享主题文件由 root 协调。
- 原日志纠正：NativeResultCompositionTests 的 Shell/两态主题失败是 Apps.Style 资源程序集找不到；不是该日志中的 Freezable 异常。Freezable 对应图像源设计组合四例。
- 树恢复未改：Hierarchical_binding 在重挂后的 child 被 root 覆盖；PropertyListView.TreeSelectionChanged 在恢复期间仍可更新 SelectedItem。交 root 独占原件处处理，保留原 child 断言。
- 已执行：XAML XML 解析；六态资源目标存在核验；git diff 检查。
- 未执行：语义构建、正式 Release 回归、Visual Studio 设计器、人工验收。未创建私有 Release、未复制 DLL。
- 下一步：集中合入资源修复及主线颜色字典/树恢复修复，沿现真实 Window.Show 回归验证；D06 尚未开始产品写入。