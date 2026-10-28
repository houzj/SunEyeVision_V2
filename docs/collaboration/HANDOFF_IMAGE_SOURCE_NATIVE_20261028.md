# 公共图像源条整批候选交接（2026-10-28）

## 身份、范围与写入

- 独立分支 `codex/image-source-native`，基线 `e81a6e84`；专用 worktree `C:/Users/houzhongjie/.codex/worktrees/workflow-timing-footer/SunEyeVision_V2`。仅本单及五个获准 ImageSources 子目录变更；未修改主仓、87f4、共享项目、Catalog、通用主题或公共结果合同。
- 交付为 **未编译、未部署、未实机验收的整批源码候选**。不创建私有 Release，不复制 DLL，不占用正式产物。不将此批成绩套到基础批的 45 项测试。
- 选定原件 `VMControls.WPF.ModuleResultView → LocalImageListView → ImageListPanel → ImageListPanelBase`，不是 ImageSourceModuleControl 同名版本。原九项来源 SHA 继续见 `evidence/IMAGE_RESULT_SOURCE_STRIP_CLOSURE_20261028.json`。

## 实现与消费者接缝

- `Controls.Native.Composition.ImageSources.ImageSourceStrip`：Session 依赖属性接受 `Presenters.Input.ImageSources.IImageSourceStripSession`；`IsExpanded` 为视图依赖属性，125/40 高度。宿主父容器按原件 MaxHeight=110，内部 header40/子面板 Margin=5,-40,5,5、72×52图像项、原水平滚动条及按钮模板保留，不将这些尺寸拆开猜测。
- `ImageSourceThumbnail`：沿原 `ImageView.xaml/.cs`补齐两层 Grid + Uniform Image 的视觉闭包，Source 只接宿主提供的冻结缩略图；原 Byte2ImageSource 图像转换由宿主资产服务承担，不迁入供应商 ImageData 或资源管理。
- Items 为只读视觉投影 `ImageSourceStripItem(Id,Name,Thumbnail,IsForRun,ResourceError)`；Thumbnail 应由宿主冻结并持有，源条不取得像素租约或文件所有权，不复制业务配置。集合/投影替换后发 PropertyChanged；Version 来自宿主接纳版本。
- 会话属性完整为 Items/Version/DisplayIndex/ConfiguredStartIndex/IsReadOnly/IsSelectMode/AutoSwitchEnabled/IsImporting/Status，继承 INotifyPropertyChanged；唯一写意图 `Task RequestAsync(ImageSourceStripRequest,CancellationToken)`。
- Request Kind/ExpectedVersion/ItemId/Flag/ItemIds/Paths 全 get-only；两个集合构造即防御复制为 ReadOnlyCollection，Kind 与参数形态校验。SetRunSelection 传完整 ID 集合，一次原子接纳；Mode/Auto 使用 Flag；拖入文件通过 Paths，导入按钮为 null 由宿主打开选择器。实际业务必须核 ExpectedVersion、当前身份和权限，不能以收到请求就直接执行。

## 原件交互与适配决策

| 原入口 | 证据语义 | 当前请求/行为 |
|---|---|---|
| lst_SelectionChanged 1314 | 用户选图后设置起点并运行；原需 needSelectionTrigger 防恢复误触发 | 明确左键、Enter/方向键等才 Activate；绑定恢复无执行请求；宿主原子选起点后执行 |
| Grid_MouseDown 1562 | Ctrl 切单项运行勾选；Shift 当前项到目标全范围勾选、范围外清空 | SetRunSelection 完整ID集合一次接纳，不循环写正式配置 |
| CheckBox_Click1554 | 更新待运行图片列表 | 同一 SetRunSelection 路径；拒绝/异常恢复一向展示 |
| FirstImage_Click1681 | 选模式下首个勾选项，否则首图；只重设下一执行起点并滚左，不直接运行 | SelectStart +滚左；无合格项无请求；不挪用显示游标 |
| 本地添加/目录 | 原对话框与解码全局服务 | ImportFiles/ImportDirectory由宿主导入；普通和拖放走同一版本化入口 |
| 删除/清空 | 正式列表删除和状态刷新 | Remove/Clear；拒绝恢复正式状态 |
| 打开所在目录 | 图像上下文菜单 | OpenDirectory带该项稳定ID，不使用容易串图的动态当前索引 |
| 模式/Auto | bool视图；原 ExecuteType1/2和字符串参数不迁入 | SetMode/SetAutoSwitch，展示恢复不发请求 |
| 折叠/悬停/横滚 | 原模板、删除按钮仅悬停、首图状态 | 原视觉资源保留；折叠仍可恢复；只读和导入禁止写入 |
| Linux/订阅/RP | 现缺正式合同；原订阅是SDK上报，RP是3D | 禁用菜单占位并明示原因，不实现OS监听或普通目录冒充 |
| Loaded/Unloaded/切session | 退订旧来源、关闭弹层 | 取消旧生命周期token/退订旧session；旧异步完成不刷新新会话 |

