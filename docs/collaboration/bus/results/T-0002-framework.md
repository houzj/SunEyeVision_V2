# T-0002 回执（framework）— App.ini 解析契约测试（零工程改动路线）

- 卡号：T-0002（lane=framework，priority=2）
- 状态：done

## 一、做了什么
| 文件 | 内容 |
|---|---|
| `tests/SunEyeVision.UI.Tests/Session/ApplicationDataPathsConfigurationTests.cs`（新增） | 10 条契约测试，`[Collection("App data path configuration")]` 独立串行集合；全部走临时目录里的独立 `App.ini`，**不读写 `AppContext`**，因此不会污染其它测试的 AppContext |
| `tests/SunEyeVision.Persistence.Tests/`（还原） | 该工程 `csproj` + `packages.lock.json` 已 `git checkout --`；我先前建在该目录的测试文件已**移动**到 `.codebuddy/tmp/moved/`（未删除），`git status` 对该工程为空 |
| `src/Infrastructure.Persistence/ApplicationData/ApplicationDataPaths.cs` | 保留 `OpenConfigured(configurationFile, defaultRoot)` 重载（卡内允许保留并在回执说明），无参版本委托给它，缺省路径行为逐字不变 |

## 二、验收项逐条对照
| 卡内验收项 | 结论 | 证据（用例名） |
|---|---|---|
| 带 BOM 的 App.ini 可正常解析 | 通过 | `Valid_root_with_bom_comments_and_section_case_is_accepted`（`UTF8Encoding(true)` 写 BOM + 断言 Root 相等） |
| `[Other]` 段在 `[Data]` 之前可正确跳过 | 通过 | 同上（`[data]` 前有 `;`/`#` 注释）与 `Missing_data_section_uses_default_root`（`[Other]` 段不被当作数据段） |
| 重复键抛错且消息含行号 | 通过 | `Duplicate_root_key_is_rejected`（消息含「重复配置」与「第 3 行」） |
| 空值 / 相对路径 / 不存在盘符 / 段名缺右括号 / 非 key=value 抛 `InvalidDataException` | 通过 | `Empty_root_value_is_rejected` / `Relative_root_is_rejected` / `Missing_volume_is_rejected` / `Unterminated_section_header_is_rejected`（含「第 1 行」）/ `Line_without_key_value_separator_is_rejected` |
| 测试类置于非并行集合 | 通过 | `[Collection("App data path configuration")]`，此类是该集合唯一成员 |
| 不得写死不存在盘符 | 通过 | `DriveInfo.GetDrives()` 取现有盘符后，从 D–Z 里挑第一个不存在的字母，动态拼 `Root` |
| 不得在 Persistence.Tests 新增对 Infrastructure.Persistence 的引用 | 通过 | 已还原；`git status -- tests/SunEyeVision.Persistence.Tests` 为空 |

补充用例（超出卡内清单）：
- `Missing_file_uses_default_root_and_reports_source`：文件缺失 → 走缺省根、`RootSource` 含「缺省根」、Solutions/Logs 目录被建
- `Unreadable_configuration_throws_without_falling_back`：文件被独占打开读不出 → 抛 `IOException` 且**不回退**缺省根（断言缺省根目录未被创建）

## 三、实测数字
- 编译：`dotnet build tests\SunEyeVision.UI.Tests\SunEyeVision.UI.Tests.csproj -c Release` → 0 错误
- 测试：`dotnet test ... --filter "FullyQualifiedName~ApplicationDataPathsConfigurationTests"` → **10 通过 / 0 失败 / 0 跳过**（779 ms）
- 全量：`dotnet build SunEyeVision.slnx -c Release --no-incremental` → 0 错误 / 6146 警告 / 1:14.58（`.codebuddy\tmp\build-cards.log`）

## 四、未验项
- 未用 `AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", …)` 路线验证无参 `OpenConfigured()` 的端到端行为；当前测试直接调重载传路径，属"同一实现的两个入口"——如果你要求覆盖无参入口，需要再加一条用 SetData 的用例（会改动进程级 AppContext，必须与其它 UI 测试串行，我倾向另开卡做）。
- 未在真实程序里跑一遍坏配置（T-0001 / 批次报告里的手工反例仍待人工执行）。

## 五、需要裁决
- 无（`Persistence.Tests` 越界引用已自行还原，不再申请）。
