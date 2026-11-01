> 原报告及相对 native / annulus 证据目录：C:/Users/houzhongjie/.codex/visualizations/2026/10/19/01a15280-de08-7361-80d8-92d9b1ddeb25/roi-20261101/Blob-ROI-inheritance.md；原报告 SHA256 E53EE5EAF853530065E6DBCE18B8B517D0E4E4FE9840C8D2A4E744A338DC5E09。

# Blob 四类 ROI 继承：值读取、索引和坐标静态补证

范围 VM 4.4.0 Build20250516@1555331。只读原始模块和既有 Ghidra 工程，未运行 VM/SDK，未修改产品工作区。原 Blob 模块 SHA-256 `4DE695D8C3051045C9010739686F94C32D66A36EE486207FE749FB183FFF4C9A`；输入 XML `F1B2F279717DCE3EC61C11C41F858BD100359095BCF13672422A3C73EFE47F6C`；共享 `Group.xml` `9129B82A0F9906DDE591347498A80E2E8C019C0FB959A721ECFD24FBBB5207DC`。

## 已证的四类入口

共享 `Group.xml` 中继承方式 0 是 `ROIBOX` 组合 `InputROI`；1 是五个 float 叶 `roicenterx/roicentery/roiwidth/roiheight/roiangle`；2 是 `ROIANNULUS` 组合 `InputROIAnnulus`；3 是六个 float 叶 `roiAnnulusCenterX/Y`、`roiAnnulusInnerRadius/OuterRadius`、`roiAnnulusStartAngle/AngleExtend`。组合与逐叶都落到这些实际叶，但逐叶不要求来源来自同一模块或同一图像帧。见 [既有原资源与入口报告](D:/MyWork/REA逆向测试/docs/joint-audit/B08-ROI继承与位置修正静态证据-20261101.md)。

`SetCombinationSourceOperation.Operate` 先解 `[...]` 并按路径找来源，找不到时提示并返回；目标存在时先 `RelateIO=source`，再 `RelateIndex=GetBindIndex(...)`。逐叶 `SetFilterSourceOperation.Operate` 同样先解析目标和来源，再依序设置这两个属性；无效常量先校验并返回，来源解析失败尝试还原选择器旧值。`GetBindIndex` 对含点文本查索引 IO、数值文本建 `VmConstantIO`，解析失败提示但返回 null；不能将这一提示等同事务回滚。

`VmIO.RelateIO` 设置先解除旧关系、再订阅新关系；订阅失败会清 `_relateIO` 并抛出。`RelateIndex` 设置先对现有来源解除订阅，递归设置索引，再重新订阅；重新订阅抛异常时清 `_relateIO/_relateIndex`。组合订阅在两端叶数相同时按子项顺序递归，数量不一致抛 `SubscriptionsNotAligned`；不证明中途失败时所有此前已设子项会回滚。`VmModule.GetPubInfo` 把未指定索引编码为常量 `All`，常量索引使用名称，变量索引记录来源模块 ID 和名称。证据分别为 `SetCombinationSourceOperation.cs`、`SetFilterSourceOperation.cs`、`VM.Core/VmIO.cs:121–204,268–310`、`VM.Core/VmModule.cs:1823–1878,1913–1950`。关联变更后是否存在其它撤销机制或原生部分副作用需另证。

## 原生取值和失败

`CVmAlgModuleBase` 的 `18000e810` 先调用 `18001b8c0` 取得输入图像及宽高，失败返回 `0xe0000510`，宽高小于 1 返回 `0xe0000001`；随后通过 `18001c790` 尝试矩形 ROI。矩形结果类型为 2 时装入矩形列表，否则再调用 `18001bf70` 尝试扇环 ROI；两者读取失败由上层转为 `0xe0000511`。这说明同一已证路径的读取/优先顺序，不等于证明所有模式和图像帧校验。