源条不调用执行层、不引用 Workflow/UI/Plugin.SDK；Activate/SelectStart 在宿主有意分开。CancelImport 为导入中唯一允许请求，显示取消按钮；错误/状态透过真实组合 ToolTip，异常不吞成成功。键盘导航发明确意图，展示索引不是 TwoWay 正式写入。

## 原模板与资源

- 图像项、原横滚条/ScrollViewer/ListBoxItem/MyButtonStyle、首图/添加/目录/删除/清空/折叠模板直接以原 XAML 为输入迁入；Auto 使用原 BooleanControl 内容式 ToggleButton 与原开关图片，未画近似开关；模式复用已迁入 `Themes/Native/Input/VisionUi/SourceComboBoxes.xaml` 的 `ListViewComboBoxGrayStyle`，未另造下拉外观。
- 新资源字典 `Themes/Native/Composition/ImageSources/ImageSourceStrip.xaml` 仅提供独立深色缺省。宿主须在实际实例 Resources/其主题边界覆盖 ImageSources.Background/Foreground/Border/Accent/Hover/Scroll/MenuBackground，同时沿既有公共模式控件提供 Col_ComboBox_*、Col_Theme、RecScrollViewer 资源。缺省字典是 merged 层，实例直接资源优先。既有共享主题入口/Catalog由主线整批接入，不在本路擅自写。
- PNG均为原件字节复制后中立职责重命名；21项SHA如下。原件来源/许可信息沿既有Catalog证据保存；本路不声称新许可已审定。

