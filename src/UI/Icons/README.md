# 图标资源说明

本目录存放应用程序与解决方案文件的图标资源。

## 文件结构

```
Icons/
├── app_logo.ico          # 应用程序图标（csproj ApplicationIcon，编译进 exe）
├── solution.ico          # .solution 文件关联图标（安装器 DefaultIcon 指向它）
├── solution_*.ico        # 历史遗留：按尺寸拆分的单尺寸图标（保留，勿再新增引用）
├── solution_preview.png  # 预览图
└── README.md
```

## 两个正式图标（2026-09-18 起）

| 文件 | 尺寸 | 说明 |
|---|---|---|
| `app_logo.ico` | **7 尺寸**：256 / 128 / 64 / 48 / 32 / 24 / 16 | 软件图标。`src/UI/SunEyeVision.UI.csproj` 的 `ApplicationIcon` 指向本文件 |
| `solution.ico` | **同上 7 尺寸，且与 `app_logo.ico` 同源同内容** | `.solution` 文件在资源管理器中的图标 |

> 约束（rule-022 精神）：`solution.ico` 必须与软件图标**保持一致**（当前实现为同源同内容）。
> 若将来希望"文档图标"与"程序图标"可区分，应改为同源 + 文档角标，而不是换成无关设计。

## 生成 / 更新方式

**不要手工拖拽替换**，用参数化脚本（会先自动备份原图标）：

```powershell
# 从现有 app_logo.ico 的最大帧提取源图，重建两个 7 尺寸图标
powershell -NoProfile -ExecutionPolicy Bypass -File installer\Build-Icons.ps1

# 或指定源 PNG（须 ≥256×256）
powershell -NoProfile -ExecutionPolicy Bypass -File installer\Build-Icons.ps1 -SourcePng "D:\path\logo-256.png"
```

脚本行为：
1. 备份现有 `app_logo.ico` / `solution.ico` 到 `build_temp/icon-backup-<时间戳>/`
2. 从 `app_logo.ico` 的最大帧提取源图（或使用 `-SourcePng`）
3. 用 HighQualityBicubic 生成 7 个尺寸，写入 PNG 压缩的 ICO 帧
4. 输出尺寸清单供核对

## 生效范围与注意事项

| 场景 | 生效条件 |
|---|---|
| exe 图标（任务栏/资源管理器） | **需要重新构建** `src/UI`（图标已编译进 exe） |
| 安装包 Setup.exe 图标 | 重新打包（`.iss` 的 `SetupIconFile` 指向 `app_logo.ico`） |
| `.solution` 文件图标 | 安装器 `[Registry]` 写入 `DefaultIcon` = `{app}\Icons\solution.ico`；**已有旧注册时需刷新资源管理器图标缓存** |
| `solution_*.ico`（单尺寸残留） | 历史文件，当前无代码引用；保留仅为避免破坏未知引用 |

## 已知问题（待 `src/UI` 侧修复）

- `FileAssociationService.IsRegistered()` 仅比对 ProgID ⇒ **关联路径不会自愈**：换安装目录后仍指向旧路径（实测当前机器指向 `D:\MyWork\SunEyeVision\SunEyeVision\src\UI\bin\Debug\...`，该文件已不存在）。
  安装器注册可覆盖新装用户；程序侧自愈逻辑待改（属 `src/UI` 禁区，需 Codex 或授权）。

## 更新历史

- 2026-03-15：初始版本，`solution.ico` 为占位图标（单尺寸 48×48 拆分批）
- 2026-09-18：`app_logo.ico` 与 `solution.ico` 均由 `installer/Build-Icons.ps1` 重建为 7 尺寸且同源（修复"解决方案图标与软件图标不一致"）
