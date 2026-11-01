# Blob 检测继承只读预览设计与候选交接

- 基线：49e8f50aa5e9d0ef812576029da4b44628d6afcd；分支：codex/blob-roi-preview-20261101。
- 唯一工作区：D:/MyWork/line-aiproject/blob-reference-chain-20261101。旧继承候选909170d2及原分支保留，不改主仓、正式Release、用户方案或Script，不启动VM。
- 当前结论：本方宿主只读预览及其相称候选自测通过，尚未集成主仓。原厂只读预览的视觉/自动刷新时序证据尚未送达，不称VM等价或整个ROI完成。
- 机器回执：[BLOB_ROI_PREVIEW_CANDIDATE_20261101.json](BLOB_ROI_PREVIEW_CANDIDATE_20261101.json)，含冻结源码、构建/测试日志、全部实际加载程序集及截图SHA256。旧失败保留在候选Release；不得以最终通过覆盖原失败。

## 完整生产链与复用

RuntimeDebugInvocation已有ParameterBindingService/RuntimeVariableBindings、发布快照、变量生产者/动态索引与reader租约。原Capture验证整节点、解析全部活动字段并MaterializeSettings，不能直接用于只读预览。本批共用私有CaptureCore，增加CaptureInputs，仅解析请求的活动Schema.Inputs及其依赖；不建立另一引用/索引解析器，不要求无关算法参数完整、不MaterializeSettings、不执行ToolSession、不写AcceptedParameters/current/正式字段/历史。

唯一生产入口：MainWindowRuntimeEditing从当前文档和真实publication调用CaptureInputs；RuntimeNodeConfigurationControl包装为仅依赖Contracts的NativeRoiInputLease，经NativeRoiFrameSource交BlobLocalRoiOwner。请求为图像、检测模式及当前活动组合或五/六叶。BlobRoiInheritance.Materialize原实现直接复用，支持正式SequenceValue/double[]、min-count及64上限，没有第二持久owner。

RuntimeInputPreviewLease持有InputValues与来源RunIdentity及释放动作。捕获前使用CaptureDefinitions的冻结ValueRevision配原正式变量对象，源配置身份在读取publication前记录；捕获结束及IsCurrent再核变量/动态索引/生产绑定、来源配置与publication执行身份。目标证明按实际节点、目标schema、活动规则及请求字段稳定序列化身份校验；无关mask/算法参数替换不退役，有关模式/字段/schema变更必须退役。部分失败、CompleteCallbacks异常及Dispose均逐项释放reader并聚合异常保首错。

## 同帧、原renderer与单owner

ResultWorkspacePresenter只借实际可见pane已选中的正确来源key/publication，不自动切图、不恢复用户后续选择、不使用fallback克隆。冻结图像InputValues的ImageFrameAddress及EffectiveDomain.Frame须与当前frame receipt精确匹配；同尺寸不是证明。Preview与mask共用BlobLocalRoiOwner的一次BorrowRoiFrame，不能另借第二owner。

只读检测使用独立ShapeLayer实例，order45；mask原attachment层为50。矩形复用NativeShapeProjection，圆环复用NativeInheritedAnnulusProjection/IAnnularSector原renderer。独立层不更改ROIList或mask session stamp，投影全部Visible/Disabled，不可命中、选择、复制、删除或写历史；刷新只清本层。负扫角保留接受Record，原renderer可反向遍历为正扫同一弧域，不按方向参数原样判错。

## 通知、隐藏与退役

所用变量及动态索引PropertyChanged、正式History.Changed的成员变化、相关字段/检测模式Changed、既有ResultWorkspaceChanged及专用RoiFrameAvailabilityChanged合并Dispatcher刷新。变量监听先安装再冻结，防监听空档；监听只涵盖实际依赖。正式删除/Undo、SetValue及producer声明变化可触发重新订阅；绕过History直接修改集合不宣称即时通知，读取proof仍拒绝失效对象。

专用pane可用性通知不重建plan、不选图。InputSelected/OutputSelected只退役占用该pane的ROI；双图切换全退役。恢复非法选择、_refreshing早返也通知；Dispose先disposed/清plan再通知。不能在通用RetireRoiPresentation内同步通知以免重借递归。Loaded/Visible是Acquire/Activate准入，隐藏释放frame与input reader，显示恢复原组原文；无可借帧保留真实参数视图，不伪造图像。

仅只读来源更新不修改mask stamp/ROIList，不提交、丢弃或取消mask pending/手势。RefreshPreview用dirty/代次重排处理重入，旧proof、旧lease和旧Dispose不得清新层。

## 完整取消前记录与原gesture身份

实际TryPresent/Clear先于host异步通知，原attachment.CancelPending可能先清ActiveDraw与投影暂存。NativeRoiSession.InteractionRetiring因此提供只记录边界：Blob仅保存原文和未完成手势的拒绝状态，不通知、重借、选图、改配置或历史；订阅按session/frame代次对称退订，旧session不能写新owner。

