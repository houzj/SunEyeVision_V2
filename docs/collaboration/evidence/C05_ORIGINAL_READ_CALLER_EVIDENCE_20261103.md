# B08 C05 回读调用者、参数读取与单值合同补证

接续 `B08-C05-C06数值模型订阅回读集中证据-20261103.md`；只读原 VM 4.4 文件与静态 CIL，未加载/执行目标程序集、VM 或 SDK。CIL 使用 dotnet-ildasm 0.12.2 离线生成于 `analysis/c05-caller-cil-20261103/`；范围内未命中不等于全安装、反射动态生成或 native 全局不存在。

## 调用者与原件范围

- 查了 Blob 实际安装目录：包含 `IMVSBlobFindModu.dll`、`IMVSBlobFindModuCs.dll` 与 XML，**未发现 `IMVSBlobFindModuControl.dll`**；后者不存在于所列目录不排除通过其他注册路径加载其他控件。`ModuleResultView.InitSpecialParams` 只有能找到/加载对应 Control 程序集，且存在实现 `ISpecialParam` 的类型时，才创建参数根并 `GetItemsValueUnit`；这是专属参数初始化分支，不能把它当每次运行完成的回调。
- 对 `IMVSBlobFindModuCs.dll`、`VMControls.WPF.dll`、`VMControls.WPF.Release.dll`、`VM.Core.dll`、`Apps.XmlParser.dll`、`VisionMaster.exe` 分别离线反汇编；对以上完整 IL 以文本模式查三个名称的 `call/callvirt` 及 `ldstr` 精确字符串。只在 Release 的 `VmParamsConfigControl` 三个对应方法各找到对 `ParamsConfigControlBase` 的转发（`release.il.txt:3139,3151,3164`）；其余本次所选 CIL 未找到直接调用或同名字符串。以上检查不覆盖动态组合字符串/反射参数、其他程序集、P/Invoke/native 或反编译器无法解码的区域。方法体存在以及包装可调用均不证明 Blob 每次运行触发。
- `ModuleResultView` 的所核回调与 `ParamsConfigControlBase` 打开根时回读分别见前一报告；结合上述有限搜索，当前**不能以 VM 原版合同规定一次运行或私有运行完成必定 UpdateAllItem/UpdataTab/NotifyItemValueChanged**。下一步若需扩大覆盖，应按实际装配根目录的程序集引用/接口 MemberRef 做静态清单，再对含相关 token 的真实消费者解码；无需为此启动 SDK。

## 普通参数与绑定接口的明确分离

`analysis/decompiled/VM.Core/VM.Core/ModuleParam.cs:21-58`：普通 `IUserStringData` 索引 getter，在 parent 未 Dispose 时，对一般模块调用 `ServerSDKManager.mModuleManager.GetParamValue(Parent.ID,key,ref strValue,bThrowException:true)`；仅 ModuleSet 且 key 以 `%` 开头转动态参数读取。`VM.PlatformSDKCS/ModuleManager.cs:749-762` 默认缓冲长度 4096，调用 `ServerSDKAPI.IMVS_GetParamValue`，非零且要求抛错时抛 `VmException`。parent 正在 Dispose 时 getter 返回初始化空串。该入口是普通参数获取，**未证明值一定是订阅源本轮执行值**。

`VmParamSubManager.SetModuleParamBinding/GetModuleParamBinding`（`analysis/decompiled/VM.Core/VM.Core/VmParamSubManager.cs:35-140`）单独设置/查询绑定列表，保存目标模块、参数、索引类型等；`ValueBase.SubData` 查询它来显示身份。其 SDK 接口不是上述 `GetParamValue`。`VmModule.IParamsOperator.GetParamsForConfig:5714-5727` 可批量读取 `GetParamList` 建参数字典；`NameValueItem.IsGetFromDictionary:1098-1113` 才按标志/缓存选择来源。普通 `GetParamUnitByZmq:1044-1085` 的已读正文直接通过 `userStringData` 索引或父项接口读取，不能把 `IsGetFromDictionary` 的缓存逻辑泛化为该方法所有分支。

## 单值读写与来源切换：可实施的有界合同

