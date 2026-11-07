# 图像源新工具编辑器集中交接（2026-10-28）

## 基线与独立写入

- 分支 `codex/image-source-editor`，独立工作树 `C:/Users/houzhongjie/.codex/worktrees/workflow-overview/SunEyeVision_V2`，基线 `e81a6e84`。
- 本批仅写 `tools/SunEyeVision.ImageSource/Wpf/**` 和本单；不写主仓、87f4、公共项目/资源/Catalog/合同。不构建私有 Release、不复制 DLL。
- 新 Runtime、宿主正式配置接纳/源条设备业务、正式发布由 root 集成；Plugin.Wpf 原件源条另路独立实现。

## 原件与整体决策

1. `D:/MyWork/REA逆向测试/examples/plugin-definitions/ImageSourceModule/ImageSourceModuleAlgorithmTab.xml` 与恢复工程 `src/VisionPlugins.ImageSource/Resources/ImageSourceAlgorithmTab.xml` SHA256 均为 `001a57675192867594d3bbc7efa94462c19f056583659d759ceb5648e55091e0`。实际是一个 `Tab_Basic Params` 页，内含基本参数、触发、订阅文件分类。不是旧宿主基本/高级两页。
2. `D:/MyWork/REA逆向测试/analysis/decompiled/ImageSourceModuleControl/datatemplate/paramusercontroldatatemplate.xaml` SHA256 `0409c47466c404fd83129214eca580a4fc70dba143da2dbcbfd601e7c0c672c7`。该工具的 LocalImageList 模板绑定真正图像源根，Width=500，外边距0,0,0,10；与主结果 VMControls.WPF 同名类型不可互换。本批不复制其厂商业务根或另造缩略图条。
3. 原件本地/相机切换控制像素格式、本地图像路径与自动播放、相机选择/曝光/增益等显示。现正式 Runtime 仅承载已批准 local/camera、输出格式、起始SN、自动切换、末图停止、相机超时；厂商SDK来源、曝光/增益、拼接、目录订阅没有合同基础，不伪造可用字段。
4. `DeclaredSettingsView` 复用已迁公共参数原件，Runtime `SettingsSchema.Presentation` 是唯一布局/字段/条件声明。Wpf 不复制 schema、默认设置或参数业务模型。

## 集中组装链

- 元数据：工具 ID 仍 `ImageLoad`；Wpf角色Editor，入口 `ImageSourceEditorFactory`，程序集 `SunEyeVision.Tool.ImageSource.Wpf`。
- `CreateSettingsView(draft, options)` → 实际 `ImageSourceParameterEditor.xaml` → 同一个 `DeclaredSettingsView.Bind` → Runtime字段声明与现公共原件参数控件。
- Runtime展示约定（root写入）：单页“基本参数”；分类“图像来源”(source.kind)、“基本参数”(output.format本地条件、sequence.initial)、“触发”(auto-switch/stop-at-last本地条件)、“相机”(camera.timeout-ms相机条件)。隐藏序列字段仍存在于正式draft与值校验，不生成裸sequence/text编辑器。
- `local.paths/start/selected/run-mode` 和 `camera.device` 的真实列表/设备组合通过既有 `ToolSettingsViewOptions.InputContent` 接入；不挪用下游上游图像来源选择器，不在Wpf文件枚举、管理连接或执行工作流。
- 注意既有API事实：`DeclaredFieldsView._groups` 按 `FieldLayout.Page` 建索引，`AddToGroup(options.InputSection)` 实际查页身份。所以本编辑器固定 `InputSection="基本参数"`，把宿主组合放在该页第0行；不是“图像来源”分类精确插入。不擅改共享API。若外观必须置于来源枚举之后，由root与主线整体调整公共插入合同。
- 接纳：公共视图负责 `draft.ApplyFields` → `AcceptCandidate` → accepted draft安装。编辑器不旁路写源模型。
- 生命周期：InputContent成功附着后由公共视图拥有IDisposable生命周期，替换/最终Dispose释放；未附着失败仍创建者拥有。编辑器不再重复释放内容，Dispose直接委托公共视图。Changed、HasInvalidInput、TryCapture均直接委托。
- 结果工厂复用现 `DeclaredResultTable`，仅为当前标准结果投影入口；不宣称完成D批原件树/表格迁移。

## 设计器

- Attached sample放在实际XAML的真实DeclaredSettingsView上，独立设计不需要主程序、方案或算法执行。
- 设计draft通过实际Runtime的 `IToolDefinition.CreateSettings/FreezeSettings` 和 `IToolSettingsFields.ReadSettings/ApplySettings` 获取默认与接纳，拒绝和重入不替换旧设置；不复制默认值或第二模型。
- 设计InputContent已接真实公共ImageSourceStrip，只读session直接投影同一实际draft的local.paths/start/selected/run-mode/auto-switch；实际默认空列表显示真实空条，不另造图片、设置或资源。设计会话拒绝请求，不做文件和相机业务；当前只做静态核查，真实VS尚未验证。公共源条类型将在集中集成时与source_native路汇合，不以静态源码代替完整组合验收。

## 集成与验收出口

1. root集成Runtime展示声明、公共源条、工具Wpf项目发现/解决方案及发布；Runtime项目路径为 `../Runtime/SunEyeVision.ImageSource.Runtime.csproj`。
2. 宿主创建同一正式draft和InputContent（本地原件条/相机设备组合），固定生命周期/切源代次与AcceptCandidate，旧入口整批退出。不在该Wpf插件做兼容回退。
3. 主仓唯一同源正式Release集中构建。验证local/camera字段条件、空来源可编辑/未选相机可编辑但执行就绪另核、真实编辑接纳与拒绝、导入选图和原件条交互、切源与关闭清理。
4. 检查新Wpf组件发现、TryCapture、取消/重复Dispose、附着前失败/附着后异常、深浅主题、宽窄/DPI/禁用状态，真实VS及人工验收分别记录。

当前状态：源码候选；没有编译、运行、设计器或人工验收成绩。不套45项旧基础成绩。Group及Blob业务未动。
