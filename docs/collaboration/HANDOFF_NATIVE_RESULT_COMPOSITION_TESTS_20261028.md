# 原件结果组合真实 WPF 验证交接

- 基线提交：fb7e7228。
- 当前分支：codex/image-result-visual-validation。
- 当前执行者：source_native，独立 workflow-timing-footer worktree 唯一写者。
- 任务目标：为实际 PropertyListView / ResultWorkspaceShell 增加有行为意义的同源 WPF 验证，不改产品或共享合同。
- 允许修改目录：仅 tests/SunEyeVision.UI.Tests/NativeResultCompositionTests.cs 与本交接单。
- 已修改文件：上述两个新增文件；未改项目、验证脚本、产品及其他 worktree。

## 整批验证出口

新增 1 个双主题 Theory（2 case）与 4 个 Fact，共 6 case，必须由主线正式 Release 构建后的同源测试运行器执行。

1. 深浅实例资源追加到真实控件，实际 Loaded 后核 resultGrid / Shell 背景颜色随字典切换；原全屏模板加载为真实 Image，不只是数资源；独立设计同一实际双 ImageViewBox 消费同一输入输出数据对象。
2. PropertyList ItemsSource 使用真实 Binding 借用同一 ObservableCollection，核树根与层级容器 DataContext 就是原视觉行对象；双向展开更新原行；SelectedItem 程序恢复和卸载重载不执行用户 SelectedCommand。
3. 公开 routed PreviewMouseDown / MouseLeftButtonDown 驱动真实 TreeViewItem 输入，核 SelectedCommand 只收到同一个实际 row 一次；随后程序恢复不重复执行。不反射私有处理器，不通过强行设置 IsSelected 冒充用户输入。
4. 真实模板中的变量链接/解绑按钮在只读与服务缺失时禁用，并加载原链接图标。
5. 实际 ResultWorkspaceDesignView / ResultWorkspaceShell 单双图列切换、Info 235/40、独立选项、卸载重载后同一 ImageViewBox 实例及同一数据对象；历史入口明确禁用。

辅助 LoadedHost 在专属 STA 线程真实 Show WPF Window（屏幕外，不进入任务栏），经过 Dispatcher ApplicationIdle 后执行行为。测试退出 Close / Dispatcher shutdown，有时间上限。没有复制另一套预览 UI，直接实例化同一正式 Design 和产品控件；视觉样例来自已有设计数据，不伪造 ImageFrame、publication、Domain 或 ROI session。

## 已完成与未完成

已执行：新增 C# 使用当前已加载 Roslyn 5 纯语法检查，零语法错误；提交前 diff 检查。

未执行：语义编译、测试运行、正式 Release 构建、截图与 VS 实际设计器、用户人工验收。本分支未运行旧产物、未私建 Release、未启动程序。源码测试候选不代表 6 case 已通过。

静态发现：原模板保留 Focusable=False，而 WPF 默认 TreeViewItem 的实际鼠标选择依赖焦点链；真实 routed 用户选择测试可能揭示原模板接入后无法点击选择的问题。已通知 root。测试不绕过该输入链，也不在本测试分支修改产品；请主线按实际运行结果集中修复与重验。

下一步：root 合并并将本类纳正式验证轨，集中正式构建后运行 6 case；失败应核产品真实模板/资源/行为，不删除用例或用私有事件调用替代。VS 设计器与人工外观验收继续独立记录，自动 WPF 测试不能替代。
