# 原四形状绘制与宿主接纳交接

- 基线提交：e8b27e54；本交接随本批中文检查点提交。工作流仍以共同基线独立开发，不重复同步本批候选。
- 当前分支：main。
- 当前执行者：Codex主线唯一写者；三路只读评审及补位。
- 任务目标：C08原四形状绘制pre-Add接宿主一次创建；完整两阶段目标未完成。
- 允许修改目录：现有Plugin.Wpf及其验证/原方案，无新工程，Blob业务冻结。
- 已修改文件：RoiEditContracts、NativeRoiSession/DesignData、Editor/Viewport、新NativeRoiDrawing、原ImageViewBox源码/XAML、原ROIItemsPage样式、Window验证、Catalog和本批文档。
- 已完成：原四替身/几何算法、显式Create/Replace/Affected、请求组/版本/唯一接收器、宿主新Guid、完整快照选择、单次消费、取消/非法/拒绝/异常及重入清理。独立捕获能力与原业务命中分离，原ItemsPresenter host命中绑定补齐。
- 未完成：原多边形上限模态、真实路由/物理指针及人工；继承、位姿、多对象菜单/撤销。未计完整C08/C17/阶段一；禁止开始Blob重组。
- 已执行构建与测试：最终UI004零错误6204警告60.15秒，全量Release001零错误6585警告77秒；Window125=81/Field074=31/Design037=69/RuntimeEditor055=10/FullChain115=8/Components197=9，总208组，正式EXE009正常退出22工具15分类。782发布项/86加载记录/10源增量SHA同源，详见O06_NATIVE_ROI_DRAWING_CHECKPOINT_20261028.json。
- 人工验收结果：本批无真实VS/VM同态/用户签收。正常桌面窗口自动测试可捕获，受限进程失败不能宣称控件实机缺陷；程序驱动生产方法不是物理鼠标。
- 已知风险或禁止改动：用户BlobParameterEditor.xaml索引2/SHA118128fb08ea3163b353620a91359b08b4d5cb1d36a58e37eaa9bf5c83da67ed不提交；失败日志Window112—123保留。Catalog在最终构建后只更新非编译验收元数据及来源，不改产品。原独立DP不改变Paste业务门禁。目标active、不推送。
- 下一步：先补原Polygon超限模态租约；原RadioSelecter/3及继承来源整组，复用现ReferenceFieldSession/WriteGroup。原同族整体/参数不自行互清；位姿复用正式Affine2D，由宿主同快照冻结，UI不烘焙几何。菜单须pre-mutation宿主事务，库不另建undo栈，不以hash作对象身份。三路只读报告供下批核收，不作已实施成果。
