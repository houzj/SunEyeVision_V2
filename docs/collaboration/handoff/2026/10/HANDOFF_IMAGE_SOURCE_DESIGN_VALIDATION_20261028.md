# 图像源同组装设计链验证交接（2026-10-28）

- 基线提交：30e05a51。
- 当前分支：codex/image-source-design-validation。
- 当前执行者：source_editor；workflow-overview独立worktree。
- 任务目标：验证B09新公共视觉组合及外层统一Bind的真实公开入口，不改产品。
- 允许修改：tests/SunEyeVision.UI.Tests/ImageSourceDesignCompositionTests.cs及本交接。
- 已修改文件：以上两个；未改产品、共享控件、合同、项目入口、Release。

## 已新增覆盖

新增1个Theory两态及2个Fact，共4个执行用例：

1. 正式PluginLoader按同版HostDebugRoot发布准入加载组件；通过ToolRegistry.CreateSettingsView断言获得真正ImageSourceParameterEditor而非通用回退，再反射公开ImageSourceDesignData.CreateParameterContext(bool)及公开工厂（不引用具体工具项目、不手动Load DLL）。用真实IToolSettingsView和离屏Window呈现同ImageSourceInputView，local/camera两态验证IsCamera/源条显隐、只读真实空列表、相机picker与管理按钮禁用、未连设备状态；只读请求拒绝；TryCapture成功；Dispose清除session。工具Wpf程序集不能引用UI/Workflow/DeviceDriver。
2. 真实DeclaredFieldEditor.TrySetConstant编辑同draft的local.auto-switch及source.kind，通过实际Changed→外层OnFieldsChanged→内容RefreshSourceDraft链验证自动开关立即同步、readonly Session更新、本地/相机显隐及切回后值保留；不直接调用私有handler，不用手工Refresh替代产品链。
3. 用自有无视觉业务CallbackSource仅记录正式IImageSourceSettingsContent.BindSourceDraft传入的prepare/accept，真实factory创建外层并调用公开Bind重绑。原source Changed走产品重读不提前Dispose；替换输入Dispose一次；旧prepare/accept拒绝且不向新草稿接纳；旧Changed已退订；新回调正常；最终双Dispose只释放一次且当前回调/再Bind拒绝。探针不复制产品模型或视觉。

## 验证状态与依赖

- 新文件当前已加载Roslyn纯语法解析错误0；git diff --check通过。
- 未语义编译、未执行上述4例、未运行正式Release、未真实VS Designer或人工验收。测试源码存在不代表B09通过。
- 依赖30e05a51的公开ImageSourceInputView、ImageSourceParameterEditor.Bind、CreateParameterContext(bool)及现有正式发布loader；项目沿UI既有Plugin.Wpf/Infrastructure引用，未新增具体工具ProjectReference。
- 用例由正式UI.Tests自动编译包含；本路未改ImageSourceCases等共享清单，主线若定向反射脚本不自动发现本类，需集中纳入这3方法/4执行例，不把旧用例数套入。
- 测试临时设置APP_CONTEXT_BASE_DIRECTORY后finally恢复；同VM canvas UI集合串行。离屏真实WPF展示只验证实际组合与行为，不能代VS独立设计器、视觉布局主题和用户人工。
- 下一步：root整合后由唯一正式Release窗口编译/定向运行，再实际VS分别打开真实ImageSourceParameterEditor与camera DesignView核验两态。