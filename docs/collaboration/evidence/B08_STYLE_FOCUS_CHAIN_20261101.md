# B08样式弹层真实焦点链候选验证

基线5ccbd2a945f8902a0d82bc7eae9141f419f1633d；主线确认与15171638产品相同。仅本独立工作区写测试，产品保持final-006，不重新构建未变产品。

生产链：原PopupButton打开→NativeGraphicStylePresenter.OnOpened Attach/PrepareForVisibility→原数值TextBox修改→NativeNumberCoordinator.OnLostFocus排入Input优先级→TryCommitPending→正式DeclaredFieldState。关闭经GraphicStylePresenter.Close保存pending并取消过期回调；重开恢复同状态pending；根捕获依据HasPendingEdits与UI-only基线拒绝非法值；真实Host取消退役草稿，确认仅接纳基线与正式字段。

测试复用完整14组实际BlobHost；新STA调用CreateDesktop/SetThreadDesktop后创建真实WPF窗口，不SwitchDesktop、不操作用户程序、不SendInput到用户桌面。样式两个原数值TextBox之间调用Focus/Keyboard.Focus，观察系统自然分派的Got/LostKeyboardFocus以及user32 GetFocus和Popup HWND；不手工RaiseEvent。分别对非法文本和恢复原文转移焦点，断言pending/捕获拒绝或恢复；另保留关闭重开和真实Host取消/确认/保存回归。文本写入为程序提案，鼠标/物理键盘输入不计已验。

若GetFocus为零或无法转移焦点，记录环境限制并停止该路径，不放松断言或回退用户桌面。只有复现明确产品缺陷才修改产品及重建；否则仅提交测试和结果。需保留失败与读取租约/退役等既有强断言。

## 本轮终态（不继续扩大取证）

产品无修改，仍为5ccbd2a9/final-006；主线正式对应15171638。两个诊断源码均由runner在对应attempt/cases.cs.txt完整固定，SHA见B08_STYLE_FOCUS_OBSERVATIONS_20261101.json；不将本轮诊断入口混入正式验收轨。

- attempt-008：测试编译0错；180秒预算后runner终止自有进程，句柄59564终态exit1。run.log有13组通过，最终既有Affine组未终结，不称14组或整轨通过。
- 008的自然WPF焦点观察：原opacity文本Focus后Got=1；非法原文转移到另一个原数值文本后Lost=1，pending仍保留并拒绝真实Host捕获。关闭重开仍保留；重聚焦Got=2，恢复原文再转移后Lost=2，pending清除且真实Host可捕获。未手工RaiseEvent。
- GetFocus一直指向owner HWND（212409158），而Popup HWND由204345270重开变204410806。不能仅凭WPF焦点观察宣称原生Popup用户输入已验。
- attempt-009：定向模式S4_BLOB_STYLE_FOCUS_ONLY=1，测试编译0错，PID44868/句柄78585终态exit1。SetFocus试图聚焦Popup HWND204935094后Popup关闭，GetFocus由448403170变36045546；原生焦点强断言失败立即停止，后续非法pending取消专项未执行。
- 生产GraphicStylePresenter已有owner.Deactivated关闭路径；强行SetFocus到不激活弹层不同于真实用户点击。观察不足以认定产品缺陷，也不能证明环境永久不支持真实输入。按任务边界停止，不切用户桌面、不注入用户鼠标键盘、不修产品。

全部日志根：D:/MyWork/line-aiproject/blob-supported-window-20261101/Release/_validation/S4-20261025/BlobSupportedWindow/attempt-008及attempt-009，各有build.log、cases.cs.txt、run.log、run-error.log、style-pending-trace.json；009另有receipt.json。外层日志在Release/_validation/BlobSupportedWindow-20261101/window-focus-008.log及window-focus-009.log。

覆盖边界：008真实Host一般取消、确认、历史保存重开和执行组已完成；“非法样式pending取消后重开”新增组在009尚未到达，不能标为通过。实际鼠标/物理键盘及系统用户输入仍未验；上一批006/007完整14组证据不被本轮失败覆盖。
