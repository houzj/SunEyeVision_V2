# B08 整数绑定索引解析静态追踪

范围：VM 4.4 原 `ModuleFrame.dll`，SHA-256 `45C57BC46F72FD1F9E0B3A6F9183FF974D14590CFD05ECF4E9FA97A4FF95AE9B`。仅复核既有反编译和汇编，未运行 DLL、SDK 或用户实例。下述结论只覆盖已核的整数绑定路径。

## 索引解析器

`FUN_1800bb4d0` 的来源类型 3 分支调用 `FUN_1800bca20` 获得索引，再将其传给 `FUN_1800ca500` 读来源整数。反编译器将 `bca20` 标成 void，必须看原指令的 EAX 返回：

| 索引记录首个整数 | `FUN_1800bca20` 指令行为 |
|---|---|
| 0、1 | `0x1800bca65–77` 跳至 `0x1800bcceb`，EAX 返回 0 |
| 2 | `0x1800bcce6` 返回记录偏移 `+4` 的整数 |
| 3 | 从记录偏移 `+4` 的来源 ID、`+8` 的名称读取整数，调用 `FUN_1800ca500` 时 R9D 为 0；成功返回读到的整数，失败记录日志后返回 0。某分支先查一个上下文、失败再回退另一上下文 |
| 其他 | 记录日志（若启用）后返回 0 |

原托管 `IMVS_ENUM_SUBSCRIBE_INDEX_TYPE` 依次声明 INVALID、ALL、CONSTANT、VARIABLE，值为 0、1、2、3。但 SDK 结构中的固定索引是 `strIndexConstant` 字节串，不能仅凭相同数字断言 SDK 到 native 内部记录的转换已核实。条件是这些枚举值经过相应转换到上述记录：ALL 在本整数读取路径上解析到索引 0，并非已证明复制整个数组；固定索引取内部整数；动态索引读其来源的第 0 项。用户输入文本、内部记录与这些数字的完整转换仍需追证。

### ABI 和实际入口复核

完整 dumpbin 中 `0x1800bb52e–545` 检查绑定记录首整数并仅在来源类型为 3 时进入 `0x1800bb586`；`0x1800bb5c6–5d2` 将该记录指针加 4 后作为 RDX 调用 `0x1800bca20`，立即将 EAX 放入 EDI。后续 `0x1800bb60e–61b`、`0x1800bb63b–648`、`0x1800bb66e–67b` 把 EDI 放到 R9D 传给 `0x1800ca500`，分别覆盖主/回退调用。`bca20` 中 `0x1800bcb3e–4b` 与 `0x1800bcbc9–d6` 的 R9D 均清零，故是读取动态索引来源的第 0 项，不能据此将动态索引本身固定为 0。

已核 `0x1800ba139` 调 `GetBindingInt` 路径的 `0x1800bb4d0`，`0x1800ba140–1cb` 对非零返回退出当前写路径；外层读取结果在 `0x1800bb680–684` 保存并测试，结尾 `0x1800bb87a–883` 将结果状态和 ESI 错误分别交回。这与动态索引解析失败降为 0 是两种不同层次：前者传播最终来源读取错误，后者可能先把索引错误掩盖为 0。完整执行路径、写入条件及生命周期边界仍以关联报告为准，不据此推广到其他类型或声称原机验证。

## 零基及错误边界

`FUN_1800ca500 → FUN_1800f5f40` 的已核整数路径：`0x1800f5fe5` 拒绝负索引；`0x1800f6000–6026` 取数量，只有 `0 <= index < count` 才从 `*(object+0x70) + 8 + 4*index` 读取一个 4 字节值。因而内部索引是**零基**；非空数组索引越界、负索引返回 `0xe0000014`；数量为零时越界分支返回 `0xe0000314`。名称/对象未命中也可能返回 `0xe0000314`，不能只靠该码判定原因。上述底层失败由 `ca500` 传回调用者；在动态索引读取器自身，失败被解析器转换成索引 0，随后外层来源读取仍可能成功，不能视作动态索引错误必然阻止目标参数写入。

这是静态分支，不证明 UI 输入文本、SOL 保存重开、目标为其他数据类型或所有异常分支有相同行为。具体三元素原厂实测仍缺受控隔离环境；[候选探针](B08三元素索引探针V2与离线前置审计-20261031.md) 尚未执行。对未闭合处不能直接宣称“已穷尽静态手段”；继续以 SDK 枚举到 native 记录的转换为明确追踪目标。

## 证据身份

- `analysis/global-variable-followup/frame-consumer/output/1800bca20.asm.txt` SHA-256 `D6CF4A46F578EEBCD4AE74536BD01552753174DFCBAE30D8F7CC1AFA5F5558FE`。
- `analysis/global-variable-followup/frame-param-binding-index-readers/output/1800f5f40.asm.txt` SHA-256 `340241F8120D7DD619F207C7A6715B2AB8BCDDAC1B1436336933854F7BAB6473`。
- `analysis/decompiled/VM.PlatformSDKCS.Audit/VM.PlatformSDKCS/ImvsSdkDefine.cs` SHA-256 `B0641283E9E7EF4AFAC7270D43508F37FBBA8D56D47BB49F49C332407FEDE717`。
- `analysis/vm-framework-architecture-20261030/module-frame-core-callers/output/1800bb4d0-complete.dumpbin.txt` SHA-256 `F3F28C00259E01ACB95B09E86346D85C402F36E236A059EE03EFE79DBD308D75`。
- `analysis/full-audit/b08-execution-inject-20261030/module-binding/0x1800ba0e0-1800ba0e0.asm.txt` SHA-256 `FCDEC64E06527F4F0C096DBEA35D5BF1A3ECFB3AF0BEC60DA979C3B1A79F520C`。
- 调用链和错误传播的范围参见 [B08 执行订阅注入边界](B08执行订阅注入边界-20261030.md)。
