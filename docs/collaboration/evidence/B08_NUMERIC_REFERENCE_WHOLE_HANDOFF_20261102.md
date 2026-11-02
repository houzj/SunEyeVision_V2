# B08 数值引用完整混合窗口候选交接

- 基线：93b9b683f1dd849b89f6e776839fe1a79b028f66；分支codex/b08-numeric-reference-20261102；目录D:/MyWork/line-aiproject/blob-reference-chain-20261101。旧提交/分支保留，无新增worktree。
- 产品代码/共享合同修改：无。全部现有owner、字段、冻结解析、真实Host、ROI与显示链正确部分复用；只新增专属测试、Runner S4/480秒轨及设计/完整24项矩阵/回执。
- 当前交付：已支持同一真实Blob窗口的阈值引用、面积双端模块/变量动态索引、浮点独立引用、本地检测屏蔽及逐叶继承、正式图文样式；实际私有/正常运行与发布scene、确认取消、非法pending原子捕获、UndoRedo、Copy、保存重开执行、失效恢复、同ID新owner与旧请求、无CurrentValues真实新文档重开均有本批限定证据。
- 构建：候选标准UI/full均自然exit0，HALCON源依赖为主仓libs/halcon，未复制正式Release。
- 最终测试：Release/_validation/S4-20261025/B08NumericComposition/attempt-013/receipt.json，pid127428自然exit0，8组，预算480秒。测试源码SHA120A89A43FFF15E0C88DE937971908FF9D857D0596B4A02A3A4B2F3B5AC76664；15个实际Loaded DLL路径/SHA全部匹配候选。完整源/载荷/日志哈希在B08_NUMERIC_REFERENCE_WHOLE_CANDIDATE_20261102.json。
- 未消费解绑137、消费后173与无值显式149均取各自owner合同，不以默认100/最近输出代替。Redo配置Fields/完整Selection/BindingOrder严格回放，当前值按Undo后live种入并经真实重跑接纳。原009前后快照保留，不再要求重放历史运行值。
- 失败保留：001/002错误旧边Origin，004/005圆度高级未加载，006/007模型/UI类型，008/009误比较运行值历史，010过宽width筛选，011 CLR属性误名；均夹具原因，严格生产保护未改。003/012单组通过不代整批，当前结论取013全八组。
- 未验：VM全部解绑/恢复值/时序等价，自然键鼠、系统焦点/剪贴板、VS/其他DPI；混合冻结后变值及多参数后项失败部分接纳/算法异常未新增直接验证。原C07旧容器事件/三范围16组等只作历史限定复用，不拼成同版全交叉。A20未知输出/Group、A21偏好持久化与连续会话缺基础继续保护；不称完整24项无条件闭口。
- 不操作主仓/正式Release或用户方案；主线集成时排除候选CURRENT_TASK，正式复验后由主线维护原主方案。

## 准确运行命令

```powershell
Remove-Item Env:B08_NUMERIC_COMPOSITION_CASE,Env:S4_BLOB_DISPLAY_CASE,Env:B08_REFERENCE_CASE_SCOPE,Env:S2_DIAGNOSTIC_CASE -ErrorAction SilentlyContinue
& scripts/verification/Run-S2ReleaseValidation.ps1 -Stage S4 -Track B08NumericComposition -ValidationScope Candidate
```

默认480秒；新CreateDesktop+STA隐藏桌面，不SwitchDesktop、不回退用户桌面、不操作正式试用窗口。WPF按钮/命令/Text提案不是物理键鼠；本地ROI经正式输入提案而非手势绘制。正式同版复验在主仓将ValidationScope改为Formal；不把候选结果当正式。