- `18001c790` 首轮依次以 index 0 查询五个 `VM_M_GetFloat`，使用第 5 个参数收集数量；若所有返回均为 `0xe000031b`，标记状态 1 并正常返回。必需的中心、宽、高查询均成功时，从五个返回数量取最小值，上限 64；每个 i 再读五个同序号叶。`0xe000031c` 在该函数映射状态 2，循环遇状态 2 返回 `0xe0000001`；角度状态非成功且非状态 2 时角度可置 0。`param_5=0` 时循环只处理首项，非零才继续至最小数量/64。实际 `VM_M_GetFloat` 下层索引映射与异常来源不可仅凭名称推断；伪代码省掉栈传第 5 参数，原汇编 `18001c892 MOV [RSP+0x20],RAX`、调用后取数量，按 x64 ABI 核验。
- `18001bf70` 首轮读取六个扇环叶；全部为 `0xe000031b` 则状态 1 正常返回。其余路径要求初始中心 X/Y 和外半径成功；按六个返回数量最小值与 64 上限控制索引遍历，`param_5=0` 则仅首项。循环中中心 X/Y 和外半径非成功返回 `0xe0000001`；内半径非成功置 0，起始角非成功置 0，跨度非成功置 360。`param_6 != 0` 时跨度限制在 -360 到 360；起始角在允许数值范围内通过 ±360 归一化。以上是该反编译路径、标志与返回码条件下的行为，不等于所有缺失输入均可用默认值；初始数量为 0 的分支和每个 ABI 隐藏参数仍应独立验证。

## 角度、坐标与图像帧

原矩形叶的数值被限制在 `[-10000,10000]`，通过加减 360 归一化至 `[-180,180]`；扇环起始角相同，跨度在上述条件下限制 ±360。上述常量经原 PE file offset 和 float32 读取复核：`18002fa28=180`、`18002fa2c=360`、`18002fa50=-360`、`180030140=10000`、`180030144=-180`、`180030148=-10000`。**推断**：这些数字强烈支持该入口采用角度制；但本次没有找到该具体继承输入字段明确写“度”的接口规范，不将其升级为经原机验证的单位合同。`MVDShapeCpp.h` 对矩形声明范围 `(-180,180]`、顺时针正，不等于已经验证该入口全部边界或扇环方向。

`18000e810` 传给两个读取 helper 的第二参数包含从输入图像及条件性替代值构成的尺寸，但两个 helper 在入口保存 RCX/R8/R9 后，即用自身常量覆盖 RDX，当前已读函数体未见利用传入的第二参数进行尺寸或图像身份比较。该发现仅限这两个 helper，不排除其前后或被调用层做其他检查。用户绘制 ROI 的另一分支可见将内部归一化几何坐标乘图像宽高的操作，不能反推继承叶也采用归一化坐标。**当前条件下无法确认**继承数据的坐标原点/轴向、实际所属图像帧、不同来源叶是否同帧、帧 ID/尺寸不匹配校验、后续修正及 Domain 规则；不能凭控件名称或图像相似性猜补。

## 可复验证据

- 矩形 `native/0x18001c790-18001c790.{c,asm.txt}`；汇编 SHA-256 `28746E37D2E90C256A7BC68F5E24219C9B9FE6690A8B232DC175A03196678865`。
- 扇环 `annulus/0x18001bf70-18001bf70.{c,asm.txt}`；汇编 SHA-256 `964E733EAB135796C4AB5EDC5D26DD2FD2866AE5E91E90C89241AA36364D9A10`。
- 上层原反编译及汇编 `D:/MyWork/REA逆向测试/analysis/full-audit/b08-execution-inject-20261030/blob-pre/0x18000e810-18000e810.{c,asm.txt}`；本目录 `native/0x18001b8c0-18001b8c0.{c,asm.txt}`。
- `SetFilterSourceOperation.cs` SHA-256 `197EC3EB19974776DB4DC9AB293CABABED6319EF2A5A1C0DA6A8A9BFD5EB0401`；`VmModule.cs` `53E2F0F9032C4631B59CF0EB2A00E06D50469D94706E7E370E9CC7E1059C72CE`；`VmIO.cs` `15E1832243422B6BFA2714F300B796D54C365950573639EB1039C5DF58E99CB0`。