| 情形 | 原有界路径/状态 | 实施核对 |
|---|---|---|
| 无读权限 | `NameValueItem.GetParamUnit:1034-1042` 调 `OnGetValueCompleted(false)`、返回 -2；base 回调发 null EventArgs。 | 禁止把 -2 当作读到零；保留调用/状态与 UI 值。 |
| 普通读取成功并通过 Check | `GetParamUnitByZmq` 获取字符串；只有 `Value != empty` 才先 `OldValue=empty`、再 `Value=empty`，后者进入 Integer/Float `CurValueA` setter；相等则两者均不在此分支赋新值。最后发 `OnGetValueCompleted(true)`、返回0。 | 数值引用模式本身不会令此普通读值路径提前 return；若确实调用且读出新值，仍按模型 setter 处理。 |
| Check 或 Value 更新内层异常 | 内层 catch 发 `OnGetValueCompleted(false)` 并调用 `SetParamUnit()`；若后者未向外抛，方法随后仍发 `OnGetValueCompleted(true)` 并返回0。`OldValue=empty` 发生于 Value setter **之前**，因此 setter 失败时不能笼统称旧值完全保持。 | 分开记录读取、检查、模型赋值、回写和通知；不能以返回0断言成功采用新值，也不能把原潜在双通知改说成原子回滚。 |
| 外层读取或内层处理向外抛 | 外层 catch 置 `GetParamErro`、发失败通知并返回 -1。 | 区分无读权限、读异常及校验异常；防御性回滚应标自研。 |
| 选择绑定 | `SetModuleParamBindOperation.Operate` 根据文本常量/外部/变量与索引调用 `SetModuleParamBinding`；目标不存在、长度超限等提前返回；底层绑定返回 false 且非解绑时提示并 `SetPropertyValueByNameNotrigger("CurValueA",null)`。 | 绑定身份提交与普通参数数值读回分离；失败分支不能默认旧关系与 UI 始终原子一致。 |
| 切引用为常量 | `ValueBase.IsSubA:27-72` 先 `IsSub=value`，仅 `VmModule!=null` 且 `_SubData` 已实例化且关联文本非空时清 `_SubData.CurValueA`、置 `IsGetFromModule=true`、调用本端 `GetParamUnit()`，之后顺序置 false；同一 try 之后写 `UserData["@"+ComName]`。 | 标志恢复**不是 finally**；若清绑定或回读期间抛出，catch 记录异常，后续重置及写 UserData 可被跳过，但 setter 末尾仍发 `IsSubA` 通知。普通 GetParamUnit 已处理大部分异常返回码，因此不要把每次失败都推断为标志遗留。 |
| 隐藏触发删除 | `ValueBase.CustomVisibleA:169-199` 在隐藏且删除条件、`_SubData`、非空来源同时满足时执行清关联、true→回读→false；该 setter 中此段未见独立 finally。 | 处理隐藏条件与关闭/取消分别建模，不能推成所有隐藏都清绑定。 |

`Integer` 的 int/步长、`Float` 的 double/ToEven 及 UI 事件保留前一报告，不再重复。**可交付范围**是上述静态可证单值合同；本机版本生产回调是否还通过其他方法更新参数、native `IMVS_GetParamValue` 在已订阅时返回何值仍未证。若必须继续实施，运行完成后不擅自自动回读作为与原等价的陈述；将来源值→显示值的刷新策略隔离为自研适配，标记影响和替换点，等待真实调用者 CIL、native 函数或隔离环境证据。

## 身份

`IMVSBlobFindModuCs.dll` SHA-256 `3106AF2E9344A97288553BFBBFA86AFD81E2384188B452734F56825F63E0A78D`，MVID `1d550689-0379-4822-8b69-8713d2324342`；`VM.Core.dll` SHA-256 `936763F6C8A1BF9FCF59F22ACEBCA8FEB09B5F3D5701AE77C82BDC81E626190D`，MVID `290025bc-e040-4cd5-94dd-cd49b9af9c99`；`VMControls.WPF.Release.dll` SHA-256 `C016BD0A78681672A6615CEEBB4BC4B9F114C4F7037771A6CD9348309655B9B6`，MVID `5f9126e4-1370-4549-8835-ca81604c764b`；`VisionMaster.exe` SHA-256 `42E2437780EC21C9D1ADE2D07ACE0A26635961F5C90559934A7FDE7E233E0C23`，MVID `77c739a5-b0ec-4834-8a1e-64dd9b618ae5`。WPF 与 parser 原件 SHA/MVID 见前一报告。静态身份不证明运行时装载。
