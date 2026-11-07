## 正式集成补记

fd70886a已经规定UI/full零错误、正式EXE0、ROI40/Box3/Polygon2/圆环6、Design96/Editor10/真实HALCON FullChain8通过。七条receipt与795发布文件/76加载记录SHA核，详evidence/O06_ROI_GESTURE_INTEGRATION_20261028.json；过滤组有重叠。候选6过不再替代正式结果，但整体阶段、实际指针全姿态、VS/VM外观/用户验收仍未完成。下一批见HANDOFF_NEXT_BLOB_LIBRARY_CONTROLS_20261028.md。以下候选历史保持。

# 原ROI手势完整候选交接

- 基线：09f9739a；main唯一产品写者Codex，三路只读评审。
- 已修改：原四类ROI及共享ROIControlBase.Gestures、ImageViewBox中立取消/guarded通知、NativeRoiAttachment、原点集转换器；新增原窗口六项事务负测和Catalog来源本地SHA。
- 已完成：库005零错误；候选真实WPF捕获窗口六组通过，原wheel与Up路由、公开Polygon Move命令、DP观察者均真实执行；不伪造私有handle或捕获状态。
- 实际修复：旧清新/capture/共享drag、旧callback读新DataContext、布局通知重入后旧尾部写、wheel-only不提交、异常泄漏监听/捕获、取消残留pending、旧stamp取消新stage、多边形通知中恢复集合重入。
- 失败保留：artifacts/roi-gesture-window-20261028-002.log有真实4过1败与集合重入异常；003五过、004六过只对应各候选源，不回填旧失败。
- 未完成：集中规定UI/full、正式EXE/发布SHA与关联ROI/Design/Editor/HALCON回归；原实际指针Move姿态/VM外观、真实VS、用户验收。普通圆CircleMes与C09完整工作仍保留。
- 禁止：不修改或暂存用户BlobParameterEditor.xaml；不推送、不改Blob业务，不用候选通过称阶段准入。
- 下一步：固定源码并集中规定构建/正式回归，再统一原方案真实状态；继续普通圆/位姿等剩余闭包。