| 产品文件 | SHA256 | 原来源文件名 |
|---|---|---|
| add-directory-hover.png | d8b6ac138d7fad48788b09cd31a12b4e9a432ad02805200ff201d6416a57ee6a | tabbar_ic_add-directory_click.png |
| add-directory-normal.png | e50a0feb5de76b88529dd3b54eb3d4448fc055299b422bb4afe58c3e199e08d1 | tabbar_ic_add-directory_normal.png |
| add-files-hover.png | 0662fc8813c0024e065009d934c4456c1a88eb74af8d08d59d3245fd047d88cb | tabbar_ic_addimage_click.png |
| add-files-normal.png | ade1a0dcddbe6dd13b8db50e15caac313c2280bf5beeb2f43c33b875bf9d35c1 | tabbar_ic_addimage_normal.png |
| auto-off.png | 50cedf155ed40fe7ae44cf69242477f5f3ec25aa343a429156e6b4705fb8c0c3 | swith_off_dark.png, swith_off_dark.png |
| auto-on.png | 344bb5e462e60d1b3ddf84ec83f4e4d8a07de41b02f7c5df64b07504a2128403 | swith_on_dark.png, swith_on_dark.png |
| clear-hover.png | 0a3bcda9beb203400b84efff3df3e131a5170d9b9388107795f5167bacc795b0 | tabbar_ic_remove_click.png, tabbar_ic_remove_click.png |
| clear-normal.png | c55da9179eb5cb566b5030f993a85b3c0070f7a4199eaffebbf68f6086978c4a | tabbar_ic_remove_normal.png, tabbar_ic_remove_normal.png |
| collapse-disabled.png | 0b1c5a9c682763d3073ecf260078b2f2b4d7b2ab56551b51751bd45650622e7a | ic_image-fold_disable.png, ic_image-fold_disable.png |
| collapse-hover.png | 97f37b87d2803f1c7c97178426c37048a343710a579e0a54bb2e4fcbe5f48ba9 | ic_image-fold_click.png, ic_image-fold_click.png |
| collapse-normal.png | f1e6c27791ff52c3de6f7997e3fda89483ab4723fa7ee46988628bc805250412 | ic_image-fold_normal.png, ic_image-fold_normal.png |
| expand-disabled.png | d073b8d8934a18a9eccb8c6aa8289afeac4deb82e4d4fdccc7b7f3cc95367559 | ic_image-unfold_disable.png, ic_image-unfold_disable.png |
| expand-hover.png | 6e4934bbd05b25352d65f72b7837cdd48907111d35807fd7ac1f40c028680ce9 | ic_image-unfold_click.png, ic_image-unfold_click.png |
| expand-normal.png | b5b0af07fddfa12d652536ad54856cbc71cb2b21debdd7cffc56248a8552bb06 | ic_image-unfold_normal.png, ic_image-unfold_normal.png |
| item-delete-hover.png | af0f8b2826843279a3c7574169e7eeb8cc7a7aaa6a4be9a820acecaab600b5d2 | image_ic_delete_click.png |
| item-delete-normal.png | 1c0382d5441c18b37ae914ef0cf82192011b103eda5baa6ccb30566f888f7565 | image_ic_delete_normal.png |
| restart-disabled.png | 1ca671f0c7a5c5f831f9649beabf745f4a41905fd185c7457dde773dcfbeacc2 | ic_btn_从头执行_disable.png |
| restart-normal.png | ac7d76d1e011456fc83e7d5b9aca95a47c91f5f3f81c2eb7e20eece481b19e81 | ic_btn_从头执行_normal.png |
| restart-selected.png | 80cd078d727c647c2985704aa1d758b83f3ad4a17b4e5580a81ceb294042d511 | ic_btn_从头执行_select.png |
| run-checked.png | e2e8e43519050f74e4f2e96164bc14ced55d3392bc2598339acb74bfe74d6fe5 | ic_image_checked.png |
| run-unchecked.png | b7204c42dc44b4d5916407536e2b19878e857d37b19253084e83d9eb10003880 | ic_image_unchecked.png |

## 设计时与验证

- `Design/ImageSources/ImageSourceStripDesignView.xaml/.cs` 在正常、资源失败、只读运行选择和空折叠场景使用同一 ImageSourceStrip，不复制预览UI或Runtime业务默认值。样例只构造冻结视觉投影并显示接收意图，不读取方案/设备，不执行工具。
- 必要静态核对：4份XAML可XML解析；23个事件名均有处理器；23个唯一直接组件URI均有目标文件；21PNG逐项与原来源SHA一致；C# Roslyn ParseText核四文件无syntax Error。首次尝试显式载入SDK10.0.401 Roslyn提示已载入版本不匹配，后实际使用当前PowerShell已载入Roslyn解析；不将该行为写作语义编译。
- 未执行：WPF markup编译/完整程序集语义编译、主仓正式Release、实际宿主生命周期/导入/运行、两主题宽窄DPI、真实VS设计器、VM同状态截图及人工验收。XML/语法/资源存在不替代这些验收。

## 主线整合事项

1. 将整个候选提交并入root工作流候选，root业务会话实现冻结API；普通文件/目录选择、资产加载和导入取消仍由宿主唯一业务服务处理。
2. 当前SDK项目默认包含新增cs/xaml且 `Themes/Assets/**/*.*`已涵盖PNG；未发现必须新增项目入口。仍须主线实际WPF构建确认Page生成和已有SourceComboBoxes闭包。
3. 由共享资源唯一写者补Catalog来源/依赖/主题映射；无需改Generic入口才能显式实例化源条。不要把缺省深色当作已通过浅色验收。
4. 独立设计视图在真实VS打开；原父MaxHeight110与125内高度的底部滚动条/折叠裁剪须结合真实宿主比对，不靠单项尺寸宣称外观完成。
5. 原.pending文件未写/未删：它们是原件查源材料，现产品消费者应只接新中立组合；最终旧视觉消费退出由root整批实施。
