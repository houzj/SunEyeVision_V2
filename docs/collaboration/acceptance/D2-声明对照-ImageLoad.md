# D2 声明对照 · ImageLoad

> 自动生成：原件 imagesourcemodule / 我方 SettingsSchema

## 0 数量对比

| 项 | 数量 |
|---|---|
| 原件运行参数（ParamItem） | 23 |
| 我方声明字段 | 23 |

## 1 原件参数清单

| # | 参数名 | 默认值 |
|---|---|---|
| 1 | `SetCameraParamWaitTime` | 0 |
| 2 | `TriggerProcessRun` | False |
| 3 | `RefreshImage` | True |
| 4 | `SubscribeFolderPath` |  |
| 5 | `IsSubscribeFolderMode` | False |
| 6 | `PathCache` | 0 |
| 7 | `ClearTrigger` | False |
| 8 | `StitchHeight` | 2000 |
| 9 | `StitchStartHeight` | 0 |
| 10 | `StitchEnable` | False |
| 11 | `ExecuteType` | 1 |
| 12 | `OutMono8` | False |
| 13 | `AutoStop` | False |
| 14 | `CameraID` | -1 |
| 15 | `ImageSourceType` | 1 |
| 16 | `CurrentImagePath` |  |
| 17 | `TriggerStringEnable` | False |
| 18 | `SolSaveImageData` | False |
| 19 | `ShowImageName` | True |
| 20 | `AutoPlay` | True |
| 21 | `InitialSN` | 1 |
| 22 | `Interval` | 0 |
| 23 | `PixelFormat` | 1 |

## 2 我方字段清单

| # | 字段 id | 名称 | 页/组 | 最小 | 最大 | 步长 | 小数位 | 默认值 |
|---|---|---|---|---|---|---|---|---|
| 1 | `source.kind` | 图像源 | 基本参数/基本参数 |  |  | 1 | 2 |  |
| 2 | `output.format` | 像素格式 | 基本参数/基本参数 |  |  | 1 | 2 |  |
| 3 | `local.save-images` | 保存图像 | 基本参数/基本参数 |  |  | 1 | 2 |  |
| 4 | `output.mono8` | 输出Mono8 | 基本参数/基本参数 |  |  | 1 | 2 |  |
| 5 | `sequence.initial` | SN初始值 | 基本参数/基本参数 | 0 | 100000 | 1 | 0 |  |
| 6 | `local.path-cache` | 图片缓存 | 基本参数/基本参数 | 0 | 5 | 1 | 0 |  |
| 7 | `local.auto-switch` | 自动切换 | 基本参数/触发设置 |  |  | 1 | 2 |  |
| 8 | `local.stop-at-last` | 最后一张停止 | 基本参数/触发设置 |  |  | 1 | 2 |  |
| 9 | `local.subscribe-folder` | 订阅图片文件夹 | 基本参数/订阅图片文件夹 |  |  | 1 | 2 |  |
| 10 | `local.folder-path` | 图像路径 | 基本参数/订阅图片文件夹 |  |  | 1 | 2 |  |
| 11 | `local.refresh-folder` | 刷新文件夹图片 | 基本参数/订阅图片文件夹 |  |  | 1 | 2 |  |
| 12 | `local.trigger-folder-run` | 触发流程运行 | 基本参数/订阅图片文件夹 |  |  | 1 | 2 |  |
| 13 | `stitch.enabled` | 拼接图像 | 基本参数/基本参数 |  |  | 1 | 2 |  |
| 14 | `stitch.start` | 拼接起始高度 | 基本参数/基本参数 | 0 | 20000 | 1 | 0 |  |
| 15 | `stitch.height` | 拼接高度 | 基本参数/基本参数 | 0 | 20000 | 1 | 0 |  |
| 16 | `stitch.clear` | 清空触发 | 基本参数/基本参数 |  |  | 1 | 2 |  |
| 17 | `stitch.clear-value` | 清空条件 | 基本参数/基本参数 |  |  | 1 | 2 |  |
| 18 | `trigger.filter-enabled` | 字符触发过滤 | 基本参数/触发设置 |  |  | 1 | 2 |  |
| 19 | `trigger.text` | 输入字符 | 基本参数/触发设置 |  |  | 1 | 2 |  |
| 20 | `trigger.filter-text` | 触发字符 | 基本参数/触发设置 |  |  | 1 | 2 |  |
| 21 | `camera.timeout-ms` | 取帧超时(ms) | 基本参数/基本参数 | 1 | 2147483647 | 1 | 0 |  |
| 22 | `camera.exposure-time` | 控制曝光 | 基本参数/基本参数 |  |  | 1 | 2 |  |
| 23 | `camera.gain` | 控制增益 | 基本参数/基本参数 |  |  | 1 | 2 |  |

## 3 名称映射（待人工确认）

原件参数名为英文，我方为语义 id + 中文名称，无法自动等值匹配。下表按归一化名称给出候选，需逐条确认：

| 原件参数 | 我方候选字段 | 置信度 |
|---|---|---|
| `SetCameraParamWaitTime` |  | **无匹配** |
| `TriggerProcessRun` |  | **无匹配** |
| `RefreshImage` |  | **无匹配** |
| `SubscribeFolderPath` |  | **无匹配** |
| `IsSubscribeFolderMode` |  | **无匹配** |
| `PathCache` | local.path-cache | 高 |
| `ClearTrigger` |  | **无匹配** |
| `StitchHeight` | stitch.height | 高 |
| `StitchStartHeight` | stitch.start | 高 |
| `StitchEnable` | stitch.enabled | 高 |
| `ExecuteType` |  | **无匹配** |
| `OutMono8` |  | **无匹配** |
| `AutoStop` |  | **无匹配** |
| `CameraID` |  | **无匹配** |
| `ImageSourceType` |  | **无匹配** |
| `CurrentImagePath` |  | **无匹配** |
| `TriggerStringEnable` |  | **无匹配** |
| `SolSaveImageData` |  | **无匹配** |
| `ShowImageName` |  | **无匹配** |
| `AutoPlay` |  | **无匹配** |
| `InitialSN` |  | **无匹配** |
| `Interval` |  | **无匹配** |
| `PixelFormat` |  | **无匹配** |
