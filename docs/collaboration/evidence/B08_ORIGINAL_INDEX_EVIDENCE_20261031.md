# B08 数值引用索引：已证边界与受控补证方案

仅只读分析 VM 4.4 已有反编译和 attempt03 记录，未启动 SDK、窗口或修改共享状态。范围为普通参数引用、Blob LowThreshold 消费；显示字符串、托管提交结构、保存后恢复和消费结果分开记录。

## 原托管代码能够确认的内容

`Apps.XmlParser.ParamTab.ButtonSelecter` 选普通 VmIO 时，如果旧表达式包含 `[`，保留首个 `[` 起的后缀；否则在 `ModulePathName` 后追加 `[]`。这只说明界面字符串的生成，不说明后端消费全部项。

`SetModuleParamBindOperation.Operate` 对非空普通变量路径先拆分第一个 `[` 及其后缀，再解析源 VmIO；满足前述分支及来源解析成功时：

| 输入表达式 | 托管层提交 | 回读格式（GetModuleParamBinding 按已存 indexType 重建） | 消费语义 |
|---|---|---|---|
| `源[]` | `SUBSCRIBE_INDEX_ALL`、常量字段 `All` | 来源路径 + `[]` | 多值/单值如何消费未知 |
| `源` | 同一普通来源分支中索引文本为空，也提交 `SUBSCRIBE_INDEX_ALL`、`All` | 若后端返回 ALL 则重建为 `源[]`；并非实测持久化 | 未知 |
| `源[0]`、`源[1]`、`源[2]` | `SUBSCRIBE_INDEX_CONSTANT`，索引字段分别为字符串 `0`、`1`、`2` | 若后端返回对应 CONSTANT 则拼成 `[0]`、`[1]`、`[2]` | 多值索引基数和对应项未知 |
| `源[索引源路径]` | 方括号内容含点且索引 VmIO 可解析时，提交 `SUBSCRIBE_INDEX_VARIABLE` 与索引源 Root.ID/Name | 若后端返回 VARIABLE 且索引对象能解析，则重建其 ModulePathName | 数组/标量索引源、取值与越界未知 |

`VmModule.SetRunParamBind` 的迁移/恢复路径有相同的空索引→ALL、数值→CONSTANT、含点→VARIABLE 分支；它不是所有输入必经的证明。特殊外部通讯、数字常量和 `TRIGGER_STRING` 在前面的分支单独处理；空字符串是解绑，不能与 `源[]` 混同。`GetModuleParamBinding` 展示来自 SDK 返回结构及当时对象路径，不能将显示表达式当作原生持久化字节格式或保存成功的证据。解析不到源/索引、64 字符上限、SDK 失败、索引对象失效等分支另有各自行为；不把一般情况外推到这些分支。没有证据证明多项/范围输入是有效能力。

## 已保存原机样本

`D:/MyWork/SunEyeVision_V2/Release/_validation/B08OriginalBinding-20261030-attempt03/B08BindingRuntimeProbe.cs` 对单值局部整数 `%B08Var%` **直接调用** SDK 绑定，固定索引字符串 `0`：来源 173 时 Blob LowThreshold 回读 173、结果 1 块；源变 90 回读 90、结果 2 块；成功绑定保存/重开仍能消费。此实例只证单值 `[0]` 的有效表现，不能证明多值下标基数或 UI 输入 `[]`。

同一探针另设索引 `99`，观察到结果项 `<null>`、参数回读保留先前成功值 173；日志含 `0xe0000314`。它不证明所有越界返回相同码，更不能由该码断言唯一根因。直接 API 解绑后保留 173 是这次样本的另一个独立结论，不覆盖 UI 额外清理。

## 待证与隔离测试设计

需要可控三项整数来源，例如 `[37,90,173]`，确保通过原 SDK 独立读回实际项数和顺序，建立原 ImageSource→Blob 控制连接、固定原始测试图像和算法参数。每个实验使用新的自有方案和输出目录；预先清点 ModuleProxy 共享历史清单可能删除的目标、并发和 PID 条件，另行获得**当前窗口**的独占授权并确认结束时清单影响；不能把历史独占授权当本轮授权。否则仅静态追踪，不启动 SDK。

1. 分别使用 `源[]`、`源`、`源[0]`、`源[1]`、`源[2]`，通过真实托管界面操作链和直接 SDK 结构分别记录输入表达式、提交的 indexType/字段、返回码、原始绑定列表和显示回读。无括号可能被界面规范化；记录原始操作，不强行模拟 UI 已支持输入。
2. 保存并重开各独立方案，记录保存前后原生绑定结构、显示文本、成功/失败，避免把 `GetModuleParamBinding` 字符串当文件格式。每轮在运行前先写入显著不同的 LowThreshold 标记，并对照读取标记值、执行状态、错误码、当前值与 Blob 数量/面积/掩膜；输出失败时不得只凭旧参数回读说消费成功。
3. 同一个多值来源依次改变各项为不同值，观察对应固定索引的消费；补单值 `[0]`、`[1]`、`[2]`、`[]`、无括号及多值越界 `[3]`、`[99]` 和负数入口可达性。三项以上、索引基数和相等值需独立验证，不从表达式名称推断。
4. 动态索引分别用单值整型来源 0/1/2/越界及失效状态，记录索引源自身原始读回、目标实际消费和执行错误；多值索引源另列为未知，不预设取第一项。每例记录输入方案、源码、图像、观察和日志 SHA256，保留所有失败、不修改其他任务文件。

## 原件与证据身份

- 安装版 `Applications/myLibs/VM.Core.dll` SHA256 `936763F6C8A1BF9FCF59F22ACEBCA8FEB09B5F3D5701AE77C82BDC81E626190D`；`Apps.XmlParser.dll` `78EC7AE1E5ADEFFF29197099C7109B153A3F75D092B7C3E9DC253D8C1B25A8D1`。身份记录来自既有 [来源树审计](VM来源与索引选择树生产消费链-B08-20261030.md)。
- `analysis/decompiled/VM.Core/VM.Core/VmModule.cs` `53E2F0F9032C4631B59CF0EB2A00E06D50469D94706E7E370E9CC7E1059C72CE`；`VmParamSubManager.cs` `42B0363C9A4FF1376FC5980865ACE43FBAE7D5A2FDF00156C55CEFC5CCC6703E`。
- `analysis/decompiled/Apps.XmlParser.CapabilityAudit/Apps.XmlParser.ParamTab/SetModuleParamBindOperation.cs` `D77BB91862138A680729E5DD18E14F7A5564E7D52F4A65AE394E9CC74460E9E4`；`ButtonSelecter.cs` `A04C04DC557328269A06D67C194F6C3A64B68659ACC1F76E4DE778F5DA858931`。以上为反编译文本哈希。
- attempt03 的 `B08BindingRuntimeProbe.cs` `255FB3248F02A473AC3F9DA303B8E73A1FC2521490867A44D9A725FCE59BCDA0`；`b08-observations.json` `A8A229568B813D556D2304D3AB4902A067B1D4CC816607058578965D64D6FCD2`。技术总监侧审计 `docs/collaboration/evidence/B08_ORIGINAL_BINDING_RUNTIME_AUDIT_20261030.json` `065B75B82584C730D646418B0EC7A9CC65C2D57629EDADBEA3C0CD4E7D612ED2`。
