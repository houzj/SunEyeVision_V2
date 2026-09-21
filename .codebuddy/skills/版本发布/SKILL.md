---
name: 版本发布
description: >
  版本发布技能。把"提升版本 → 构建 → 打包 → 校验 → 清单 → tag → 变更日志"做成一条可执行 SOP：
  - 版本号只改真源 version.json 一处（rule-022），禁止硬编码
  - 打包前必须提交，且程序集版本串的 commit 必须等于 HEAD
  - 产物必须带版本+渠道，生成发布清单，按需打 tag，并同步 CHANGELOG
  Trigger keywords: 发版/发布版本/打包/打安装包/出包/升版本/内测版/外测版/公测版/Alpha/Beta/候选版/RC/release/pack/生成安装包
  DO NOT trigger on: 日常编码、只读分析、Debug 构建与启动、仅讨论版本策略而不做发布。
---

# 版本发布 — 单一真源 + 可校验的发布链路

## 1. 硬性红线

- ❌ **禁止**在源码/脚本/文档里硬编码版本号；版本只写 `version.json`（rule-022）
- ❌ **禁止**手改产物文件名、手改产物版本；产物名由脚本按真源生成
- ❌ **禁止**跳过发布清单与（正式发布的）tag
- ❌ **禁止**用未提交的工作区打包后宣称"这是某版本"——版本串必须能反查到提交
- ❌ **禁止**跳过校验（`-AllowHardcodedVersion` 等放行开关仅在明确知晓原因时使用，并在报告中写明）
- ✅ 打包会**停掉正在运行的程序**（文件锁）并**重建 `Release\`**；若他人正在做运行期验证，先确认时机

## 2. 前置确认（Step 0）

1. 读 `version.json`：确认 `version` / `channel` / `revision` 是本次要发的值
2. 读 `CURRENT_TASK.md`：确认**当前执行者与授权目录**（rule-021）；`installer/**` 属发布规则范畴
3. 确认**没有任何人正在跑运行期验证**（打包会重建 `Release\` 并改变程序集版本串）
4. 确认 Inno Setup 6 已安装（脚本会自动探测 `ISCC.exe`）

## 3. 发布流程

### Step A：准备发版（版本号**无需人工填写**）

**版本号由脚本自动推导**（依据提交历史），人只确认渠道：

- `channel`：`alpha`（内测）/ `beta`（公测）/ `rc`（候选）/ `stable`（正式）
- 若本次转正式，把 `version.json` 的 `channel` 改为 `stable`；其余字段（`version` / `releasedCommit` / `releaseDate`）由脚本写回

推导规则（rule-022）：`feat!:` / `BREAKING CHANGE` ⇒ MAJOR+1；`feat:` ⇒ MINOR+1；`fix:` / `perf:` ⇒ PATCH+1；其余 ⇒ 仅 BUILD+1。

> **唯一前提：提交信息必须规范**——这是"版本自动管理"的前提，也是《代码提交》技能的既有硬性要求。

### Step B：更新变更日志

在 `CHANGELOG.md` 追加该版本条目（`Added` / `Changed` / `Fixed` / `Known Issues`）；
草稿可用 `git log <上一个 tag>..HEAD --oneline` 生成。

### Step C：提交（必须先提交）

按《代码提交》技能：UTF-8 文件 + `git commit -F`；提交内容含 `version.json` 与 `CHANGELOG.md`。

### Step D：打包

```powershell
# 自动推导版本（推荐）：不带版本参数即为自动
powershell -NoProfile -ExecutionPolicy Bypass -File installer\Pack-Release.ps1 -AutoVersion -Tag

# 若 src/UI 尚有版本号字面量（见 §5 自查项），需临时放行：
#   ... -AutoVersion -AllowHardcodedVersion   （并在报告中写明原因与后续修复计划）
```

常用开关：

| 开关 | 用途 |
|---|---|
| `-FromSource` | 从 `version.json` 读版本/渠道/序号 |
| `-Coexist` | 内测版与正式版**并存**（独立 AppId + 独立安装目录，不覆盖正式版） |
| `-Tag` | 成功后打 `v<产品版本>` tag |
| `-SkipBuild` | 复用现有 Release 产物（**仅限确认产物已是最新时**） |
| `-NoManifest` | 不生成发布清单（不推荐） |

### Step E：核对脚本输出的校验结果

**必须全部为 OK，否则停止发布**：

- `hardcoded version literals: 0`
- `BUILD_EXIT=0`
- `payload version mismatch` 未出现（版本串含目标版本）
- `commit check: OK`（版本串 commit == HEAD）
- `source newer than payload: 0`
- `ISCC_EXIT=0`
- 产物存在 + SHA256 + `icon match: True`

### Step F：产物核对

```
dist\SunEyeVision-Setup-<产品版本>-x64.exe
dist\release-manifest-<产品版本>.json
```

清单必须含：`productVersion` / `channel` / `commit` / `setupSha256` / `payloadFileCount` / `schemaVersion`。

### Step G：打 tag（正式发布必做）

`-Tag` 已自动执行；若手工执行：`git tag v<产品版本>`。

### Step H：报告

输出：版本号、渠道、产物路径、大小、SHA256、commit、清单路径、以及**未通过的校验项（若有）**。

## 4. 内测版（Alpha）特别约定

| 项 | 约定 |
|---|---|
| 版本格式 | **四段** `MAJOR.MINOR.PATCH.BUILD`（如 `1.0.0.1`）；渠道**不进版本号** |
| 显示名 | 安装向导与程序组显示 `SunEyeVision（内测版）`（脚本自动注入） |
| 与正式版关系 | **默认覆盖升级**（同一 AppId）；需要并存时加 `-Coexist` |
| 分发 | 只需分发 `Setup-*.exe` 与 `release-manifest-*.json`（自包含，依赖已打包） |
| 提醒 | 内测版同样是"可追溯产物"：清单里的 commit 必须能对上源码 |

## 5. 自查清单（报告前逐项确认）

- [ ] 版本只改在 `version.json`；源码无版本号字面量
- [ ] 已提交，且 `commit check: OK`
- [ ] `CHANGELOG.md` 已更新
- [ ] 打包脚本全部校验通过（无 FAIL、无未说明的 WARN）
- [ ] 产物名与版本一致；清单已生成
- [ ] 正式发布已打 tag
- [ ] `dist/` 产物未入库（已 gitignore）
- [ ] 若使用放行开关（如 `-AllowHardcodedVersion`），已在报告中写明原因与后续修复计划
