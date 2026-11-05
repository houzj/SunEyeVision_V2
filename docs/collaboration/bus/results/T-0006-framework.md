# T-0006 回执（framework）— 数据根不存在提示【已按 VM 流程拆除，卡作废】

- 卡号：T-0006
- 状态：**superseded（作废）** —— 用户 2026-11-05 人工验收时定调：这套「`App.ini` [Data] Root + 根不存在弹提示」是自建机制，**按原厂（VM）流程拆掉**；顺带把 `App.ini` 从载荷 SHA 里摘除（H-0013 取 A）。

## 一、结论先说：验收发现的两件事

1. **提示本身能工作**（用户截图实证）：新根不存在时弹出「数据目录」提示，正文 `D:\SunEyeVisionData_probe2 不存在，已创建空目录；如需看到原有方案请检查程序目录下的 App.ini。`，按钮「确定」，背后启动页继续加载（已用 26.5s / 版本 2.0.0.8）。点确定后继续启动，新根被创建、日志落到新根。
2. **但这条路在发布版上原本走不通**：`App.ini` 被 `installer/Release.Common.ps1` 的 `New-ReleasePayload` 收进 `release-payload.json`，改一次 `Root` 就 `InvalidDataException: Payload SHA mismatch: App.ini`，程序起不来（用户两次截图为证）。这等于「唯一的配置入口被防篡改绑死」。

## 二、按决定做的拆除（本轮实际改动）

| 文件:行 | 改动 |
|---|---|
| `src/UI/App.xaml.cs:252` | 删除整段 `if (_dataPaths.IsRootNewlyCreated) { … ThemeMessageDialog.Show(…"数据目录"…) }`；数据根解析后直接继续启动 |
| `src/Infrastructure.Persistence/ApplicationData/ApplicationDataPaths.cs` | 删除 `IsRootNewlyCreated` 属性；删除只服务于它的 `OpenConfiguredRoot(...)`；`OpenConfigured(...)` 的配置根分支改回统一 `Open(ValidateRoot(...), …)` |
| `installer/Release.Common.ps1:289` | `New-ReleasePayload` 枚举改为 `if($relative -ne 'release-payload.json' -and $relative -ne 'App.ini')`，并注释理由：配置项不进完整性清单，否则改配置就要重封包 |
| `tests/.../ApplicationDataPathsConfigurationTests.cs:131` | 原 3 条 `IsRootNewlyCreated` 断言替换为 2 条：`Configured_root_is_created_and_used`、`Other_section_root_key_is_ignored_and_default_root_is_used`（保住「配置根会被创建」与「[Other] 段被跳过」两处语义） |

## 三、实测（本轮）

| 项 | 命令/方法 | 结果 |
|---|---|---|
| 全量 Release 构建 | `dotnet build SunEyeVision.slnx -c Release --no-incremental` | **0 错误 / 6144 警告 / 1:11**；`Build payload sealed: 31DB9D57…` |
| 清单是否仍含 App.ini | 读 `release-payload.json` | `ManifestContainsAppIni=False`，条目 591 条 |
| **改配置不重封包能否启动** | 只把 `App.ini` 的 `Root` 改成 `D:\SunEyeVisionData_probe_tmp`（不重封包）→ 启动 exe | **44 秒进主界面**「太阳眼视觉 - 机器视觉平台」，无任何提示框、无 SHA 报错，新根目录被自动创建 |
| 数据根配置测试 | `--filter ApplicationDataPathsConfigurationTests` | **12 通过 / 0 失败** |

## 四、现场

- `App.ini` 已还原为 `[Data] Root=D:\SunEyeVisionData`（该文件已不参与校验，无需重封包）。
- 验收期间建的 `D:\SunEyeVisionData_probe`、`_probe2`、`_probe_tmp` 三个测试根**已删除**，正式根 `D:\SunEyeVisionData` 未动。

## 五、未验项

- 「缺省根不弹提示」「日志目录不可写时的 Warning」两条场景在本轮**未继续跑**（提示机制已拆除，前者随之失去意义；后者见 T-0008 回执）。
- 原厂（VM）在同一情形下的确切行为（数据根缺失时是否静默重建目录）本次未取新证据，按用户口径"按 VM 流程启动"执行，未额外取证。
