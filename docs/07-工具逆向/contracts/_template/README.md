# 卡片模板使用说明

新增一个工具卡时，按本说明建 `tools/<toolid>/` 七件套，然后跑校验脚本。

字段契约以 `../card.schema.json` 为准，页面章节以 `../page.sections.json` 为准。本目录不放实际卡片数据，只放填写规则。

## 一、建卡步骤

1. `tools/<toolid>/` 目录名必须等于 `evidence.json.stableToolId`，小写、只含 `a-z0-9_-`
2. 复制 `tools/imagesourcemodule/` 作为参考样本（该卡是完整七件套的真实例子）
3. 逐文件替换为本次工具的数据，**不留任何上一个工具的残留**
4. 跑 `pwsh -File scripts/tool-reverse/validate-cards.ps1 -OnlyTool <toolid>`
5. 校验通过后再写入 `index.json` 并生成页面

## 二、七件套各自要填什么

### `<toolid>-reverse.md`

必含四个标题：`逆向范围`、`已登记清单`、`当前证据状态`、`已有逆向证据`。

每条结论必须写成可追的形式：文件路径 + 行号/偏移 + SHA-256 + 可确认范围。
写「未确认」比写推测有用，不要用推测填空白。

### `evidence.json`

约 22 个必需字段，见契约。三个容易写错的地方：

- `unknowns` **必须非空**。没有未知项说明还没真正取证。
- `stableToolId` 必须与目录名逐字符一致。
- `status` / `priority` / `uiCategory` 必须取自契约枚举，不得自造。

继承自 Codex 的卡片不加 `origin` 字段（缺省即视为 `codex`）。我方核实并改写的，加 `origin: "suneyevision"` 与 `rewriteNote` 说明改了什么。

`currentImplementation` 只在核对我方实现后填写。**没核对就不要填**，页面会显示「我方未核对」，这是真实状态。

### `resources.json`

`xmlEvidence[].sha256` 必填且为 64 位十六进制，`root` 必填（XML 根节点名）。
哈希用于换机后核对是否同一份原件。

### `parameters.json`

保留原厂 XML 参数树原样结构：`{ type, attributes, text, children[] }`。
**不要二次加工成扁平表**，否则丢掉层级就无法判断哪个参数属于哪一组。

### `ui-preview.json`

`controls[]` 每项必须有 `type` 与 `name`。
`missing[]` 写清缺什么（比如「显隐触发器」「主题悬停态」）。
`previewKind` 固定 `resource-control-evidence`。

### `ui-preview.svg`

证据性布局图，**不是像素级还原**。图内或同目录 note 必须写明：恢复原厂资源或运行时截图后替换并保留来源。

### `algorithm-semantics.md`

9 步固定，顺序不可变：`init`、`parameters`、`inputsRoi`、`process`、`branchFailure`、`resultWrite`、`displayPublish`、`persistence`、`release`。

每步给 `unknown` 直到拿到原厂证据。只接受反编译源码、资源、汇编/ABI、受控运行观察；产品意图、截图、自研实现不能单独证明算法语义。

## 三、状态怎么定

| 状态 | 什么时候用 |
|---|---|
| `inventory-only` | 只有 XML 资源和工具项声明 |
| `source-indexed` | 找到程序集与类型，还没读函数体 |
| `source-partial` | 至少一个环节有反编译证据 |
| `source-confirmed` | 该环节能指到具体文件与行号，且已读通 |

**不许为了好看把状态往上调。** 状态被高估的代价是下一个人按错误预期排工期。
