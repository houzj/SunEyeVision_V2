# D07未应用补丁只读审核

- 基线：正式dfb7b053；公共Shell唯一view API已固定f93fe2b8。生产D07未完成。
- 制品：`C:/Users/houzhongjie/.codex/worktrees/87f4/SunEyeVision_V2/docs/collaboration/patches/D07_RESULT_WORKSPACE_CUTOVER_20261028.patch`，SHA256 `b5e7732a2cf770313178b8fa1e51866f7994071f366c5172167987ca78cbdeb3`。
- 同名review.json声明base48e5929f、17文件完整proposed文本；已实际读取核hash，applied=false。仅语法/XML过，不是语义编验。
- 自动审批已两次拒绝工作流线的核心显示改写，理由为改写范围大、尚未语义验证。主线不重放被拒脚本或拆命令规避。先把下列完整性问题补齐成具体可审核制品，再处理必要批准；当前不请求用户批准一个未闭合补丁。

三路只读审核的确定缺口：

1. 缺ResultImagePane/LocalResultImagePreview/ResultViewportFullscreenScope/ResultViewportImageLease等前置源码；当前17项不能独立集成。必要依赖及测试需同包明确。
2. 已共享snapshot再次FromSnapshot取得第二reader/publication；selected结果必须借实际_resultPublication，上游独立来源才由plan拥有新publication。多个刷新入口也应收敛。
3. Present先ClearGraphics再取epoch/plan，会借新代次执行旧来源并清掉同步重入的新帧；入口冻结请求，每个清理后复核。
4. 构造/Dispose/Shutdown中DP通知、事件退订、CTS Cancel、CloseResultPresentation等外调未全部纳聚合，异常可跳过panes/plan释放且置disposed禁止重试。
5. 固定橙色绘制全部Regions，不是正式Geometry/Text显示映射、可见性及OK/NG样式；旧DisplayCollector退出前必须有完整替代生产链。
6. 每Region全帧BGRA缺整场景层数/总字节/runs预算，可能OOM并留半场景。先正式显示选择/整批准备准入，超限拒绝本结果场景，保背景/表，不截断。
7. 无正式SetRegionInputProvider/BorrowCurrentImage/BeginRoiEditing/HasPendingGesture接线，Runtime编辑入口也未注入冻结输入；不等于ROI私有草稿闭环。
8. RoiViewportEditScope历史固定false并拒Undo/Redo，不能替代Apply前私有历史。
9. 全屏同一view正确，但搬移前需CancelGesture/编辑scope取消、焦点恢复和异常关闭矩阵，不能只等Unloaded。
10. ShowDeclaredRun仍无真实历史调用；历史selected与最新WorkflowContext上游会拼不同run，需显式历史运行来源上下文。

以上均已交原写者集中修制品。不移除原目标、不恢复SDK视觉后备、不把未应用提案或局部原视口通过宣称完整交付。Group非视觉边界继续冻结。
