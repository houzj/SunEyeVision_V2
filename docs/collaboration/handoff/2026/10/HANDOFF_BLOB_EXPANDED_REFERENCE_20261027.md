# AI 协作交接单：原扩展订阅窗口

- 基线提交：f13cf433；本批成果见最新中文本地检查点。
- 当前分支：main。
- 当前执行者：Codex主仓唯一产品写者；三路只读逐控件核证，已补位C08。
- 任务目标：两阶段完整目标不缩减，第一阶段继续，Blob业务重组未准入。
- 允许修改目录：公共库、必要当前消费者、验证及原O06；不复制产品/算法库/仓库。
- 已修改文件：原ParamSubscribeWindow/Checkbox激活；ReferenceExpansionRequest/Session/PopupScope，公共Presenter协调器，引用/单值/between消费，Catalog和验证、原方案。
- 已完成：同Session及组writer；原双树、来源双击、括号索引/括号外清空、实际Owner互斥及双向跟随、默认UI偏好、代次与节点身份门禁、关闭/卸载/模式退出清理。未声称VM Solution偏好持久化。
- 未完成：真实VS/VM同态用户验收；C08原参数栈/同帧图像、完整显示/窗口/命名及其他阶段一要求。第二阶段继续冻结。
- 已执行构建与测试：UI002和全量Release002均0错误；窗口044=26、字段062=31、Runtime窗口042=10、设计018=28、整链101=7、组件186=9、内核040=17、算法025=9、文档049=30；正式EXE exit0/22工具。
- 证据：O06_BLOB_EXPANDED_REFERENCE_CHECKPOINT_20261027.json，12源码增量/760发布项/224加载SHA/九轨同源。Catalog已列原来源SHA再核；实际原专窗PNG已查看；无Application/宿主构造正式资源及双树通过。
- 人工验收结果：本批真实VS与VM同态用户验收未取得，不代签。先前人工反馈仍保留。
- 已知风险或禁止改动：用户BlobParameterEditor.xaml SelectedPageIndex=2保留未提交。不推送。失败Window042/043、FieldEditing061保留；测试取刷新后当前树节点，不放宽旧节点门禁。
- 下一步：C08原Box五参数栈及原图像事件的独立同帧样例，下面静态核证是实施入口，不算已运行。

## C08三路只读核证汇合

1. BoxROIStack使用Apps.XmlUI.ParamTabControl.SliderArrangeFloat，不能换成VM.PcUI五single。X/Y为−30000…30000，宽高0…30000，角度−180…180；均步长1、默认两位小数。前四通过RightFixdMultiplicationConverter按图像尺寸归一化，角度直接绑定。零宽高可进入原控件，但正式几何接纳须拒绝；图像尺寸0明确禁编辑，不造尺寸1。
2. ROI原RoiSelectControl模板内名为expand的ToggleButton与ROIListStyle必须同名称域。通用Expand DataTemplate是高级布尔行，没有该名称，不能混用。原ROIListStyle按15种形状DataTrigger选模板，静态资源必须解析；适配层保证唯一活动形状。
3. 同Group+Guid+frame generation投影实现INormalizedRectROI（继承IRectROI），另公开CenterX/CenterY供Box栈；不能只实现IRectROI而选错像素RectROI。原ImageViewBox.ROILIst承接同一批对象，RoiSelectedCommand收到RoiWithTagEventArgs.Roi按稳定身份选中，不按Tag文字/序号定位。
4. 图像RectROI多属性手势先更新同Guid临时候选，结束时NotifyROIParamChanged统一接纳；参数栈OnStartValueChange进入ROIValueChangedCommand同一入口。DataChangedCommand与RoiParamChangedEvent只选一条接纳入口，禁止双写。成功重读当前根，失败整组回投；切帧/选择/删除/卸载取消旧候选。此候选不是第二持久化模型。
5. 原DragingChangedCommand→ROIStackControl.IsDraging→反向CanUpDateROI保留；参数和图形同帧双向，宿主无UI消息泵算法边界不变。不能借SDK绘图层替代原图像手柄。

原文件入口均在现库pending：Apps.XmlUI.ParamTabControl/RoiSelectControl.xaml、BoxROIStack.xaml(.cs)、paramtabcontrol/listview/roiselectdatatemplate.xaml及Apps.Style/buttonstyle/bottombuttonstyle.xaml。需定向修正合并URI/依赖后激活，不把pending导入当完成。

原来源SHA：ROIListStyle为16748f53467c58c978581b25c0233c4359e6c70770c63b551c1d0e0fff4e8f03；RoiSelectControl为d8a7b7d07ab9d06d781f5a4a1b10020d137007d0e5e973f701ca6239a83381eb；bottombuttonstyle为0eb6fbf313592f15b61cf79a612cadbb4802ff73ea15a2493fcd5280e5b317fc。以上只读源码核证，尚未构建或实机运行。
