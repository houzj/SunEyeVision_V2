# Blob分析 工具逆向主文档

- 稳定工具 ID：`imvsblobfindmodu`
- 原厂工具：`IMVSBlobFindModu`
- 原厂类别：Location
- UI 组装类别：结果视图（result-view）
- 逆向优先级：P2
- 当前状态：source-partial（环节级部分确认）
- 原厂目录：`D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\Location\IMVSBlobFindModu`

## 逆向范围

资源身份、UI 组装、参数与绑定、输入输出、Process/ProcessAlg、结果与判定、保存恢复、停止释放、运行宿主，以及我方 `tools/SunEyeVision.BlobAnalysis` 的接入差异。

## 已登记清单

| 项 | 数量 | 口径 |
|---|---|---|
| 参数 | 105 项（含 8 个引用组） | 原厂 XML 参数树 |
| 输入节点 | 39 | 活动节点含组合与叶项，不是互不重叠的输入值 |
| 输出节点 | 95 | 同上 |
| UI 控件 | 94 | `AlgorithmTabRoot` 控件别名声明 |
| XML 资源 | 6 份 | 均带 SHA-256 |
| 页签 | 3 个 | Tab_Basic Params / Tab_Run Params / ResultShow |

具体数据见同目录 JSON 附件。

## 当前证据状态

- 7 个分析环节：ui / common / parametersRoi / execution / display / storage / native 全部为 **partial**，每条都写明已达到的证据级别与缺口。
- 9 步语义链：4 步 confirmed（parameters、process、resultWrite、persistence），5 步 inferred（init、inputsRoi、branchFailure、displayPublish、release）。
- 7 维算法对照：4 维 partial，3 维 not-established（边界坐标类型、像素数值、验证基线）。

已确认的是**调用边与静态声明**，不是算法等价。原生 `1805fd2c0` 的产出仍未取到。

## 已有逆向证据

| 文件 | 内容 |
|---|---|
| `docs/collaboration/evidence/BLOB_WHOLE_TOOL_VM_20261031/原厂整工具证据链.md` | 身份哈希、8 段调用边、迁移边界 |
| `docs/collaboration/evidence/BLOB_WHOLE_TOOL_VM_20261031/原厂参数资源索引.json` | 96 项参数声明与引用组 |
| `docs/collaboration/evidence/BLOB_WHOLE_TOOL_VM_20261031/原厂输入输出声明.json` | 39 输入 / 95 输出活动节点 |
| `docs/collaboration/evidence/B08_BINDING_ORDER_20261031/B08参数绑定执行顺序原生证据-20261031.md` | 绑定登记与执行链顺序 |
| `docs/collaboration/evidence/B08_BINDING_ORDER_20261031/B08绑定序列化及重载顺序-20261031.md` | 保存树遍历与导入顺序 |
| `docs/collaboration/evidence/B08_NATIVE_DECISION_20261030/B08-Blob低阈值原生参数对象追踪-20261030.md` | 单参数原生读写路径 |

## 引用但未随库迁入的细项证据

以下文件在既有证据链中被引用，但当前仓库内不存在。结论已在卡片中保留，出处标注为「未随库迁入」，不得当作可复查证据：

- `VM-Blob参数窗口组装与界面实施证据-20261029.md`
- `B08-填充面积阈值静态证据-20261031.md`
- `B08-连通方式静态证据-20261031.md`
- `B08执行订阅注入边界-20261030.md`
- `B08形状筛选执行与量化值校验-20261031.md`
- `B08-Blob图像与二值图输出静态证据-20261031.md`
- `B08结果列表角度与长短轴映射-20261031.md`
- `B08与图像源受控探针隔离审计-20261030.md`

## 下一步

1. 补齐上述未迁入的细项证据，或在卡片中把它们降级为 inferred。
2. 追 `1805fd2c0` 的产出，闭合阈值、填孔、连通与特征公式。
3. 取得隔离 Windows 环境后做原机差分（阈值边界、非矩形 Domain、输出像素、绑定生命周期）。
4. 以同一张图与同一组参数建立我方差分基线，替代「HALCON 管线可用」这类非等价结论。