| 入口 | 本批处理 |
|---|---|
| Bind、Unloaded、ReleaseFrame、Dispose | 清理前只记录，再逐项聚合执行原取消/资源清理 |
| 失效PrepareDrawing/StartDrawing、NativeCancel/CaptureLost、drawing失败 | 仅失效proof记退役；有效用户取消或有效绘制错误保持原行为 |
| 原gesture CancelDraft、attachment.OnLostCapture | 清原投影前核失效proof，提前捕获旧取消闭包；核session、stamp、draw request、attachment及gesture代次，不重新枚举新gesture |
| Session失效Refresh/Capture/CancelPending/CancelDraw | 有退役订阅者才增加proof读取；预告前后核request/代次，取消rows核同代原row及PendingStamp，不清回调后新安装对象 |
| 正常切组、命令取消、CancelGesture/Escape/原绘制拒绝 | 有效会话不记失效，不改变原公开交互 |

ROIControlBase.Gestures先在旧slot中取消旧暂存，返回后确认未被第三者抢占才安装新claim；成功Claim后捕获取消闭包，绑定真实旧gesture身份。旧End的command重入新gesture并抛错时，其后续清理不能清新item。异常聚合不阻原资源清理；无新事件订阅的既有session消费者保持原CancelDraw直接路径。所有相关公开控件/模板/厂商外观不变。

## 候选构建与实际验收

冻结010源文件构建前后SHA256零差异。候选UI011与Full011均exit0，分别6539/6541警告；HalconSourceRoot使用主仓原依赖源，由标准规则自建/部署本候选，没有复制正式Release。

| 轨道 | 实际结果 | 主要范围 |
|---|---|---|
| BlobRoiPreview Core attempt013 | 5组、exit0、15个Loaded DLL全匹配 | 只解析目标、无算法/写入，捕获期间变量/来源变更拒绝、监听安装变值、清理异常聚合 |
| BlobRoiPreview Window attempt014 | 19组、exit0、15个Loaded DLL全匹配 | 真实Host、同帧原renderer、mask同屏/原文/手势、索引空失效恢复、History删除/Undo、同ID SetValue、publication/错图/单双图/隐藏、自动取消、End命令重入、旧回调关闭 |
| RuntimeDebug attempt002 | 7组、exit0、13个Loaded DLL全匹配 | 原正常调试冻结/设置订阅、跨轮保护、资源排空、fallback、环及缺输入拒绝 |
| FieldEditing attempt002 | 实际31组、exit0、12个Loaded DLL全匹配 | 当前完整公共字段脚本；不是38组，不代其他工具完整验收 |

运行命令均为Run-S2ReleaseValidation.ps1 -ValidationScope Candidate。Preview用-Stage S4 -Track BlobRoiPreview -TimeoutSeconds 480并分别设置S4_BLOB_PREVIEW_SCOPE=Core/Window；Debug为-Stage S3 -Track RuntimeDebug；Field为-Stage S4 -Track FieldEditing -TimeoutSeconds 180。Preview独立CreateDesktop/SetThreadDesktop隐藏桌面；Field原脚本直接owner.Show、未隔离，已自然关窗退出，不能标隐藏桌面。

原RuntimeDebug attempt001缺source→consumer控制前序，既有普通参数引用规则拒绝第二组；只补正式WorkflowConnection，原断言全保留，修后七组通过，不为此修改产品。更早变量“未刷新”原夹具使用草稿创建对象而非正式对象；环角度/缩放及原HALCON离散点误判等失败和诊断仍保留，不冒称产品缺陷。

截图preview-with-mask.png与preview-annulus-with-mask.png已实际查看：只读原旋转矩形/部分环与原mask橙色编辑句柄同屏。原PathGeometry内外/孔洞点、真实ImageScale/变换bounds、非命中及层顺序已断言；不是VM视觉对照。

## 未完成与下一步

- 原厂预览视觉/自动刷新时序待主线证据；平台像素坐标/degree/半径与HALCON合成仍是明确适配，不称VM栅格/坐标/帧等价。
- 组合ROIBOX/ROIANNULUS正式生产者缺失，不能以测试Record称真实组合来源已验。图像ROI/输出mask/屏蔽继承/位置修正仍保护。
- 同ID正式变量SetValue保留原实例；仅在实际对象不同才执行的旧变量迟到通知分支不声称已发生。直接绕过正式History改集合不声称即时通知。
- 原工具箱/附件完成、PrepareRoiGesture/End反射、LostMouseCapture路由事件、异常proof/command为真实处理链注入，不是自然鼠标绘制/拖动。原文使用实际WPF Focus，但不是原生物理键盘取证。清理结构损坏是人工故障注入，不宣称自然可达。
- 任意DPI、自然键鼠、VS设计器与人工未验；主仓正式Release整合验收由主线执行。候选二进制不同于正式产物，不把同源码称正式同版。
- 下一步：主线按文件集成并运行正式验收；不得因此记完整ROI、Blob、S2或S3闭口。
