# 数值控件依赖闭包中立化设计

状态：候选源码已整体迁名并完成初轮构建，正在验证完整消费者；尚未正式集成。候选基线17ed3c0c8，确认分段计时测试另有未提交改动，须独立保留。

目标：保留原数值控件家族与行为，将实际产品命名空间、资源路径及消费者统一按SunEyeVision职责命名。不是重新实现数值逻辑；SliderArrange与NumericUpDown两家族不合并，不保留旧类型兼容入口。原程序集、类型、MVID、XAML与图像SHA保留Catalog和证据。

映射：Native.Numeric.HikPcUI.ListView→Native.Numeric.Arranged；Native.Numeric.HikPcUI.Controls→Native.Numeric.Spinners。Controls/Native/Numeric/PcUi及PcUiControls分别改为Arranged及Spinners；Themes/Native/Numeric/PcUi改为Arranged；Themes/Assets/Native/Numeric/VM.PcUI改为Arranged。实际文件移动前再次核验目标无冲突，按完整闭包同步资源URI。

生产链：DeclaredFieldEditor→NativeNumberPresenter或NativeRangePresenter→原数值控件→NativeNumberCoordinator/NativeNumericCommit→DeclaredFieldState→Blob声明/草稿捕获/执行与保存。保持Int32/Int64/Float64的现有storage与精度保护。共享消费者包含NativeGraphicStylePresenter、CrosshairSettingsView、EditRoundedWindow，以及颜色/IP等输入控件中的真实XAML与代码数值引用。默认WPF Compile/Page和NativeResources.props均须核查，不修改obj生成文件。

实施边界：Plugin.Wpf的数值源码、字典、图片路径及所有精确引用；共享输入控件仅同步依赖，不改变其逻辑；verification中涉及旧全名的数值、范围、字段、设计时、图形、十字线测试同步。JSON来源清单和历史证据保留原厂身份，产品有效目标路径字段需更新。

验收：旧数值namespace及本批厂商图片URI产品引用为零；资源图片字节SHA不变；UI/完整Release构建；NumericContract、FieldEditing、RangeRoundtrip、Blob范围生产、图形样式、十字线、B08真实窗与间接颜色/IP加载回归；正式产物实际加载SHA核对。VS设计时由用户单独验，未支持阈值模式继续保护，不借命名修改扩展算法。确认269秒及原并发超时保留独立待办，不能由此次迁名覆盖。

## 候选验证进展
- UI及全量Release初轮构建通过；NumericContract、FieldEditing、RangeRoundtrip通过。随后更新Catalog本地目标与哈希，须集中重建后再核对同版产物，不能沿用初轮构建为最终验收。
- Area、Circularity的All生产回归通过；Rectangularity仍在会话96904串行执行。首次范围失败保留：撤销断边后测试直接访问已退役结果，现增加结果不复活断言并真实重新执行，再验证恢复链。
- 十字线图层身份回归7组通过（numeric-neutral-crosshair-layers.log）：真实表选择事件属于当前Publication和Frame，独立选择层接纳高亮；清高亮后常驻图文和两个十字按对象身份保留。旧总数3/2断言失败保留，诊断显示常驻图文使选前4项、选后5项，不以改计数替代图层隔离验收。
- 当前通过仅为候选范围；正式Release、VS设计时及人工验收分别记录。未支持算法模式、确认长耗时等原待办仍保留。

## 显示职责边界
图像/图形/文本/ROI原件及模板来自逆向迁移；NativeResultViewport的图层接纳、帧坐标校验、结果投影和生命周期为SunEyeVision适配，不能将整个显示链宣称为原厂代码。数值迁名仅同步这些现有消费者的类型与资源依赖，不改变该边界。
最终候选：bd5e390ba；最新资源UI/全量构建、数值合同42、十字线7、B08完整12通过并核实际加载SHA。三范围各16通过。原超时原因保留，正式集成与验收仍待；详见NUMERIC_NEUTRAL_CANDIDATE_20261102.json。
