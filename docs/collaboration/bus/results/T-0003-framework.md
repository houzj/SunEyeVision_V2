# T-0003 回执（framework）— 图像源条选中节点后不显示的回归修复

- 卡号：T-0003（lane=framework，priority=1）
- 状态：done（界面部分 4 条待人工验收，见第四节）

## 一、根因与改法
`ImageResultControl.xaml` 的图像源条可见性绑在一个**永远为 false** 的开关上：`ShowImagePreview` 全仓只有定义与透传、**没有任何写入点**（历史提交 `f8a6ca34e` 重写数据链时把写入点删了，可见性开关留成孤儿）。数据链本身是通的（`RefreshFormalImageSourceSelection` → `_activeImageSourceBinding` → `ImageResultControl.RefreshSourceSession()` → `ImagePreviewContent.Session`），所以只补"显示判断"，不动解析与会话链路。

| 文件:行 | 改动 |
|---|---|
| `src/UI/Shell/Controls/Shell/ImageResultViewModel.cs`（+5/−19） | 删死成员 `_showImagePreview`、`ShowImagePreview`、`ImagePreviewHeight`；新增 `HasActiveImageSource`（`SetProperty` 通知，默认 false） |
| `src/UI/Shell/Windows/MainWindowViewModel.cs`（+2/−20） | 删 `_showImagePreview` 字段与 `ShowImagePreview`/`ImagePreviewHeight` 两个透传 |
| `src/UI/Shell/Windows/MainWindowImageSourceEditing.cs`（+10/−4） | 新增私有 `SetActiveImageSourceBinding(binding)`：同时写 `_activeImageSourceBinding`、写 `_imageResult.HasActiveImageSource`、发 `ActiveImageSourceBinding` 通知；创建（原 L45-49）与清除（原 L62-65）两处都改走它 |
| `src/UI/Shell/Controls/Shell/ImageResultControl.xaml:16` | `Visibility="{Binding ShowImagePreview…}"` → `HasActiveImageSource`；**保留 `<StackPanel>` 包裹层**，`ImageResultControl.xaml.cs:92-93` 的 `(Panel)Workspace.SourceStripContent` + `Children.OfType<ImageSourceStrip>().Single()` 一行未改 |
| `tests/SunEyeVision.UI.Tests/Session/SourceAndVariablesSessionTests.cs`（+2/−1） | 补断言：选中图像源节点 `HasActiveImageSource == true`；取消选中后 `== false`（锁住这次缺失的写入点） |

## 二、卡内验收项对照
| 验收项 | 状态 | 证据 |
|---|---|---|
| 选中图像源节点零张图：条可见、计数 0/0、工具条可点 | **未验（需人工）** | 依赖真实窗口渲染；自动化只能断言 `HasActiveImageSource == true`（已通过） |
| 加载 2 张图：条内 2 个缩略图 | **未验（需人工）** | 同上；`DocumentUiFixture` 场景里 `strip.Items.Count == 2` 已由既有断言覆盖 |
| 选中 Blob 节点：条隐藏、上方画布不受影响 | **未验（需人工）** | 逻辑上 `ImageSourceResolver.Resolve` 对非图像源节点返回非 Found → `ClearFormalImageSourceSelection()` → `HasActiveImageSource=false`；无自动化用例 |
| 连续快速切换 10 次：不抛异常、无空条 | **未验（需人工）** | 唯一写入口保证"绑定为空 ⇔ 标志为 false"，无中间态；仍需人工点按验证 |

## 三、实测数字
- 编译：`dotnet build tests\SunEyeVision.UI.Tests\...\SunEyeVision.UI.Tests.csproj -c Release` → 0 错误
- 测试：`--filter "FullyQualifiedName~SourceAndVariablesSessionTests|DisplayName~preferences"` → **3 通过 / 0 失败 / 1m4s**（含新加的两处 `HasActiveImageSource` 断言）
- 全量：0 错误 / 6146 警告 / 1:14.58

## 四、未验项（需要人工，属 C 类）
按批次报告里的 5 步：选中图像源节点看条与 0/0 → 加载 2 张图看缩略图 → 选中 Blob 节点看隐藏 → 切子程序/取消选中看隐藏不报错 → 快速切换 10 次不异常。

## 五、需要裁决
- 无。若"非图像源节点也显示本地源条"才是期望行为（VM 原件语义待 rea 证据），需另开卡，不在本卡范围。
