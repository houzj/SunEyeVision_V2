# Blob真实窗口受限接入设计与候选回执

基线：0e86b03ddb4e639e00029e3361e41312ea166d3b。唯一写入目录为 `D:/MyWork/line-aiproject/blob-supported-window-20261101`。本批不是ROI/图文生产迁移或S2整体闭口。

## 唯一正式入口和职责

BlobParameterEditor通过现有DeclaredInputFieldsView与DeclaredFieldsView承载同一正式草稿；BlobRunUi沿现有ToolSettingsViewOptions捕获校验接入BlobSupportedWindowState。实际RuntimeNodeConfigurationControl/ProductionEditorContext/Host继续承担确认、取消、调试、历史、复制和保存；不增加保存旁路或方案字段。无正式输入宿主时仍保留原总保护。

图像输入、已接通数值、单阈值、排序/连通及二值输出复用正式字段。只允许检测/屏蔽输入均Unset的现有算法全有效域/无屏蔽语义。原VM几何继承不是像素Region，生产视图保留原几何占位和原四态显隐；真实Region仍留在输入Fields及草稿中，不伪装几何来源。任一非空Region明确拒绝确认/运行，保留原值；本批不提供Region正向窗口选择保存执行。

## 保留S1及拒绝误保存

ROI绘制/图像/继承模式、形状、位置方式、图形眼睛/样式/颜色及文本维持同一原控件交互。NativeRoiUiDraftControls、NativeAssistUiDraftControls、NativeDisplayUiDraftRows跟踪全部临时状态与原始pending；通过Changed向真实编辑宿主传播。偏离无副作用基线时拒绝捕获，恢复基线后可接纳。颜色与复合值复用DeclaredFieldState既有深比较；不为通过测试清空pending。

所有临时项用对应原控件简短Tooltip标记未接入。默认眼睛不表示已绘制。无常驻说明、无新增组/行，无共享EditModes扩展。原灰度/边界正式字段保持原抑制与标签；非安全默认（灰度true、边界false）保值拒绝。原二值行继续真实消费。

已有非Single、启用位置、非空pose、隐藏高阈值Reference/Variable、任一启用正式Judgment均拒绝；不清值。已有pose来源通过原可选引用Clear入口由用户明确清除，信息行随真实设置显示，取消保留旧值。设置与输入Changed均刷新；Dispose对称退订，避免退休草稿通知新窗口。

## 原件与差异依据

原96项/8组和O06/S1资料、`BLOB_ROI_IMAGE_SOURCE_AUDIT_20261101.md`及其链接的ROIBOX/ROIANNULUS原证据决定原显隐与几何身份；`BLOB_GRAPHIC_MIGRATION_DESIGN_20261101.md`及原厂“B08-结果显示配置到视口静态证据-20261101.md”决定图文未接通边界。本批沿现有控件和原资源组装；全图/无屏蔽、灰度/边界安全默认是本方现行执行合同，不能声称原厂等价。

## 构建、验证和证据

独立候选通过HalconSourceRoot只读原始依赖源标准构建；没有复制主仓Release。最终产品构建为 `Release/_validation/BlobSupportedWindow-20261101/build-final-006.log`，0错误/3444警告。冻结前后9个产品文件哈希一致；attempt-006实际加载15个DLL与当前候选产物哈希全部一致，见 `hash-check-final-006.json`。后续仅测试及本文变化，未改产品。

真实窗口专项attempt-006：14组exit0。涵盖实际图像选择、默认捕获、S1模式/形状、图文颜色/内容/原Popup pending、位置条件、隐藏输出保值拒绝、真实取消/确认、历史撤销重做、复制、保存重开、工作流执行及私有调试、三个Region旧配置保值拒绝、正式判定和真实Affine值清除。

同版必要回归：RuntimeWorkflow 10组、RuntimeAlgorithm 9组、BlobParameterProduction 21组均exit0，独立日志为 `regression-*.log`。不把后台Region同帧/错帧能力算作真实窗口正向接入。

失败记录全部保留：window-001/005测试编译错误；002忙线程无法SetThreadDesktop；003选择夹具未限定PortId；004恢复原文后没有失焦提交。006已使用新STA独立桌面并限定正式输出成员，不切换用户桌面。

原Popup诊断见attempt-006/style-pending-trace.json：非法原文在关闭/重开时保留并拒绝；恢复文本本身仍pending，原LostKeyboardFocus处理链后清除pending。测试用Keyboard.Focus证明初次焦点，再显式RaiseEvent验证原失焦处理链；这不等于系统鼠标或自然焦点转移，后两者未验。

## 明确保留

原ROI绘制/几何继承/图像ROI/掩膜输出、三种位置、图文冻结到正式结果图层缺消费者，仍是可交互且不可保存的占位。非空Region窗口正向、实际鼠标/系统焦点、VS设计器、其他DPI、原厂算法同态、跨文档/Root完整整合不在本批通过范围。设计样例复用同一实际控件链，VS由用户人工检查。本批仅候选自测，正式Release集成验收由主线记录。

补充终态：window-007/attempt-007 同版14组exit0，保留原14组并增加实际Visibility强断言，覆盖绘制/继承、四继承分支、屏蔽、图像模式及基线恢复。未重新构建产品。
