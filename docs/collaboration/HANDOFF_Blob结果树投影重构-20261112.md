# 交接单：Blob 模块结果树「转置投影」重构（2026-11-12）

> 新会话第一句发：**按 `docs/collaboration/HANDOFF_Blob结果树投影重构-20261112.md` 继续**
> 读完本单 + `CURRENT_TASK.md` 顶部 + `evidence/Blob模块结果树原件结构-20261112.json`，直接开工，不要重新调研方向。

## 0. 一句话目标

让「模块结果」页签的**显示项、顺序、分组、值形式**与 VisionMaster 4.4.0 完全一致；根因已定位为**数据模型到树的投影方向错了**，本轮任务就是把投影方向改对。

## 1. 工作区与分支

- worktree：`C:\Users\Administrator\.codex\worktrees\param-window-datatree-project-v2\SunEyeVision_V2`
- 分支：`feature/param-window-datatree-codebuddy`（与 D 盘 worktree 的 `feature/param-window-datatree` 同基线 `c5e8901df`，可快进合并；不动 main）
- 最新提交：`be0350166 fix(result): 同身份快照不再退休重建，消除打开调试窗口闪烁`
- 工作区干净（仅 `plans/plan.json` 钩子文件）
- 构建命令：
  `dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -p:HalconSourceRoot=D:/MyWork/SunEyeVision_V2/libs/halcon -v:quiet --nologo`
- PowerShell 7：便携版在 `C:\Users\Administrator\.local\pwsh7`，已由 git-ignored 的 `installer/Release.Local.props` 指向
- **构建前必须确认用户已关闭 `SunEyeVision.UI` 进程**，否则 MSB3027/MSB3021 文件锁（不要杀用户进程）
- 测试：**先全量构建（末尾封签 payload），再 `--no-build` 跑测试**；独立构建测试项目会让清单失效

## 2. 已完成（均已提交、构建 0 错误）

| 项 | 提交 | 状态 |
|---|---|---|
| 换显示图像保留模块图形与文字（坐标域不一致不再整体清空，只跳过域绑定 ROI/内接/轮廓） | `e97718b2c` | **用户已验证正常** |
| 双击打开调试窗口显示正式结果叠加（`IsCurrentPresentedSnapshot` 去掉编辑器门禁） | `e97718b2c` | 用户已验证"会显示"，但**曾闪烁** |
| 打开调试窗口闪烁修复（同快照身份直接 return，不 retire 重建） | `be0350166` | 待用户验证 |
| 结果项顺序改回 SDK 声明序（`OutputDisplay` 只给显示名） | `1f1ffa9de` | 待验证 |
| 树值回裸值（模块状态=1，撤回树内枚举中文化） | `2ad83dadd` | 待验证 |
| 标量数组一行 `;` 拼接，取消 `[i]` 子节点与「加载下一页」行 | `2ad83dadd` 前一轮 | 部分（仅标量序列） |
| 6 张原件的完整项集/顺序/分组清单 | `96762a2db` | 已登记 |

## 3. 剩余改造（本单核心，按序做）

### 3.1 现状 vs 原件（用户截图 + `Blob模块结果树原件结构-20261112.json`）

我们现在：

```
▾ 质心点                     [45 项]
  ▾ 质心点 [All]              All · 按声明绑定全部元素…
    ▾ [0]  ▾ 质心X / 质心Y
    ▾ [1]  …
```

原件：

```
▾ 质心点
    质心X    631.70703;633.13763;1020.406;162.8699;…
    质心Y    383.29745;860.26086;693.6305;688.5462;…
```

即：**VM 把 `BlobsPort` 记录「按成员转置成列」**（每个成员一行，值=全部 Blob 的数组，分号拼接）；**我们按元素展开成行**（`[0]/[1]` + `All`）。这是投影方向问题，不是渲染细节。

### 3.2 改造清单

1. **`OutputDisplayNode` 增加 member-path 行**
   - 文件：`src/Plugin.Contracts/OutputDisplayNode.cs`（或同目录定义处）
   - 新增：一条显示行可指向"父端口 + 成员路径"，并支持子级分组（如 `质心点` 下 `centroid-x`/`centroid-y`）
   - 兼容：现有按端口 id 的行保持可用，`Freeze` 校验同步

2. **按 member 投影取值**
   - 文件：`src/Plugin.Wpf/Controls/Native/Composition/Results/PropertyList/ResultPresentationModel.cs`
   - 新增 member 行节点：从一个记录序列端口（如 `BlobsPort`）取某成员，产出"元素数组"，交给已有的 `;` 拼接显示
   - 顺序沿用上一提交的**声明序**（`schema.Outputs`）；`OutputDisplay` 提供名称与分组，不参与排序

3. **去掉树里 VM 没有的行**
   - `src/UI/Services/Monitoring/ResultPresentationPresenter.cs`
   - 删除 `LoadPageCore` 中 `CreateAllMembers()` 的 All 行创建与 `DisplayName` 的 `[All]` 分支
   - 记录序列不再生成 `[i]` 子节点（改由 member 行承载），删掉「加载下一页」行（`LoadPageCore` 后半段）

4. **重写 Blob 显示声明**
   - 文件：`tools/SunEyeVision.BlobAnalysis/Runtime/Results/BlobOutputDisplay.cs`
   - 按 `evidence/Blob模块结果树原件结构-20261112.json` 的 `items` 顺序/名称/分组**逐项重写**
   - 名称修正示例：`灰度对比度`→`对比度`、`检测矩形`→`检测区域`、`扇环检测区域`→`ROI圆弧`、`轮廓点`→`轮廓点集`、`Blob主轴角度`→`主轴角度`
   - 结果表继续沿用 `Runtime/Resources/BlobDisplay.xml`（列定义已与原件一致，勿改）

5. **重写旧结构用例**
   - `tests/SunEyeVision.UI.Tests/ResultPresentationPresenterTests.cs`：12 条按旧分页/子节点模型写的用例（`Large_sequence_creates_only_clicked_pages`、`Publication_replacement_during_lazy_page_guard…`、`Switching_request_or_schema_retires_old_rows…` 等）
   - 原则：**先按原件改断言，再改代码**；在证据里登记"撤回原因"

## 4. 验收方式

1. 构建 0 错误
2. 用户人工对照 6 张原厂截图，逐项核对：**项名 / 分组层级 / 顺序 / 值形式（裸值、`;` 拼接、float32 自然位数、图像项空值）**
3. 双击打开调试窗口：**不闪烁**、图像+矩形+文字同时出现
4. 结论写回 `CURRENT_TASK.md` 与证据 JSON

## 5. 必须遵守的约束

- **未经运行态证据不改显示类代码**；原件优先，禁止按本方现状猜测
- 旧测试与原件冲突时先改测试并登记，不得为迁就旧测试回退原件语义
- 提交信息用中文，UTF-8 文件 + `git commit -F`（禁止命令行直传中文）
- 不控制用户桌面、不杀用户进程、不 `git reset --hard`
- 交付前自检：“这段代码对应哪条 VM 事实？”答不出不算完成
