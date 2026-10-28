# 原ROI手势收尾修复交接

- 基线提交：dfb7b053，当前正式产物也为该提交；无活动构建/测试。
- 当前分支与执行者：main，Codex为主仓唯一产品写者。
- 任务目标：第一阶段C08完整交互；修正同原控件同步开始新手势后，旧结束尾部清掉新手势的确定缺口，不改Blob业务。
- 允许修改目录：Plugin.Wpf原ROI控件/非视觉适配与真实验证；用户BlobParameterEditor.xaml禁止改或暂存。
- 已完成：四路只读核清CircleROI、RectROI、SectorROI、PolygonROI；尚未实施本修复，不套此前参数栈/视口27过为实际捕获通过。

确定路径：旧Up先清handle、ReleaseMouseCapture或NotifyROIParamChanged；同步回调在同一原控件启动新Down。旧finally随后无条件清`_isMove/AfterMouseUp`，将新`IsDragROI`清掉。没有handle的旧Cancel也无条件清共享旗。Attachment入口门禁不能覆盖方法内部回调。换owner后旧控件若已断树，未证明所有此类情况都必现。

下一完整实施批：

1. 同一ImageViewBox的短生命周期手势令牌，加控件本地请求代次；不是持久模型。Down在选择事件前占代次，选择路由、CaptureMouse、IsDragROI设置后逐次核token/handle/context/template/parent。
2. Up/Cancel先快照旧token、changed及handle，清旧局部状态并摘旧监听，再Release；其后不无条件改新局部状态。共享清旗只由同token仍拥有时结束；不得在失败分支调用会取消新请求的无参Cancel。
3. ROIControlBase.NotifyROIParamChanged增加受门禁路径，冻结旧command/context/view，在DataChangedCommand.Execute与父图通知两次外调间复核，不能第二次重读新context冒充旧提交。
4. CircleROI的Move/半径/中心setter后复核再写旧clickPoint。不能把双半径CircleROI当普通圆CircleMes。
5. RectROI: Down384–403；Move406–519、AdjustRect526–558每个宽/高/中心/角度setter后复核；Up561–573与Cancel582–591。Wheel617–621当前只改角度未置changed，纯滚轮后Up不提交；补token有效时标变化，保AddAngle键盘调用不强制鼠标token。
6. SectorROI: Down657–670、Move673起、三段重塑800–807/862–869/923–930逐setter复核；IsFresh批异常安全且旧token失效后不能恢复新手势。Up958–966/Cancel970–978同统一收尾。
7. PolygonROI: Down330–342在选择事件前占本地代次；Current344含token/context/parent；MoveStep345–356每个顶点写前后复核；Move358–367与Up369–374、Cancel376–381；同handle/points/topology不能代替手势身份。集合/模板取消前先推进代次，外调后不继续覆盖新模板字段。
8. 四类OnApplyTemplate取消可重入，取消后需核模板代次再继续挂部件。

- 必要验证：真实原模板捕获失败零提交；LostCapture/accepted Changed同步新Down保新handle/实际capture/共享drag；旧提交不通知新context；Rect仅Wheel120后Up角度+0.5且一次writer；Polygon顶点通知中换手势旧循环停止；模板/拓扑重入不重复挂载；参数拒绝恢复accepted。当前真实Win32鼠标有历史error5边界，不能反射填handle冒充真实捕获通过，自动与人工分列。
- 下一步：主线成片实施四类令牌及通知守卫，之后集中规定构建、同源相关回归，保失败日志。普通圆CircleMes另有完整原三字段/单圆图标闭包，随后接入，不能拿环形图标/内半径近似。
