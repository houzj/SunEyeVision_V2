# 当前协作任务

- 2026-09-29 沟通基线：已保存《通讯解析字段与统一绑定模型最终优化方案-20260929.md》，统一稳定 GUID 字段、可修改显示名称、引用快照、显式数据源、脚本/子程序/通讯解析输入输出模型，以及新增、删除、重命名和旧配置迁移规则；当前为方案基线，后续按实施顺序推进。

- 2026-09-29 绑定数据源显式化：移除 `ParamBinding` 默认追加全局变量的设计，新增默认关闭的 `AllowGlobalVariableBinding` 能力开关；`ConfigSetting`、ROI 订阅面板和 SDK 示例显式开启，通讯管理器组装内容仅使用解析列表数据源，不再通过可见性隐藏全局变量。

- 2026-09-29 绑定数据源显式化构建验证：SDK Debug 构建通过（0 错误、226 个既有警告），UI Debug 构建通过（0 错误、930 个既有警告），UI Release 构建通过（0 错误、1158 个既有警告）。

- 2026-09-29 解析列表重命名同步：解析项名称变更时同步替换 `AvailableDataSource` 的绑定身份，并迁移组装输出中的 `BindingSource` 与显示文本，避免重命名后继续引用旧的 `ReceiveEventInputs.inX` 路径；删除解析项时按重命名前的身份清理数据源。

- 2026-09-29 解析列表重命名同步构建验证：UI Debug 构建通过，0 个错误、760 个既有警告。

- 2026-09-29 构建验证：SDK 与 UI Debug 构建均通过，0 个错误；UI 构建存在 748 个既有 XML 注释警告。运行中的程序锁定了 dev 输出文件，因此未强制覆盖运行时 DLL。

- 2026-09-29 组装内容参数绑定：将组装列表“内容”列替换为 SDK `ParamBinding` 控件；解析列表变量通过 `AvailableDataSource` 注入绑定候选，新增/删除解析项同步更新候选源，组装项保存绑定路径与显示文本。UI 构建通过，0 个错误、748 个既有警告。

- 2026-09-29 协议比对页布局补丁：输入/组装 DataGrid 使用专用 `CompareListMaxHeight`，展开消息时收缩到 120，给 `＋` 预留空间；组装配置分隔符改为下拉框，并仅在“回复给设备”启用时显示。

- 2026-09-29 协议比对 string/对齐二次修复：类型 ComboBox 增加即时更新；数值范围和 string 文本框改用 DataTrigger 样式切换，string 时强制隐藏 NumericUpDown；序号及状态列使用 DataGridCell 水平居中样式，避免仅设置 TextBlock 对齐无效。

- 2026-09-29 通讯管理器绑定设备下拉显示修复：两个接收事件详情页的设备 ComboBox 改用显式 `ItemTemplate` 绑定 `Communication.Name`，并设置 `TextSearch.TextPath`，避免主题模板回退显示 `SunEyeVision.Workflow.Communication` 类型名。

- 2026-09-29 协议比对输入/组装页展开消息布局修复：两个页签的 DataGrid 改为绑定 `SelectedReceiveEvent.OutputListMaxHeight`，打开消息后自动收缩表格并保留下方 `＋` 按钮可见，避免消息面板遮挡新增入口。

- 2026-09-29 协议比对列表视觉与结果列调整：解析/比对/组装列表序号表头及内容居中；比对列表表头统一为序号、名称、设备、比较规则配置、数据结果、状态，数值范围控件收进比较规则配置列；string 类型使用文本比较值输入框；比对输入增加 Result/Status，状态初始为“未接收”。代码/XAML 编译阶段未见新增错误，但完整构建最终复制仍被运行中的 `dev/Debug/SunEyeVision.UI.dll` 锁定。

- 2026-09-29 接收事件列表右键操作：事件项支持右键重命名和删除；右键时自动选中目标，重命名复用主题对话框并刷新列表名称，删除二次确认并自动选中相邻事件/显示空状态。

- 2026-09-29 协议比对输入配置补齐：增加字符长度比较开关和字节长度 NumericUpDown；输入/组装 DataGrid 设置 `HeadersVisibility=Column` 隐藏默认行头，消除左侧主题不一致的白色空白条。编译/XAML 阶段无错误，但最终复制仍被运行中的 `dev/Debug/SunEyeVision.UI.exe` 锁定（MSB3021/MSB3027）。

- 2026-09-29 文本协议比对 string 项样式：比较项数据类型为 `string` 时隐藏数值范围 NumericUpDown，改为主题化文本比较值输入框；`int/float` 继续显示最小值–最大值范围控件。编译/XAML 阶段无错误，但最终复制仍被运行中的 `dev/Debug/SunEyeVision.UI.exe` 锁定（MSB3021/MSB3027）。

- 2026-09-29 协议比对详情显示修正：协议比对页面的判断改为直接依据 `EventType=ProtocolCompare`，并补齐事件类型切换通知；避免文本-协议比对事件错误显示为文本-协议解析输出列表，确保显示输入配置/组装配置页签。UI Debug 构建成功，0 错误、748 个既有警告。

- 2026-09-29 文本-协议比对详情初版：新增输入配置/组装配置双页签；输入页包含绑定设备、分隔符、解析列表、类型、比较规则、范围和增删；组装页包含回复设备、分隔符、组装列表、类型、内容、结果和增删。当前为 UI/临时 VM 配置，尚未接入真实比对执行和持久化。编译/XAML 阶段无错误，但最终复制仍被运行中的 `dev/Debug/SunEyeVision.UI.exe` 锁定（MSB3021/MSB3027）。

- 2026-09-29 导航禁用样式修复：导航按钮改用轻量主题 ControlTemplate，避免 WPF 默认 Button 禁用模板覆盖背景和文字颜色；发送事件、触发配置、响应配置、心跳管理等未实现项不再显示白色块，保持深浅主题一致。UI Debug 构建成功，0 警告、0 错误。

- 2026-09-29 通讯管理器导航主题化：所有导航项统一使用主题基础样式；正常、悬停、选中、禁用和焦点状态绑定主题资源，禁用项不再使用 WPF 默认灰色/文字样式，移除不一致焦点虚线框。UI Debug 构建成功，0 警告、0 错误。

- 2026-09-29 通讯管理器导航对齐：所有导航按钮统一水平/垂直居中，移除设备管理与接收事件的左侧 Padding，修正启用与禁用导航项对齐不一致。UI Debug 构建成功，0 错误、744 个既有警告。

- 2026-09-29 通讯管理器展开高度调整：窗口高度提升至 860；解析结果展开时 DataGrid 可视高度提升至 220，目标稳定显示 4–5 个 item，同时保留 DataGrid 自身滚动和底部 `＋`。UI Debug 构建成功，0 错误、744 个既有警告。

- 2026-09-29 展开状态交互与导航悬停优化：解析结果展开时将 DataGrid 可视高度调整为 105，超出项目通过 DataGrid 自身滚动，确保 `＋` 按钮可见；导航按钮悬停使用主题悬停背景和主题主文字色，避免浅色悬停背景下白字不可读。

- 2026-09-29 输出列表与解析面板空间协调：上方详情区域保持无滚动条；DataGrid 最大高度根据解析面板展开状态动态调整（展开时 170、收起时 260），超出内容只由 DataGrid 自身滚动，`＋` 始终位于 DataGrid 下方且不被遮挡。UI Debug 构建成功，0 错误、744 个既有警告。

- 2026-09-29 导航与展开交互二次修复：接收事件导航样式不再继承设备管理的 ActivePage 触发器，避免两个按钮同时高亮；解析结果展开后恢复上方详情区域滚动，使输出列表底部的 `＋` 可滚动到并点击，DataGrid 鼠标滚轮仍优先驱动自身列表滚动。

- 2026-09-29 通讯管理器滚动交互：窗口高度调整为 760；上方事件详情不再抢占纵向滚动，DataGrid 保留自身滚动条；鼠标悬停 DataGrid 时直接驱动其内部 ScrollViewer，避免打开解析结果面板后列表滚动失效。UI Debug 构建成功，0 错误、744 个既有警告。

- 2026-09-29 接收事件基本配置布局：分隔符与字符长度比较拆为独立行；长度字段改名为“字节长度”，使用 SDK `NumericUpDown`，范围 1–65535，避免手动文本输入和无效 0 值。UI Debug 构建成功，0 错误、744 个既有警告。

- 2026-09-29 输出 DataGrid 空白区域主题修复：DataGrid 空视口、边框和滚动区域统一使用主题表格背景，行背景改为透明交由主题行样式控制，消除出现滚动条后底部默认白色空白条。编译/XAML 阶段无错误，但最终复制仍被运行中的 `dev/Debug/SunEyeVision.UI.exe` 锁定（MSB3021/MSB3027）。

- 2026-09-29 通讯管理器导航高亮修复：移除“设备管理”固定选中背景，设备管理/接收事件按钮分别根据 `ActivePage` 动态显示高亮样式，页面切换后导航选中项与内容一致。编译/XAML 阶段无错误，但最终复制仍被运行中的 `dev/Debug/SunEyeVision.UI.exe` 锁定（MSB3021/MSB3027）。

- 2026-09-29 接收事件详情布局：解析结果面板从详情滚动内容中移出，固定在右侧底部独立区域；上方配置/输出区域独立滚动，DataGrid 多行不会挤压解析结果面板。编译/XAML 阶段无错误，但最终复制仍被运行中的 `dev/Debug/SunEyeVision.UI.exe` 锁定（MSB3021/MSB3027）。

- 2026-09-29 接收事件空状态：没有接收事件或没有选中事件时隐藏右侧详情，显示“暂无接收事件/点击左侧上方＋创建接收事件”提示；只有选中事件后显示绑定设备、基本配置、输出列表和解析结果。UI Debug 构建成功，0 警告、0 错误。

- 2026-09-29 解析结果面板样式优化：增加主题化消息面板样式，标题栏右侧动态显示“打开消息/收起”，解析结果内容区使用主题背景、文字和边框，增加内容滚动，保持十六进制开关与清空按钮在标题行对齐。编译/XAML 阶段无错误，但最终复制仍被运行中的 `dev/Debug/SunEyeVision.UI.exe` 锁定（MSB3021/MSB3027）。

- 2026-09-29 修复输出列表新增：输出列表 `＋` 改为直接绑定 `AddReceiveEventOutputCommand`，由 ViewModel 操作当前选中事件的 Outputs 集合，避免代码后置点击路径失效。编译/XAML 阶段无错误，但最终复制仍被运行中的 `dev/Debug/SunEyeVision.UI.exe` 锁定（MSB3021/MSB3027）。

- 2026-09-29 接收事件输出 DataGrid 多行优化：列表区域最大高度并启用自身纵向滚动条，避免新增多项后被详情页裁切；压缩事件列表中间栏及名称/设备列，数据结果改为固定可见宽度；设备列改为 `int/float/string` 下拉；序号交替索引上限提高，避免出现 1/2 循环编号。UI Debug 构建成功，0 警告、0 错误。

- 2026-09-29 字符长度比较配置优化：字符长度默认值改为 1；长度输入仅在启用“字符长度比较”后显示，避免关闭状态下出现无意义的 `0`；开关补齐双向绑定和垂直对齐。UI Debug 构建成功，0 错误、742 个既有警告。

- 2026-09-29 接收事件输出列表布局：移除空数据时的 DataGrid 大块占位高度，禁用 DataGrid 内层纵向滚动，统一使用详情页外层滚动条；输出编辑框、行背景、文字和边框绑定主题资源，新增按钮紧跟实际 item 列表显示。编译/XAML 阶段无错误，但最终复制仍被运行中的 `dev/Debug/SunEyeVision.UI.exe` 锁定（MSB3021/MSB3027）。

- 2026-09-29 接收事件基本配置：分隔符改为下拉选择，默认逗号，提供逗号、分号、空格、`\r`、`\n`、`\r\n` 选项。UI Debug 构建成功，0 错误、742 个既有警告。

- 2026-09-29 文本-协议解析事件详情界面：输出列表改为主题化 DataGrid，名称/数据类型可直接编辑，数据结果与状态只读，删除使用行末 ×，新增按钮固定在 DataGrid 正下方；新增消息 Expander，展开后显示解析结果、16 进制显示开关和清空按钮。当前输出/消息仍为临时 VM 数据，尚未接入真实接收解析与持久化。原目标 UI 构建编译/XAML 阶段通过，但运行中的 `dev/Debug/SunEyeVision.UI.exe` 锁定最终复制；改用独立 OutDir 验证时因项目引用输出路径缺失失败，需关闭运行实例后按标准命令重建并人工验收。

- 2026-09-29 事件管理弹窗规则收敛：文本处理方式支持“协议解析/协议比对”；字节匹配自动限定为“协议比对”；脚本处理方式隐藏事件类型区域并显示 Python 脚本说明。VM 同步约束可选项和显示名称，避免生成非法组合。编译阶段无代码/XAML错误，但当前运行中的 Debug UI 锁定 `dev/Debug/SunEyeVision.UI.exe`，最终输出复制失败（MSB3021/MSB3027），关闭程序后需重建。

- 2026-09-28 接收事件 UI 第一阶段：沿用通讯管理器现有左侧导航，启用“接收事件”页面；新增接收事件 VM 列表、`+` 事件管理弹窗（文本/字节匹配/脚本，协议解析/协议比对）、绑定设备、分隔符、字符长度比较和输出列表详情布局。当前仅完成 UI 与临时 VM 配置，不接入真实接收触发、发送执行或持久化。UI Debug（不重建项目引用）构建成功，0 错误、742 个既有警告。

- 2026-09-28 修复通讯管理窗口关闭重入异常：未保存配置处理的异步 `OnClosing` 在当前 Closing 回调尚未返回时直接调用 `Close()`，触发 WPF `Window.VerifyNotClosing`；改为设置允许关闭标记后通过 Dispatcher 延迟 Close。UI Debug（不重建项目引用）构建成功，0 错误、742 个既有警告。

- 2026-09-28 OPC UA 数据点表再次收敛：值列缩至 200，移除无必要的质量列；双击编辑改由 DataGrid 统一捕获并从点击单元格向上定位 DataGridRow，恢复命名空间/标签编辑窗口入口。UI Debug（不重建项目引用）构建成功，0 错误、740 个既有警告。

- 2026-09-28 OPC UA DataGrid 水平滚动条恢复：单一 DataGrid 改造后列宽总和可完全适配窗口，WPF 不显示水平滚动条；将名称列固定为 150、值列固定为 260，使总列宽超过视口，恢复自然水平滚动。UI Debug 构建成功，0 错误、752 个既有警告。

- 2026-09-28 OPC UA 数据点表改为单一 DataGrid：移除独立表头 + ListBox + 横向偏移补偿方案，表头、数据行和水平滚动条统一由 DataGrid 管理，保留直接 Value 编辑、双击配置编辑和 × 删除。编译阶段未发现代码/XAML错误，但输出覆盖再次被运行中的 `dev/Debug/SunEyeVision.UI.exe` 锁定（MSB3021/MSB3027）；关闭程序后需重新构建并人工验证。

- 2026-09-28 OPC UA 数据点表滚动同步二次修正：用户实测表头仍未随横向滚动，改为直接监听 ListBox 路由的 `ScrollViewer.ScrollChanged`，每次按 `HorizontalOffset` 更新表头平移，移除不稳定的内部 ScrollViewer 查找/订阅。当前 UI 进程占用 `dev/Debug/SunEyeVision.UI.dll/.exe`，本轮无法覆盖输出完成构建（MSB3021/MSB3027），需关闭运行实例后验证。

- 2026-09-28 OPC UA 数据点表横向滚动对齐修正：表头与 ListBox item 分离导致水平滚动时只有 item 偏移；现在监听 ListBox 内部 ScrollViewer 的 HorizontalOffset，同步反向平移表头，纵向滚动保持 item 独立。UI Debug（不重建项目引用）构建成功，0 错误、740 个既有警告。

- 2026-09-28 OPC UA 数据点配置可重复编辑：明确只有命名空间索引和标签名称是配置源；数据点行双击打开同一对话框编辑这两个字段，名称/类型/权限/质量/更新时间继续只读派生展示。保存后解除旧 NodeId 监视并重新监视新 NodeId，重复 NodeId 会拒绝。UI Debug（不重建项目引用）构建成功，0 错误、740 个既有警告。

- 2026-09-28 OPC UA 数据点表列对齐修正：表头与 ListBox item 统一使用同一组列宽（132、星号、90、70、110、90、24），移除 item 行额外左右内边距，补齐名称列边距并将 Value/Type 表头改为中文“值/类型”。本轮复建被运行中的 `dev/Debug/CommonControl.dll` 文件锁阻塞（MSB3021/MSB3027），此前同一 UI 工程构建已通过；需关闭运行实例后再复建确认。

- 2026-09-28 OPC UA 数据点表交互收敛：数据点列表继续使用主题化 ListBox 行样式，表头/行文字、背景、悬停、选中、输入框和删除按钮均绑定主题资源；Value 统一为可直接编辑的 TextBox，不再按布尔类型切换 ComboBox，也不再显示修改/确认/取消按钮；按 Enter 或离开输入框自动写入，Esc 恢复；删除改为行末主题化“×”并直接移除。UI Debug 构建成功（0 错误，752 个既有警告）。

- 2026-09-28 OPC UA 数据点表与分离式端点实施：在现有匿名 None 基线之上，加入地址/端口输入及旧 `endpointUrl` 迁移、可持久化的数据点、两个文本框的添加弹窗、选中行公共 NodeId、表格类型/质量/权限/值、显式类型化写入、应用级加载/重连监视、异步服务调用与 UI 合并刷新。新建设备默认自动重连；退出时处理 OPC UA 未保存配置。Debug UI 构建通过；OPC UA 定向测试 5/5、通讯架构与 Modbus 定向 9/9、排除方案加载回归组的 UI 测试 59/59 通过。全量 UI 测试 6 项失败：方案加载组 5 项因 `ImageLoad` 类型标识反序列化失败，Modbus 1 项在并行全量运行失败但单独通过；需要单独跟进，不可声称全量通过。`127.0.0.1:49320` 端口可连接，但真实 KEPServerEX 数据点读写、WPF 运行时布局、VS 设计时和服务器重启恢复尚待用户人工验收。工作区原有匿名 None 改动保留，未提交。

- 2026-09-28 OPC UA 数据点表沟通基线补充：NodeId 不再占用表格列，改在表格上方公共只读区域显示**当前选中行**的完整 NodeId；无选择、删行或切换设备时清空。添加弹窗保留 NodeId 预览。方案文档 ASCII 和选择交互已同步修改；仍属设计，尚未实施代码。

- 2026-09-28 OPC UA 沟通基线追加确认：新建及详情页将整条“服务端点”输入拆成“服务器地址 + 服务器端口”，默认 `127.0.0.1` / `49320`，`opc.tcp://...` 仅作只读预览；已更新 `docs/01-方案设计/2026/09/OPC-UA数据点表与匿名联调优化方案-20260928.md`。现存旧方案需优先从有效 `endpointUrl` 迁移，不能误用旧通用 `host/port` 默认值；此变更仍仅为设计基线，**尚未实施代码**。

- 2026-09-28 OPC UA 数据点表方案已保存为沟通基线：`docs/01-方案设计/2026/09/OPC-UA数据点表与匿名联调优化方案-20260928.md`。确认第一阶段仅匿名 + None；不做节点树、用户名/密码及证书管理；以两个输入框添加字符串 NodeId，在表格监视和明确写入 Value，并持久化数据点配置。此条仅记录**设计**，数据点表、弹窗及相应运行时链路尚未完整实施；后续分工与验收以文档为准，完成度以代码/测试另记。

- 2026-09-28 OPC UA 第一阶段收敛为匿名 None 联调：移除新建/详情页安全模式选择、用户名/密码说明及服务器证书信任列表/导入/撤销操作；删除仅供证书管理的运行时 API、`OpcUaTrustStore` 与对应测试，移除未实现的身份模式和安全模式配置映射。驱动只选择同时支持 None 与 Anonymous 的端点，保留 OPC UA SDK 为客户端应用自动创建证书所需的内部配置；未删除磁盘上既有证书或已保存方案的旧设置字典键，旧键不再参与连接。匿名本地集成测试 2/2 通过（默认端点与连接/浏览/读写/订阅）；UI Debug 构建 0 错误、1044 警告，未构建 Release。KEPServerEX 真实联调、WPF 运行时布局和 VS 设计时仍待用户人工验收。

- 2026-09-28 OPC UA 联调默认端点调整：依据用户 KEPServerEX 端点截图，将新建 OPC UA 设备与 `Communication.EndpointUrl` 缺省值统一改为 `opc.tcp://127.0.0.1:49320`，新增默认值回归测试；已保存设备的显式端点保持不变。UI Debug 构建成功（0 错误、918 警告），定向默认值测试 1/1 通过；未构建 Release。KEPServerEX 的端点、匿名登录及读写订阅仍需用户运行时联调验收。

- 2026-09-28 设备管理层双向基线同步：从 `main=c5b1c046`、`feature/comm=ba31c749` 合并通讯运行时下沉改动，保留主线相机图像源/帧调度改动与双方协作记录。发现通讯分支 `src/DeviceDriver/Communications/Runtime/` 的两份核心源码被通用 `runtime/` 规则忽略、未进入 Git；已补入并添加定向忽略例外。NuGet 依赖重新还原后，全量 Release 构建成功（0 错误、976 警告），通讯聚焦测试 36/36、相机/图像源/停止与 SDK 聚焦测试 23/23 通过。Release 程序短暂启动验证：方案加载、Modbus 会话连接、相机自动连接及 `MainWindow.after Show` 已记录；测试用 PID 41032 已结束。测试环境 TCP 客户端目标拒绝连接，属于外部服务状态；真实设备、通讯窗口 UI、VS 设计时仍待人工验收。详情见 `HANDOFF-device-baseline-sync-20260928.md`。

- 2026-09-27 本轮收尾验证：全量 Release 构建 0 错误、806 既有警告；全量 Debug 构建 0 错误、840 既有警告，`dev/Debug` 已更新。最终 Debug 定向测试 DeviceDriver 5/5、ImageLoad 6/6、UI 相机取流/连续停止 4/4、WorkflowRunRequest 2/2 全部通过；同类 Release 定向测试此前通过。单次相机运行的 SDK 启动、触发、停止已显式放到工作线程，避免从 WPF 命令同步进入原生调用。Release 启动冒烟日志 `logs/2026/09/27/SunEyeVision_230826.txt` 仅确认方案加载及海康设备自动连接；为清理本次隐藏测试实例，按精确 PID 22556 结束进程，未确认 `MainWindow.Show completed`。真实海康相机的工作流帧驱动、UI 卡顿和触发关闭原故障仍需用户可见界面复测；VS 设计时未验收。现有工作区未提交改动均保留。

- 2026-09-27 相机帧驱动第一阶段实施：图像源节点接收本轮预绑定帧而不重复采集；UI 工作流单次、连续及解决方案级单次入口在执行前取得帧；相机自由运行/硬触发使用后台取流的最新帧邮箱，软件触发每轮发一次命令等待一帧。预览调试与工作流采集使用设备级互斥租约，关闭预览窗口不再停止非自身取流；相机结果逐轮入缓存、展示约 200 ms 合并，像素统计在工作线程预计算，低频记录 UI 等待/刷新超时及帧计数。定向 Release 测试：DeviceDriver 5/5、ImageLoad 6/6、UI 4/4、WorkflowRunRequest 2/2，通过；全量 Release 构建 0 错误（最近一次 1072 警告）。Release 程序隐藏启动冒烟确认当前方案加载和真实海康设备自动连接，日志停在 `MainWindow.Show begin` 后由测试脚本结束本次进程，不能据此宣称主界面显示完成或工作流真实相机验收。触发关闭读取失败与 UI 卡顿根因仍待真实运行复测；VS 设计时未验收。已有未提交改动保留，未清理或提交。

- 2026-09-27 补全相机图像源调度沟通基线：明确图像源节点的两阶段角色——运行前由协调层读取配置确定相机，帧就绪事件由调度器决定是否启动一轮，运行中节点只消费本轮帧快照并输出给下游；帧事件可由 SDK 回调或后台阻塞取帧循环产生，节点不自行发触发/取帧/重启工作流。补充单次/连续状态转移、旧帧和停止后迟到帧排除、图像内存所有权，以及未来通讯事件仅保留非图像调度抽象、不实施其触发功能。本轮仅更新方案文档，未改代码或构建。

- 2026-09-27 根据用户补充修订相机图像源调度方案：工程实施通常一个工作流使用一个图像源，多相机优先拆为多个工作流；本轮不在程序中限制图像源数量，不预设多相机主触发/配对，也不因多个图像源而新增运行拒绝逻辑。仅更新沟通基线文档，未改代码或构建。

- 2026-09-27 保存相机图像源工作流驱动与采集调度方案：新增 `docs/04-工具设计/图像源/2026/09/27/相机图像源工作流驱动与采集调度方案-20260927.md`，明确按钮授权与帧事件由唯一调度器协调、单次/连续及触发模式语义、无图像源/本地图像回归、调试预览不进入工作流且首版与工作流采集互斥、有界超载与分类计数、UI 展示隔离及先定位后实施的验收门槛。旧图像源 UI 文档已注明相机调度部分由新文档覆盖，并更新文档索引。本轮仅文档写入，未改代码或构建；触发关闭读取失败与 UI 卡顿根因仍待实测。

- 2026-09-27 相机图像源连续运行 UI 卡顿的架构诊断（未实施修复）：当前每次取帧即完整执行工作流，`WorkflowExecutionManager` 每轮新建 PluginManager/WorkflowExecutionEngine，并将节点状态、快照、完成结果和进度派发到 UI；`MainWindowViewModel.OnNodeExecutionCompleted` 在 UI 路径读取全部输出、刷新结果树/图像、通知调试窗口和写日志，图像源结果的像素统计还可能触发 HALCON 全图计算。真实日志 `SunEyeVision_213344.txt` 中短时间内重复出现节点完成与结果刷新，和双击窗口无响应一致，但尚未做 UI 线程采样，不能将单一热点断定为最终根因。上一轮未经验证的 200ms 展示限流改动已撤回；下一步应在用户确认后按“采集/工作流执行/UI 展示解耦、结果合并与背压、昂贵计算脱离 UI”做设计与有针对性的验证，不能仅靠降低刷新频率掩盖问题。

- 2026-09-27 修复相机图像源停止后工作流标签仍显示红色方块：`SunEyeVision_212234.txt` 中工作流1于 21:23:07.712 记录停止请求，之后没有“执行已停止”；而本地图像工作流2于 21:25:39.745 请求停止、21:25:39.747 收到停止确认。连续循环原先以 `while (!token.IsCancellationRequested)` 退出，取消若落在两轮边界可正常退出而绕过唯一的 `OperationCanceledException` 停止通知；后台租约释放了，但 UI `IsRunning` 不复位。改为循环顶端显式抛取消，并在统一 `finally` 中于释放租约后恰好发送停止通知；本地/相机共用同一状态路径。连续停止相关 STA 回归测试 2/2 通过；UI Debug/Release 构建 0 错误。未接真实相机人工验收，VS 设计时待验收。

- 2026-09-27 修复相机图像源连续运行“停不下来”的交互重入：`SunEyeVision_211208.txt` 显示 21:14:14.799 发出停止请求、21:14:14.827 确认停止，但 21:14:14.854 又启动新连续执行，说明该次取帧取消生效，问题在停止后按钮快速再次触发。`WorkflowTabControlViewModel` 对已接受的停止请求增加 500ms 连点保护，抑制重复停止和停止确认后的立即重启；标签按钮提示由根据事后 `IsRunning` 误报“开始连续运行”改为“正在停止”。新增 STA 交互回归测试 1/1 通过；UI Debug/Release 构建 0 错误，`git diff --check` 通过。未用真实相机运行验收，VS 设计时/用户运行时待用户复测；未启动 Release UI 程序或连接相机。

- 2026-09-27 修复图像源取帧成功但结果画布空白、关闭调试窗口抛异常：真实日志已证明海康相机取到 4024×3036 Mono8 帧；`ImageLoadResults.GetPropertyMap()` 把 `SourceKind` 枚举声明为通用输出，`GetOutputVariables()` 因 `SdkTypeVocabulary` 不支持该类型而在结果面板刷新时抛异常。保留内部强类型 `SourceKind`、可空 `FrameId` 和 `FrameTimestamp`，输出契约改用对应字符串只读属性，避免新增非通用全局变量类型。新增本地/相机两模式完整输出变量测试；Debug/Release 各 6/6 通过，完整解决方案 Debug 与 Release 均构建成功（0 错误）。当前有一个 UI 实例在运行，未启动第二个 Release 实例以免争用真实相机；VS 设计时与用户运行时画布显示仍待验收。

- 2026-09-27 修复图像源节点双击无调试窗口：画布双击命令原本正常，但 `ImageLoad` 未实现插件调试控件，`NodeOpenDecisionChain` 返回空控件后 UI 直接退出。现由 UI 宿主针对图像源节点包装现有 `ImageSourceSettingsControl` 为标准调试窗口，直接编辑当前 UI 节点配置；关闭窗口时刷新右侧配置面板。UI Debug 构建成功（0 错误，输出 `dev\Debug`），UI Release 构建成功（0 错误）；`git diff --check` 通过。未进行 VS 设计时或用户运行时双击验收，需用户从 Debug exe 复测窗口可见、配置修改及关闭后状态。
- 2026-09-27 按用户复测意见调整通讯方案恢复：移除新建/详情中的“打开方案时连接”开关，方案加载时直接尝试连接 `Solution.Communications` 中已启用的设备；旧 `autoConnect=false` 不再阻止连接，新保存方案移除遗留键。相机管理的加载时机作为参考，但通讯无需相机的独立自动连接选项。仅做 Debug 构建与测试，结果见本轮交接单。

- 2026-09-27 Codex 在 `feature/comm`、基线 `313f2457` 上实施通讯架构方案：新增 Plugin.SDK 通用通讯契约；将 TCP/UDP/串口、Modbus、S7、CIP、OPC UA 驱动迁入 `DeviceDriver/Communications`，建立内部工厂注册及不可变配置快照；应用级服务接管方案打开/关闭、自动连接、退避重连、连接与轮询生命周期，通讯窗口关闭只解除订阅；地址表首地址/容量随方案保存，越界调整拒绝并提示受影响地址，清空地址块需确认；配置保存采用同目录临时文件、原子替换与 `.bak` 恢复副本，运行值/短期记录退出方案写入。只做 Debug 构建与测试，未做 Release，也未做 VS 设计时/真实设备人工验收。后续需确认 UI 按实际设备交互、超大地址表性能，以及明确的工作流协议操作消费场景。详见 `HANDOFF-communication-20260927.md`。

- 2026-09-25 为一次性定位方案启动问题新增第二层真实调用链回归测试：`SolutionManager.LoadSolutionOnly + SetCurrentSolution`、STA 线程上的 `MainWindowViewModel.OnCurrentSolutionChanged -> LoadSolutionToWorkflowTabs`。首次测试在未创建 `Application.Current` 的测试环境中命中 `UpdateUndoRedoCommands` 的空引用；补齐真实 WPF `Application` 后 5/5 测试全部通过（Workflow、Devices、Repository.Load、SolutionManager 链路、STA 工作流标签页链路）。当前证据表明核心加载和工作流 UI 加载链路在规范 WPF 环境下均可完成，之前单测异常是测试环境缺失 Dispatcher，不是生产代码异常；仍需用新 exe 采集一次完整 `[StartupTrace]` 日志，确认实际启动实例是否走到 `MainWindow.Show`。
- 2026-09-25 将生产启动顺序中的 `ServiceInitializer.InitializeServices()` 也加入 STA 回归测试后，`MainWindowViewModel + CurrentSolutionChanged` 链路仍通过（1/1）；说明当前日志系统/服务初始化本身未能在测试环境复现 `Workflow constructor begin` 后停止的问题。最新已有日志 `SunEyeVision_002531.txt` 仍是旧进程记录，下一步必须用当前测试通过的构建启动一次并拿到新时间戳日志，避免继续分析旧日志。

- 2026-09-25 新增 `SolutionLoadRegressionTests`，分别测试 Workflow 反序列化、Devices JSON 读取和 `SolutionRepository.Load`，每阶段带超时；测试执行成功：3/3 通过，总耗时约 1 秒。说明当前方案文件和核心反序列化路径在脱离 UI 后均可完成，之前 UI 日志中的卡点很可能来自 UI/日志埋点/重复探针路径，而不是 JSON 数据本身。

- 2026-09-25 按“一次性定位并避免设备字段阻塞”方案实施：方案加载时不再使用通用 `JsonSerializer` 反序列化 `Devices`，改为从 `JsonElement` 明确读取设备持久化字段和 Settings；通用 Solution 反序列化前移除 `Devices` 属性，完成后注入手动恢复的设备列表。UI Debug 构建成功，0 个错误、829 个既有警告，输出更新至 `dev\Debug`。待用户复测方案加载。

- 2026-09-25 查看最新日志 `logs/2026/09/25/SunEyeVision_001947.txt`：`RuntimeIdentity` 已正常记录，确认实际运行的是 `D:\MyWork\SunEyeVision_V2\dev\Debug\SunEyeVision.UI.exe`，版本 `2.0.0.1+9603e18d...`，PID=31280。方案加载中 Workflow、GlobalVariables 和设备字段已完成至 `Port`；`Port` 已完成，当前停在 `Device field begin | field=Status, type=DeviceStatus`，说明不是旧 exe，下一步继续检查 Status/后续字段。

- 2026-09-25 将 `RuntimeIdentity` 调用移动到 `Service.Initialize` 完成之后，确保启动身份日志在日志系统初始化后写入；UI Debug 构建成功，0 个错误、740 个既有警告，输出更新至 `dev\Debug`。

- 2026-09-25 查看最新日志 `logs/2026/09/25/SunEyeVision_001650.txt`：本次已进入新增设备字段探针；设备字段依次完成到 `IpAddress`，在 `Device field begin | field=Port, type=Int32` 后没有继续。Workflow 和 GlobalVariables 均已完成。日志中暂未看到 `RuntimeIdentity`，说明该启动身份日志调用时机过早或未被当前日志系统接收，后续应在日志系统初始化完成后再记录一次。

- 2026-09-25 增加启动运行身份日志 `RuntimeIdentity`：记录 PID、程序集位置、进程路径、InformationalVersion、FileVersion、文件修改时间和文件大小，用于确认测试实际运行的 exe；UI Debug 构建成功，0 个错误、740 个既有警告，输出更新至 `dev\Debug`。

- 2026-09-25 查看最新日志 `logs/2026/09/25/SunEyeVision_001059.txt`：本次启动完成 JSON 文档解析后停在 `Property probe begin | property=Workflows`，没有进入 `WorkflowJsonConverter`，也没有到达新增的设备字段探针；本次未能获得 `Devices` 字段结果，需确认测试启动的是最新 exe 且无其他实例干扰后再复测。

- 2026-09-25 重试构建包含设备字段级探针的 UI Debug 版本，已成功：0 个错误、740 个既有警告，`dev\Debug` 已更新。此前 DLL 占用已释放。

- 2026-09-25 构建包含设备字段级探针的 UI Debug 版本失败：`SunEyeVision.Workflow.dll` 和 `CommonControl.dll` 被残留进程占用，MSB3021/MSB3027，共 4 个错误、121 个警告；代码已编译到中间产物，但 `dev\Debug` 未能完成更新。需释放占用进程后重试。

- 2026-09-25 针对 `Devices` 反序列化卡点增加设备字段级探针：先逐设备记录基础字段、`Settings`、时间字段的独立反序列化开始/完成/异常，再继续原 `List<Device>` 探针，用于确认具体阻塞字段。本轮已修改 `SolutionRepository.cs`，待构建后测试。

- 2026-09-25 查看最新日志 `logs/2026/09/25/SunEyeVision_000521.txt`：本次单实例测试已正常完成 JSON 解析、Workflow 反序列化和 GlobalVariables 反序列化，随后稳定停在 `Property probe begin | property=Devices`；说明当前确定是 `Devices` 字段反序列化卡住，尚未进入相机连接或主界面显示。

- 2026-09-25 查看最新日志 `logs/2026/09/24/SunEyeVision_235910.txt`：本次新进程在 `JsonDocument.Parse begin` 后没有继续输出，尚未进入 Workflow/Devices 探针；进程仍存活且无主窗口句柄。与上一轮已进入 `Devices` 探针的日志相比，本次可能受残留旧进程/多实例日志竞争影响，需先关闭全部旧 `SunEyeVision.UI.exe` 后再单实例复测。

- 2026-09-24 查看新构建后的日志 `logs/2026/09/24/SunEyeVision_235641.txt`：最新埋点已生效，Workflow 已完整反序列化（Id、Name、构造函数、Nodes、Connections 全部完成），GlobalVariables 也完成；随后停在 `SolutionRepository` 的 `Property probe begin | property=Devices`，没有出现 Devices probe completed，也未进入完整 Solution 反序列化完成或主界面显示。当前卡点已从 Workflow 收敛到 `Devices` 字段的反序列化，下一步检查 Device 类型/转换器及其中的初始化逻辑。

- 2026-09-24 按用户建议重新构建包含最新方案加载细粒度埋点的 UI Debug 版本：`dotnet build src/UI/SunEyeVision.UI.csproj -c Debug --no-restore -m:1 -v:minimal`，成功，0 个错误、829 个既有警告，输出已更新至 `dev\Debug`。待关闭旧进程后用新 exe 复测方案加载。

- 2026-09-24 查看最新日志 `logs/2026/09/24/SunEyeVision_235003.txt`：文件读取、序列化选项初始化、JSON 文档解析均完成；顶层 `Workflows` 属性探针已开始，并进入 `WorkflowJsonConverter.Read`。日志已记录“Workflow JSON parsed”，但未出现“Workflow instance created”，说明当前卡点位于读取 `Id/Name` 或执行 `new Workflow(id, name)` 之前/期间。已增加基本属性读取和 Workflow 构造函数前的细粒度埋点，下一次运行可区分具体位置。

- 2026-09-24 增加方案顶层字段探针：在完整 `Solution` 反序列化前，依次记录 `Workflows`、`GlobalVariables`、`Devices`、`Communications`、`DatabaseConfiguration`、`ExecutionStrategy`、`VersionHistory` 的独立反序列化开始/完成/异常及耗时；用于进一步锁定顶层字段卡点。UI Debug 构建成功（0 错误、829 个既有警告）。

- 2026-09-24 继续增加方案反序列化埋点：`SolutionRepository` 在完整反序列化前记录 JSON 顶层属性及 Workflows/GlobalVariables/Devices/Communications 数量；`WorkflowJsonConverter` 记录工作流 JSON 解析、实例创建、节点数组、连接数组和完成阶段。UI Debug 构建成功（0 错误、829 个既有警告）。

- 2026-09-24 查看新埋点日志 `SunEyeVision_234252.txt`：已确认文件读取完成（1646 字符）、`WorkflowSerializationOptions.Default` 初始化完成（1ms），随后停在 `JsonSerializer.Deserialize<Solution> begin`；进程仍存活但 `MainWindowHandle=0`，没有进入 `LoadSolutionOnly returned`、`SetCurrentSolution` 或主窗口显示。当前卡点已精确收敛到 `JsonSerializer.Deserialize<Solution>` 内部，下一步需检查 `WorkflowJsonConverter`/多态参数转换或增加转换器级埋点。

- 2026-09-24 增加方案加载细粒度日志：`SolutionConfigurationDialogViewModel` 记录 LoadSolutionOnly 调用前后；`SolutionManager` 记录 Repository.Load 前后和耗时；`SolutionRepository` 分段记录文件读取、序列化选项初始化、`JsonSerializer.Deserialize<Solution>` 开始/结束、线程号、字符数和耗时，用于定位无主窗口实例卡在反序列化的具体阶段。UI Debug 构建成功（0 错误、740 个既有警告）。

- 2026-09-24 检查最新日志：`SunEyeVision_233144.txt` 与 `SunEyeVision_233211.txt` 均停在 `SolutionRepository` 的“开始反序列化解决方案”之后，没有进入 `LoadSolutionOnly completed`、`SetCurrentSolution` 或相机连接恢复；对应进程仍存活但无主窗口句柄，属于启动卡死/窗口不可见，不是已确认的进程闪退。此前 `SunEyeVision_232501.txt` 同一方案曾正常显示主窗口并完成相机连接。当前系统同时存在多个 `SunEyeVision.UI.exe` 实例，后续需先清理残留实例并对反序列化前后增加更细日志。

- 2026-09-24 调整相机连接生命周期：自动连接从 `CameraManagerViewModel` 构造函数移到方案加载完成后的 `App.RestoreCameraConnections`；打开方案后先按已保存传输类型发现、注册并自动连接相机，再显示主界面。相机管理器打开时只刷新运行时连接状态，不再触发发现/自动连接；无相机方案跳过发现，避免默认模拟设备混入。UI Debug 构建成功（0 错误、740 个既有警告）。

- 2026-09-24 分析最新日志 `SunEyeVision_231708.txt`：真实设备确实只有 1 台，但恢复日志显示“持久化设备=1，重新发现设备=2，运行时注册=0”；第二台来自默认 `SimulatedCameraDiscoveryProvider`，而运行时发现结果又未注册到 `CameraRuntimeManager`，因此自动连接最终故障。已修复：恢复时按已保存的传输类型过滤发现器，并显式 `Runtime.RegisterDevices(discovered)`；完整 UI Debug 构建成功（0 错误、740 个既有警告）。

- 2026-09-24 定位相机重启后故障：方案文件 `C:\Users\houzhongjie\Desktop\1.solution` 已确认保存设备、稳定 DeviceId、IsEnabled=true、AutoConnect=true，排除配置未持久化。增强 `CameraManagerViewModel.RestoreAutoConnectionsAsync`：记录重新发现/运行时注册数量，按 DeviceId、序列号+厂商+传输类型、MAC+传输类型重新绑定设备身份，绑定变化后回写方案，并记录自动连接开始/结束/异常详情。UI Debug 构建成功（0 错误、740 个既有警告）；待用新构建重启后打开相机管理器验证日志。

- 2026-09-24 用户关闭运行实例后重新执行完整 UI Debug 构建，成功：0 个错误、740 个既有警告；输出已更新至 `dev\Debug`。

- 2026-09-24 用户要求构建：完整 UI Debug 构建仍因运行实例占用 `dev\Debug\CommonControl.dll` 失败；未结束用户进程，改用 `--no-dependencies` 仅构建 UI 项目验证本次 XAML，结果 0 错误、728 个既有警告。关闭运行实例后可执行完整构建。

- 2026-09-24 按用户比例要求调整相机管理器尺寸：基于 600×420，宽度降低 30% 为 420，高度增加 20% 为 504；最小尺寸同步为 400×360。构建因运行中程序再次占用 `dev\Debug\CommonControl.dll` 失败（2 个文件占用错误），XAML 修改已完成，关闭运行实例后可重试。

- 2026-09-24 二次收紧相机界面：设备管理器默认尺寸由 720×500 调整为 600×420，最小尺寸为 520×320；相机设置窗口图像/参数比例由 6:5 调整为 7:4，参数分类栏由 170 调整为 160，进一步突出图像显示。UI Debug 构建成功（0 错误、740 个既有警告）。

- 2026-09-24 优化相机预览窗口图像/参数区域比例：主布局由图像 5*、参数 6* 调整为图像 6*、参数 5*；参数分类导航宽度由 190 调整为 170，参数编辑区最小宽度由 520 调整为 460，给图像预览更多空间。UI Debug 构建成功（0 错误、740 个既有警告）。

- 2026-09-24 优化相机管理器窗口尺寸和布局：移除已无实际用途的右侧说明分区，设备列表铺满主体区域；默认窗口由 1020×820 调整为 720×500，最小尺寸调整为 560×360，保留设备名称、预览按钮和连接 ToggleButton 的显示空间。Debug 构建因运行中程序占用 `dev\Debug\CommonControl.dll` 失败（2 个文件占用错误，非本次 XAML 编译错误），待关闭运行实例后重试。

- 2026-09-24 复核用户仍看不到配置窗口的问题：当前 `SunEyeVision.UI` PID 57140 的主窗口为“太阳眼视觉 - 打开解决方案”，Win32 状态为可见、未最小化，并已强制置前；置前后系统前台窗口仍为该配置窗口。结合其运行在 Session 1/Default 桌面，应用内部启动和配置窗口显示均正常，后续应优先核对远程查看端是否连接到同一交互会话/桌面，不再继续修改配置窗口业务流程。

- 2026-09-24 验证启动页优化日志 `logs/2026/09/24/SunEyeVision_221542.txt`：配置模式下 `Splash closed before configuration dialog` 在 +1829ms 记录，随后 `SolutionConfigurationDialog constructed` 与 `ShowDialog begin`；日志出现“新建”按钮悬停事件，说明启动页已关闭、配置界面已显示并可交互。由于用户尚未选择/新建方案，没有进入主窗口显示阶段，属于预期行为。

- 2026-09-24 启动页优化第一阶段已实施：增加阶段说明文本和 `UpdateStage(stage, progress, detail)`，启动页显示“正在启动/加载模块/初始化服务/初始化解决方案/打开工作区/显示主界面/启动完成”等真实阶段；配置方案模式进入 `ShowConfigurationDialog` 前立即关闭启动页，避免遮挡“打开解决方案”窗口；跳过配置模式保留启动页直到主窗口 `Show()` 前后。新增 `CloseImmediately`，保留已有 UI 消息泵以保证同步初始化期间阶段可见。UI Debug 构建成功（0 错误、740 个既有警告）；`git diff --check` 通过，未实机验证。

- 2026-09-24 进一步定位 `SunEyeVision_220255.txt` 对应实例：Win32 枚举确认配置窗口 `太阳眼视觉 - 打开解决方案` 实际存在，句柄 `0xCF1456`，坐标 `(373,193)-(1333,833)`，`visible=True`、未最小化；启动页句柄 `0x1721850` 也仍可见并覆盖在同一桌面。已临时隐藏启动页并将配置窗口置前验证。根因是 `ShowConfigurationDialog` 在 `ShowDialog()` 期间没有关闭启动页，导致用户视觉上只看到启动页；后续应在进入配置对话框前关闭启动页并激活配置窗口。

- 2026-09-24 分析启动日志 `logs/2026/09/24/SunEyeVision_220255.txt`：本次启动没有卡在工作流加载，`Startup decision resolved | decision=ShowConfiguration`；由于 `Desktop\solution_settings.json` 不存在、已知方案数为 0，主窗口保持 `Collapsed` 是当前设计行为，程序进入 `SolutionConfigurationDialog.ShowDialog` 等待选择/新建方案。日志中已出现配置对话框“新建”按钮悬停记录，说明配置对话框实际已创建并接收输入；启动页未关闭导致用户容易误以为仍停在启动页。建议后续在 `ShowConfigurationDialog` 前先关闭启动页，或让配置对话框明确置前显示。

- 2026-09-24 根据启动日志 `logs/2026/09/24/SunEyeVision_215853.txt` 继续细化启动埋点：已确认 `Service.Initialize`、`SolutionManager.Initialize`、启动决策和 `MainWindow` 构造完成；程序在 `SetCurrentSolution` 触发的工作流加载阶段停止输出，尚未到达 `mainWindow.Show()`。新增 `OnCurrentSolutionChanged`、工作流列表遍历、`CreateWorkflowTab.SetNodes/Register/Tabs.Add` 前后埋点，用于区分具体工作流加载步骤。UI Debug 构建成功（0 错误，既有警告）；待使用新构建重新启动采集日志。

- 2026-09-24 增加应用启动诊断埋点：在 `App.OnStartup` 覆盖线程池预热、启动页、插件加载、服务初始化、方案初始化、启动决策等阶段；为主窗口增加 `SourceInitialized/Loaded/ContentRendered/Activated/StateChanged/Closed` 生命周期日志，记录窗口句柄、可见性、WindowState、坐标、尺寸、任务栏状态和 PresentationSource，并记录 `Show()` 后的 UI 空闲状态。日志统一带 `[StartupTrace]` 和耗时，启动早期日志失败不会阻断应用。UI Debug 构建成功（0 错误、740 个既有警告，输出 `dev\Debug`）；未启动新构建实测。

- 2026-09-24 相机管理器最终方案第一阶段已实施：设备卡片新增“预览”按钮，继续打开现有 `CameraPreviewWindow`；管理器右侧移除参数详情面板，改为设备管理操作说明；普通选中设备不再触发参数树读取，只有点击“预览”才把设备交给预览会话并按需加载参数。连接/断开仍由卡片 ToggleSwitch 管理，连接会话继续复用应用级运行时。UI Debug 构建成功（0 错误、874 个既有警告，输出 `dev\Debug`）；`git diff --check` 通过，未做真实相机运行验收。

- 2026-09-24 新增《相机管理器最终重构方案》本地文档：明确设备管理器仅负责设备列表和连接入口，现有预览窗口继续承载采集与全部参数调试；定义统一连接会话、参数状态解耦、方案持久化、窗口/方案/应用生命周期和分阶段实施计划。本轮仅保存方案文档，未修改业务代码，未构建。
- 2026-09-24 修复相机管理器窗口生命周期：`CameraRuntimeComposition` 提升为 `ServiceInitializer` 持有的应用级共享运行时；关闭设备管理器不再释放相机连接，重新打开时可接管已有会话；新运行时先重新发现并注册持久化设备，避免 `is not registered`；应用退出时统一释放运行时。UI Debug 构建成功（0 错误、755 个既有警告，输出 `dev\Debug`），未做真实相机开关窗口验收。

- 2026-09-24 相机设备管理持久化第一阶段已实施：复用当前方案 `Solution.Devices` 保存相机绑定，不另建独立配置源；方案打开时恢复名称、设备身份、厂商/型号、IP、启用状态和自动连接配置；添加、删除、重命名、启用状态和自动连接配置变化后写回当前方案；未保存方案先更新内存，后续保存方案时落盘；运行时连接状态和 SDK 会话不序列化。UI Debug 构建成功（0 错误、740 警告，输出 `dev\Debug`），未做真实相机重启恢复验收。

- 2026-09-24 新增《相机设备管理持久化方案》本地设计文档：明确设备配置跟随当前方案保存到本地，区分 `CameraBinding` 持久化模型与运行时 `CameraDevice/CameraSession`，定义稳定身份匹配、离线保留、自动连接、保存时机、版本迁移和验收标准。本轮仅新增文档，未修改业务代码，未构建。

- 2026-09-24 修复相机预览关闭后的连接状态：记录打开预览前的连接状态，仅由预览临时建立的会话在关闭时释放；预览前已通过设备列表连接的相机，关闭预览后保持连接。代码修改完成，`git diff --check` 通过；本次构建因 `dev\Debug\CommonControl.dll` 被运行中进程占用而失败，需关闭占用程序后重试，未做真实相机运行验收。

- 2026-09-24 相机设备管理详情继续优化：新增相机后自动发起连接；右侧“基本信息”改为常用属性平铺面板，顶部保留“预览”按钮，不再使用预览选项卡或常用属性分类树；参数修改在未打开预览时也会通过临时诊断会话自动写入设备。XAML XML 与 `git diff --check` 通过；UI Debug 构建成功（0 错误、740 警告，输出 `dev\Debug`），未做真实相机运行验收。

- 2026-09-24 相机设备列表按新版通讯设备列表样式优化：将左侧相机 `DataGrid` 改为无表头的扁平 `ListBox` 设备行，显示序号、名称、厂商/接口/连接状态、状态圆点；保留右键重命名/删除；右侧 `ToggleSwitch` 改为连接/断开控制并复用相机运行时会话。XAML XML 与 `git diff --check` 通过；UI Debug 构建成功（0 错误、740 警告，输出 `dev\Debug`），未做真实相机运行验收。

- 2026-09-24 按最终方案继续实施：将 UI 的“高级属性=全部有效参数”改为“高级功能”视图；完整参数仅保留在“属性树”，高级功能按参数/分类识别 ROI、AOI、查找表、事件控制信息、Chunk 数据和用户集操作；补充 Shading Correction、Transfer Control 等分类本地化。`git diff --check` 通过；Debug 构建已完成（0 错误、744 警告，输出 `dev\Debug`），未构建 Release，未实机验证。

- 2026-09-24 原生相机属性树第一阶段实施：`CameraParameterDescriptor` 新增 `SourceOrder`，MVS XML 枚举保留原生参数顺序；UI 增加“属性树”主视图，原生分类直接挂在属性树下，常用属性分为基本属性/ISP/传输层控制快捷分组，参数列表按 XML 顺序显示，不再按中文名称排序；补充 `GevSCPSPacketSize`、`GevSCPD`、`GevPauseFrameReception` 的传输层标准映射。`git diff --check` 通过，Debug 构建已完成并输出到 `dev\Debug`，未构建 Release；未实机验证。

- 2026-09-24 相机原生属性树最终方案已保存：更新 `docs/01-方案设计/2026/09/相机管理器与预览调参界面最终优化方案-20260923.md`，明确以 GenICam XML 原生分类/层级/顺序为权威，常用属性作为快捷视图，ROI/AOI/LUT/事件/Chunk/用户集采用专用功能视图，并补充 ASCII UI/架构示意、参数权限、Selector 依赖、采集触发和多品牌适配边界。本轮仅更新文档，未改代码、未构建。

- 2026-09-23 MVS 单次采集停止延迟优化（本轮）：参考 BasicDemo 的停流顺序，将 `MV_CC_StopGrabbing_NET` 提前到等待接收循环退出之前，以便打断阻塞中的 `GetImageBuffer`；停流引起的 SDK 取帧错误识别为正常取消；新增 SDK 停流调用耗时和接收循环退出等待耗时日志。`git diff --check` 通过。用户提出构建，但按“构建前要提出审批”要求，尚未申请/执行构建；待审批后只构建 Debug 到 `dev\Debug`，不构建 Release。未实机验证。

- 2026-09-23 修复设备占用解除后的参数树恢复：用户反馈先被厂商相机软件占用导致预览连接报错，关闭厂商软件后再次预览虽已连接但参数列表空白；删除重加设备才恢复。根因是选中设备时的首次 Schema 读取失败后清空了参数集合，而后续 Preview 只连接、不重试 Schema。现增加 Schema 加载成功状态，预览连接成功时若此前读取失败，则通过现有会话重读完整 Schema 并重建分类树，失败时在状态区显示明确原因；不重复创建 SDK 句柄。Debug 构建后续获批并成功（0 错误、757 警告），输出 `dev\Debug`；不构建 Release。

- 2026-09-23 相机预览采集按钮与单次采集修正：空闲主按钮命名为“连续采集”，连续流运行时显示“停止采集”；单次采集期间不提供取消，单次按钮禁用、主按钮显示“连续采集”并禁用，单帧完成后自动恢复。定位到 MVS 停流期间 `GetImageBuffer` 超时会被当作异常抛出，导致 `StopAcquisitionAsync` 未复位状态；现将取消后的帧超时作为正常退出，并确保 Host 无论 StopStream 结果如何都复位 UI 状态。单次采集仍遵循相机当前触发配置，不更改参数。MVS BasicDemo 停止行为已核对：先清接收循环标志，再直接调用 `MV_CC_StopGrabbing_NET`；没有等待队列清空/接收线程 Join，已取到的当前帧仍会处理并释放。获批后 Debug UI 构建成功（0 错误、757 警告），输出 `dev\Debug`；未实机验证；不构建 Release。

- 2026-09-23 相机预览采集按钮简化与单次采集：工具栏把开始/停止合并为状态切换按钮，新增“单次采集”。单次采集不改相机参数，连续模式等下一帧、软触发模式主动触发一次、硬触发模式等待一次外部触发，收到图像后停止；原“触发一次”保留用于连续触发调试。获批后 Debug UI 构建成功（0 错误、742 警告），输出 `dev\Debug`；预览窗口 XAML XML 解析和 `git diff --check` 通过。未启动程序、未连接相机实测；不构建 Release。

- 2026-09-23 相机参数低卡顿写入优化（本轮）：MVS 取帧不再占用参数读写互斥锁，另用独立取帧锁防止并发获取帧；这样 UI 写曝光/增益时不必等 `GetImageBuffer` 的最长 2 秒超时。TriggerMode/TriggerSource 在线写入，不再强制停流或重建完整参数树；读回新触发配置时同步更新 MVS 软件触发有效状态。AcquisitionMode 与 ROI/像素格式仍保留停流/Schema 刷新。依据是 MVS BasicDemo 的取帧线程同时允许 UI 线程直接写触发节点。Debug UI 构建已获批并成功：0 错误、757 警告，输出 `dev\Debug`；`git diff --check` 通过。未启动程序、未连接真实相机验证；未构建 Release。

- 2026-09-23 相机参数实时生效日志复核与修复：用户日志 `logs/2026/09/23/SunEyeVision_181337.txt` 显示相机连续帧正常提交（18:13:56 起；至 18:15:31 至少 720 帧），但 18:14:09 曝光 NumericUpDown 获得焦点、18:14:13 失焦后，没有任何参数变更/自动提交/SDK 写入记录。代码根因：设备选择后的首次参数加载走 `LoadSchemaAsync`，这条路径创建参数 ViewModel 时漏订阅 `PropertyChanged`；仅后续完整 Schema 刷新路径有订阅，导致首次预览会话参数编辑完全没有触发自动写入。现已补齐首次加载事件订阅、重建列表时解除旧订阅，并在自动提交前记录节点/值/RequiresStopped/AffectsSchema 和状态栏提示。获用户审批后 UI Debug 构建成功（0 错误，742 警告），输出 `dev\\Debug`；未启动/实机验证，不构建 Release。

- 2026-09-23 相机参数即时生效修订：响应用户对照 MVS/VM 软件“改参数立即生效”的交互预期，参数项变更后自动调用 SDK，连续输入延时 350ms 合并；普通参数只读回所改节点，Selector/模式/图像格式等 `AffectsSchema` 参数才重建完整 Schema；停流参数仍自动暂停/恢复；采集按钮不再有单独“应用参数”，开始采集仍会提交尚未完成的待修改项。关闭窗口会取消待执行 debounce 并等待参数提交结束再释放会话。获批后 UI Debug 构建成功（0 错误，757 警告），输出 `dev\\Debug`；未启动程序或真实设备验收；不构建 Release。

- 2026-09-23 相机设置与采集闭环第二阶段：实施“常用标准功能/高级 SDK 节点”分层、统一顶部应用/开始/停止/触发操作栏、打开窗口不自动取流、开始前应用待提交参数、停止保留最后一帧、软触发状态约束；新增跨适配器标准化采集配置契约，MVS 与模拟适配器映射厂商/模拟参数，单帧首帧后停止；参数修改根据 `RequiresStopped` 暂停/恢复采集，MVS 也拒绝取流中写入停流参数。参数面板删除重复应用按钮。获用户审批后 UI Debug 构建成功（0 错误，742 警告），输出 `dev\\Debug`；`git diff --check` 与相机 XAML XML 解析通过；未启动应用或真实设备验收。未构建 Release。

- 2026-09-23 相机管理器与预览调参整合：第一阶段已实施，管理器详情收敛为基本信息，预览入口仍在基本信息；参数面板与图像预览同窗，点击预览自动连接，关闭设置窗后释放会话；管理器宽度收窄。用户补充多品牌适配及显式“应用/开始/停止/触发”要求后，完整方案已更新至 `docs/01-方案设计/2026/09/相机管理器与预览调参界面最终优化方案-20260923.md`。下一阶段需将常用标准相机功能设为默认视图、完整 SDK 节点留在高级属性，并实现显式采集控制和按适配器处理参数修改时机。目前第一阶段 Debug 构建通过（0 错误，746 警告，输出 `dev\Debug`）；最终方案新增部分尚未实现/验收。未构建 Release，未实机验证。
- 2026-09-23 相机预览问题日志证据链：新增 `CameraDiagnosticScope` 与 `camera-trace`，串联参数提交（旧值→新值）、MVS逐节点 SDK 写入返回码、设备真实回读、预览读回的 AcquisitionMode/TriggerMode/TriggerSource、预览流模式、MVS取帧状态、帧尺寸/像素格式/采样均值、帧到达及图像提交控件结果。MVS `MV_E_NODATA (0x80000007)` 与其他 SDK 取帧错误分开记录；无数据日志首发并按 5 秒节流，帧数据首帧及每 120 帧采样记录，避免日志刷屏。软触发命令和曝光/增益自动控制切换也记录节点及 SDK 返回码。日志目标为应用文件日志及 UI，按 camera id 和 trace id 过滤。`git diff --check`、相机 XAML XML 解析及调用点静态核对通过；**2026-09-23 UI Debug 构建成功**（`dotnet build src/UI/SunEyeVision.UI.csproj -c Debug --no-restore -m:1`，0 错误、1106 警告），产物输出 `dev\Debug`。未构建 Release，未启动应用或做实机验证。
- 2026-09-23 相机预览与真实参数联动：对照本机 MVS BasicDemo，确认采集模式/触发模式由相机实际节点控制，TriggerMode 关闭时连续取帧，TriggerMode 开启时按触发源等待；预览仅显示相机帧，不调度工作流、不覆盖设备参数。修复预览只在 TriggerMode/Source 改变时刷新的问题：任意参数应用后重新读取 AcquisitionMode/TriggerMode/TriggerSource 并重启预览；状态栏显示 SDK 读回模式。修复预览刷新等待流结束导致参数保存挂起：采集流改为后台观察，启动/重启操作立即返回。MVS 取帧改用官方示例的 GetImageBuffer/FreeImageBuffer 配对，取帧超时改为继续等待而非终止预览；手动修改曝光/增益前按 MVS 示例关闭对应自动控制。已通过 `git diff --check` 与预览 XAML XML 解析；用户批准后 UI Debug 构建成功（0 错误，761 警告），输出 `dev\Debug`；未启动程序/实机验证。
- 2026-09-23 相机参数可编辑状态与所见所得第一阶段：读取失败/无枚举选项/不支持类型的字段不得显示为可编辑文本框；数值、枚举、布尔控件按 SDK 权限及实际值状态统一禁用。参数页导航精简为“常用参数/全部属性”；保存后重新读取完整 Schema 与设备值（写入部分失败时也尽力回读），刷新依赖参数的权限和选项并保留当前分类。`git diff --check`、参数页 XAML XML 解析通过；UI Debug 构建成功（0 错误，742 警告），输出 `dev\Debug`；未运行程序、未实机验收。基线 `8a66d177884bdb038b6082b1b36c2041464e6237`，保留工作区已有改动。
- 2026-09-23 相机参数控件映射修正：只读数值/布尔/枚举显示只读值，不再呈现 NumericUpDown、ToggleSwitch 或禁用 ComboBox；只有 SDK 返回至少两个选项的可写枚举才显示下拉框。MVS 枚举值由 `MV_CC_GetEnumValue_NET` 与 `MV_CC_GetEnumEntrySymbolic_NET` 获取；模拟适配器仍用固定测试数据。静态检查通过；这是上条 Debug 构建后的新增修改，尚未重新构建/实机验收。
- 2026-09-23 预览调参闭环调整：移除预览窗口软触发按钮，软件触发统一从动态参数树的 `TriggerSoftware` 命令执行；预览窗口复用参数面板相机会话。修改 TriggerMode/TriggerSource 后重新读取配置并重启预览。MVS 预览启停不再临时改写/恢复 TriggerMode 和 TriggerSource，避免关闭时覆盖用户新值。曝光/增益手动设置后若自动控制仍开启，状态提示其可能接管每帧亮度。UI Debug 构建通过（0 错误，757 警告），输出 `dev\Debug`；未启动程序、未实机验收。

- 基线提交：当前 `main` 的最新已提交版本。
- 当前执行者：Codex。
- 当前任务：相机参数动态节点接入与预览按设备触发配置运行（保留显式强制连续模式）；首轮实现已完成，待真实设备验证。
- 修改权限：`src/DeviceDriver/Cameras/`、`src/Plugin.SDK/Devices/Camera/`、`src/UI/Features/Camera/`、`tests/`、`docs/`。
- 当前任务基线与限制：保留工作区已有未提交内容；开发验证仅构建 Debug，Release 留待发布前；如本轮实现完成，追加记录实际改动与验证结果。
- 本轮相机管理器改动（2026-09-23）：增加设备重复识别与添加拦截；搜索可取消并保留已发现结果；连续预览不受触发设置影响，另增触发调试模式（软触发单帧按钮/硬触发等待提示），适配 MVS 与模拟相机。验证：`dotnet build src/UI/SunEyeVision.UI.csproj -c Debug --no-restore` 成功（0 错误，742 警告）；未构建 Release，未进行连接真实相机的运行验收。
- 参数与预览优化实施（2026-09-23）：MVS 从 SDK GenICam 类别树动态枚举参数节点，读取节点类型/访问权限/可见级别；接入整数、浮点、布尔、字符串、枚举读写和命令执行；参数 UI 按节点类别路径构建可折叠树，不再静默过滤读取失败项；预览默认按设备已应用 TriggerMode/TriggerSource 运行，强制连续作为显式覆盖并在状态栏说明。Debug UI 构建通过（0 错误，742 警告）；未构建 Release，未连接真实相机验证。后续仍需真实设备节点盘点/完整性报告，并验证 SDK 树遍历、命令节点与触发模式实机效果；底层 Register/Port 等非普通参数节点当前不开放通用编辑。
- 相机管理 UI 收敛优化（2026-09-23）：预览移除“按相机参数预览/强制连续预览”双模式按钮，打开窗口即按相机真实触发配置启动；唯一“软触发一次”按钮常驻，仅在 TriggerMode=On 且 TriggerSource=Software 时可用，其他配置禁用并说明原因。参数树为常见 GenICam 类别和节点增加中文显示映射，未知 SDK 节点保留原名避免误译；树行加宽并调整标签列；管理窗口默认 1040×760、最小宽度 850。经审批执行 Debug UI 构建成功（0 错误，742 警告）；未构建 Release，尚未实机验收。
- 相机参数树与管理器交互修正（2026-09-23）：扩展相机节点/枚举项中文映射，并将 MVS AM_NI/AM_NA 访问状态显示为中文；修复主题 ComboBox 模板遗漏命名 ItemsPresenter 的问题；树选中/悬停改用主题色，增加参数搜索及全部展开/折叠；连接/断开和预览按钮移动到 Tab 外公共操作栏，连接状态、IP、延迟、帧率移动到详情底部状态栏。经审批执行 Debug UI 构建成功（0 错误，757 警告）；XAML XML 解析和 git diff --check 通过；未构建 Release，未进行运行时/实机验收。
- 参数树过滤未实现项（2026-09-23）：普通参数树隐藏 SDK 标记为 AM_NI/AM_NA 的未实现或当前不可用节点；Schema/内存节点集合保留完整，顶部统计显示 SDK 节点总数、实际显示数、成功读取/可写数和过滤数。文档同步修订。尚未重新构建。
- 相机参数页改为左侧分类导航、右侧属性表单；增加常用属性/触发/高级属性快捷视图，搜索结果跨类别展示。相机管理窗口宽度调整为 1320（可缩放，最小 1120），相机列表与详情按 1:2 分配宽度；参数详情内部按类别导航与表单分配空间。经审批 Debug UI 构建通过（0 错误，742 警告），XAML XML 解析与 git diff --check 通过；未构建 Release，未启动程序做视觉验收。
- 当前任务：通讯管理器协议驱动完整接入（TCP Client/Server、UDP 已实现；继续接入 Serial、Modbus TCP/RTU、EtherNet/IP CIP TCP、Siemens S7 TCP、OPC UA Client，并完成协议专属操作 UI、测试及阶段验收）。
- 本任务修改权限：`src/Workflow/`、`src/UI/Features/CommunicationManager/`、通讯相关 `tests/`、`docs/` 及为协议驱动所需的通讯专属项目/依赖配置；第三方依赖必须先核对许可、目标框架、原生部署及离线构建可用性，不得将 UI 配置存在误报为驱动已接入。
- CodeBuddy 并行授权目录（2026-09-18 起，"发布与版本管理"线；与上表**不重叠**，rule-021 §3 的 `installer/**` 禁区在本次授权范围内）：
  `installer/`、`src/UI/Icons/`、`.codebuddy/rules/`、`.codebuddy/skills/`、`version.json`、`CHANGELOG.md`。
  方案见 `docs/01-方案设计/2026/09/18/内测版发布与版本管理最终方案-20260918-112404.md`。
- 已完成：
  - **Release 包已全量重建**（2026-09-16 15:40，`BUILD_EXIT=0`；308 文件 / 608.3MB；`plugins/` 下 18 个工具目录）；
    程序集版本串同步为 `1.0.0+fb49f6b2…`（.NET SDK 默认 InformationalVersion，可反查源码修订）；
    两个 HALCON 部署目标均对 Release 输出执行（25 个 native dll + `halcondotnetxl.dll` 托管程序集）。
  - **重新编译安装包**：`dist\SunEyeVision-Setup-1.0.0-x64.exe`（158MB；
    SHA256 `00766C8F2FE1AE776718F36EB8909AB02990A52B7ED0BAF67788CA9ACFA3CF48`；编译 197.8s）。
    旧包（SHA256 `508FE0…`，15:15 生成）**已作废** —— 其源包早于 `Directory.Build.targets` 的 HALCON 托管部署改动。
  - **Release 程序运行验证**：PID 44184，`Responding=True`，日志 `logs/2026/09/16/SunEyeVision_154537.txt`
    （1489 行，`[ERROR]`/`[WARN]` 均为 0）；解决方案加载成功（`Workflows=5`），16 个工具类型空洞池初始化正常。
  - `dist/` 已加入 `.gitignore`，避免 158MB 产物入库。
  - **安装包图标改用应用图标（已完成）**：`.iss` 新增 `#define AppIcon` → `SetupIconFile`（Setup.exe 图标）
    与 `UninstallDisplayIcon`（卸载项图标）；已于 **2026-09-17 08:13 重新编译**，
    产物 `dist\SunEyeVision-Setup-1.0.0-x64.exe`（158.1MB；
    SHA256 `C347A0B03541D1A66C06372EA17D1A4599CCFBDCA30B5472DD96C121EAAF8F89`；编译 187.8s）
    ⇒ **取代**上面的 `00766C8F…`。
    **图标已验证**：安装包图标与应用 exe 图标同源（提取后 PNG-SHA256 均为 `19e6cb67…`），旧包为 Inno 默认图标（`db090be5…`）。
    **更正**：先前"脚本无 UTF-8 BOM 会导致向导中文乱码"的判断**有误** —— 实测 ISCC 6.7.3 对有/无 BOM 的
    UTF-8 脚本均正确解析（产物中文名码点一致），BOM 已保留但非必需条件。
- 已知偏差（未处理，涉及发布规则禁区）：产品版本 `1.0.0` 硬编码于 6 处
  （`installer/SunEyeVision.iss:5,8`、`Directory.Build.props:45,47`、`Directory.Build.targets:41,168`、
  `run_rule_checker.bat:12,23`、`src/UI/Shell/Windows/AboutWindow.xaml:37`），无单一真源，
  且界面"关于"显示写死 `v1.0.0.0` 与程序集版本脱钩。
- 下一步：**人工安装验证**（管理员权限 → 安装到 `{autopf}\SunEyeVision` → 程序菜单与可选桌面快捷方式
  → 安装后启动、加载工具、跑通执行流程）。
- 载荷说明：安装包内容为 **2026-09-17 08:10 重建** 的 Release 包（程序集版本串 `1.0.0+ba2b3ab9…`），
  **含**工作流运行引擎改造（`ba2b3ab9`）；载荷写入 08:10:18 早于打包 08:13:18，且 Release 目录内
  **无**比安装包更新的文件（308 文件 / 608.4MB），时间线自洽。
  ⚠ 该次重建在提交之上还带入了 **3 个当时未提交的 src 改动**
  （`src/UI/Services/Workflow/WorkflowExecutionManager.cs`、`src/UI/Shell/Windows/MainWindowViewModel.cs`、
  `src/Workflow/WorkflowExecutionEngine.cs`，08:06–08:09 修改）⇒ 载荷 ≈ **当前工作区代码状态**，而非纯 `ba2b3ab9`。

## 工作流运行引擎架构优化（实施中）

- 审计与实施设计：`docs/05-框架架构/2026/09/16/工作流执行架构审计-20260916.md`、
  `docs/01-方案设计/2026/09/16/工作流运行引擎全量重构设计-20260916.md`。
- 当前状态：M1 已实施；M2 流程内依赖图与解决方案级全局变量边已实施；M3 已使用全局就绪队列、执行画像与资源租约；M4 已接入状态栏“执行快照”与逐节点实时刷新；M5 已有生产者/消费者模型、跨流程依赖图、RunAll 批次变量快照；生产者唯一在模块结果绑定，变量管理器只读显示输入来源，目标参数管理器已主题化并改为二态复选框交互；模块结果通过 `IsBindable`/完整结果路径统一支持普通结果、ROI 图形和图形字段绑定。B2 目标树全集逻辑、B5 第一批工作流作用域消费者键、D1-D3 SDK 参数契约和校验器已实施；D4-D5 工具契约迁移、子程序实例路径和后续模型迁移待实施。
- D1-D3 验证：`ParameterContractValidationTests` 3/3 通过；`dotnet build src/Plugin.SDK/SunEyeVision.Plugin.SDK.csproj -c Release --no-restore` 与 `dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore` 均 0 error。架构方向已修正为：契约和算法参数只使用纯 CLR 值；当前 `ParamValue<T>` 解包仅用于迁移施工，后续必须删除，不保留旧工具兼容层。
- D4-D5 第一批：Blob 分析参数实现 `IInputSlotContract`/`IRuntimeParameterContract`，登记检测区、屏蔽区和主要运行参数；`BlobAnalysisParameterContractTests` 1/1 通过，工具 Release 构建 0 error。其他工具仍按批次迁移，暂不启用全局严格完整性校验。
- D4-D5 第二批：图像二值化参数实现同一组契约，登记检测区、屏蔽区、固定阈值、自适应阈值和 InRange 参数；工具 Release 构建 0 error。
- D4-D5 第三批：图像滤波和形态学参数实现同一组契约，登记检测区、屏蔽区及核心滤波/形态学参数；两个工具 Release 构建均 0 error。
- 参数模型拆分第一步已实施：新增 `ParameterBindingDefinition`、`ParameterBindingSet`、`RuntimeParameterSnapshot` 和 `RuntimeParameterSnapshotResolver`；`ToolExecutionRequest` 支持绑定定义与运行快照，`ToolExecutor.ResolveRuntimeParameters` 生成快照且不修改设计参数。SDK 快照测试 3/3 通过。Blob 分析、图像二值化、图像滤波、形态学处理四类工具已完成纯值化，下一步移除解析器对 `ParamValue<T>` 的迁移依赖。
- 绑定生产端第一步已实施：全局变量管理器现在同时把普通 CLR 参数的绑定写入节点 `ParamSettings`；清除绑定时同步移除外部定义，旧 `ParamValue<T>` 内部配置仍保留作过渡。UI Release 构建 0 error。下一步把 `AlgorithmNode` 的执行请求接入节点 `ParamSettings` 快照，并让运行参数从快照注入运行副本。
- 执行接入已实施：`AlgorithmNode` 从节点 `ParamSettings` 构造 `ParameterBindingSet`，创建参数运行副本；`ToolExecutor` 解析目标属性类型、生成快照并将值注入运行副本，设计期参数不被覆盖。SDK 测试 443/443 通过，UI Release 构建 0 error。下一步验证全局变量到纯 CLR 参数的端到端执行，再清理旧解析路径。
- 端到端验收方案已记录：`docs/01-方案设计/2026/09/18/全局变量运行快照端到端验证方案-20260918.md`，CodeBuddy 可按 E2E-01 至 E2E-08 执行并回填证据。
- 调试参数持久化桥接已补齐：`ToolDebugControlBase` 统一监听 `ConfigSetting.BindingConfigChanged`，将纯 CLR 参数绑定写入当前节点 `ParamSettings`，并在调试控件加载/切换节点时恢复绑定状态；不再只依赖 `ParamValue<T>` 内部配置。UI 验证构建 0 error。
- 最新实现：主界面“全部流程运行”已接入 `WorkflowExecutionManager.RunSolutionAsync`，不再使用标签页中的模拟延时；生产节点结果只更新当前批次快照，不回写设计期全局变量值。
- 已执行验证：`dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore` 成功（0 error，已有 742 warnings）；`SolutionExecutionDependencyGraph` 测试 2/2 通过。
- 当前基线提交：`ba2b3ab9 feat(workflow): 运行请求、依赖图、资源租约与工具执行画像`。

## 通讯能力设计任务（2026-09-21）

- 2026-09-23 用户授权开始按最终方案接入真实协议实现。实施从真实 TCP Client 运行闭环开始：本机异步运行时、连接测试、连接/断开、UTF-8 发送、接收数据回显到调试区；TCP Server/UDP/Serial/Modbus/CIP/S7/OPC UA 尚未接入，未支持协议必须返回明确错误而非伪成功。工业协议 SDK/许可评估与验证按阶段进行。
- 当前进行中实现：`src/Workflow/CommunicationRuntimeManager.cs` 提供 TCP 客户端会话生命周期；通讯管理 UI 的按钮和收发框已绑定该运行时，异常断线会更新状态，删除/关闭会释放连接。验证：用户授权后执行 `dotnet build src/UI/SunEyeVision.UI.csproj -c Debug --no-restore -p:OutputPath=../../dev/Debug/`，成功（0 error，831 warnings）；未启动程序，待用户手工验收。
- 用户手工验收反馈：TCP 客户端配合调试助手，发送与接收交互正常（2026-09-23）。后续实施转入 TCP Server 与 UDP。
- TCP Server/UDP 代码增量已接入同一运行时：Server 执行真实本机 bind/listen，接收多个连接并向最近活跃客户端发送；UDP 真实 bind/receive 并向配置目标端点发送。用户再次授权构建后，执行相同 Debug 构建成功（0 error，831 warnings）；未启动程序，待用户手工验收。
- UI 交互修订：TCP Server 的单一状态切换按钮显示“开始监听/停止监听”；TCP Client 显示“连接/断开”；UDP 显示“绑定端口/解除绑定”。已移除重复的连接、断开双按钮，测试连接仍单独保留。此交互修订尚未重新构建。
- 2026-09-23 用户进一步确认设备列表交互：改为垂直选项卡式设备项，标题序号/名称，副标题显示协议类型与状态，圆形状态灯呈现连接状态；每个设备项使用 SDK ToggleSwitch 执行对应协议启停，未接入驱动的设备禁用该开关。右侧保留参数、调试区和测试连接，不再放独立启停按钮。本轮 XAML/VM 改动尚未构建。
- 已按该反馈替换 DataGrid 为主题化 ListBox 选项卡项；副标题绑定协议+状态，圆形指示灯区分连接中/已连接/故障/断开，SDK ToggleSwitch 按设备直接启停，未接入的协议开关禁用。当前二进制尚未包含此改动，等待用户要求后再构建。
- 2026-09-23 右键菜单样式修复：通讯设备菜单不再使用局部不完整的 ContextMenu/MenuItem 样式，改复用应用全局主题菜单模板，并设置合理最小宽度；UI Release 构建成功（0 error，1117 warnings）。本轮未启动 UI，待用户实际检查不同主题下菜单背景、文字间距与边缘显示。
- 本轮基线提交：`8a66d177884bdb038b6082b1b36c2041464e6237`。

- 2026-09-23 最终范围修订：基于用户逐协议截图与确认，新增完整架构基线 `docs/01-方案设计/2026/09/通讯管理器运行时架构与协议扩展最终设计方案-20260923.md`。已明确 OPC UA 仅客户端；当前纳入 TCP Client/Server、UDP、Serial、Modbus TCP/Serial、EtherNet/IP CIP TCP、Siemens S7 TCP；不纳入 HTTP 与 OPC UA Server。
- 新方案覆盖传输/工业协议/角色术语边界、运行时分层和契约、配置 schema/迁移、连接生命周期、安全凭据与证书、协议扩展流程、各协议参数基线、OPC UA Client 安全/身份/会话参数，以及完整主窗口/创建设备/OPC UA 编辑器 ASCII 线框、按协议差异化调试和分阶段验收。
- 用户截图确认 Modbus 的 TCP Client/串口切换，但尚未提供 Modbus 串口模式的详细参数截图；新方案将字段标为待确认，避免把建议字段误写为 VM 既定行为。此前的“当前仅做 UI、不实施通用通讯协议”阶段性说明不构成本次最终协议范围；本次目标是按新架构逐步实现真实驱动，尚未宣称驱动已接通。

- 已完成现状审计：`src/Workflow/Communication.cs` 和 `src/UI/Features/CommunicationManager/CommunicationManagerViewModel.cs` 仍为弱类型配置与 `Thread.Sleep(500)` 占位测试，尚未接入真实通讯运行时。
- 已形成设计基线：`docs/01-方案设计/2026/09/通讯管理器与通讯工具完整设计方案-20260921.md`。
- 方案覆盖配置 schema、运行时连接管理器、传输层/协议适配器、通讯工具、工作流快照集成、UI、持久化迁移、安全可靠性及 M0–M4 实施验收；UI 章节已补充参考界面收敛方案、设备/控制器分层、六类功能页面、协议创建对话框、测试/删除对话框、交互时序、验收清单及 ASCII 线框图。
- 历史阶段记录（已被 2026-09-23 的真实协议接入授权取代）：曾以 VisionMaster 通讯管理器 UI 为参考，仅做设备管理骨架；现按本节最新实现范围逐步接入通用通讯驱动。
- UI 反馈修订：移除设备管理页的“控制器列表”；协议下拉框只保留 TCP 客户端、TCP 服务端、UDP、串口、HTTP、Modbus TCP、Modbus RTU、OPC UA，厂商专用协议通过后续协议适配器扩展。
- UI 交互修正：保留参考 VM 的左侧功能导航和控制器列表布局；当前只实现设备管理页，控制器列表不实现业务逻辑；内容区顶部工具栏移除，设备新增入口保留在设备列表标题旁。
- 当前已开始设备管理 UI 实施：通信设备创建对话框和选中设备详情均按 TCP 客户端/服务端、UDP、Modbus TCP/RTU、串口、OPC UA、HTTP 显示差异化参数；开关统一使用 SDK `ToggleSwitch`。未选中设备时右侧隐藏所有参数控件并显示空状态提示；设备管理对话框文本和输入控件使用主题资源。接收结束符关闭时隐藏编辑框和快捷按钮，开启后显示可滚动多行编辑框及 `\r`、`\n`、`\r\n` 快捷输入，默认值为空，不再预填 `0D 0A`。Debug 构建已验证 0 error。
- 最新主题与布局修正：SDK ComboBox 当前值和 ComboBoxItem 下拉项显式继承主题 Foreground，并补充禁用项主题色；通信设备 DataGrid 的行、单元格、列文本统一使用主题资源，选中/失焦状态不再使用固定白字或系统默认失焦色；协议类型列显示协议名称而非枚举名；参数绑定属性补齐正确的属性变更通知。Debug 构建通过，0 error，产物位于 `dev/Debug/`，尚未做本轮运行时人工验收。
- 本轮布局修正：按当前阶段要求移除控制器列表及空占位，设备列表区域占满左侧；接收结束符改为“标签/SDK 开关同一行 + 自适应多行文本框 + 等宽三列快捷符号按钮”的结构。Debug 构建通过，0 error，尚未做本轮运行时人工验收。
- 调试数据区补齐：接收数据页增加带主题背景/边框的只读多行 TextBox，发送数据页增加带主题背景/边框的可编辑多行 TextBox 和禁用发送按钮；16 进制显示改用 SDK ToggleSwitch。Debug 构建通过，0 error。
- 接收/发送记录均保留独立历史框并带毫秒时间戳与 `RECV/SEND UTF-8` 标记；发送区使用紧凑主题化圆角组合输入器、空值提示、强调色发送按钮水平并排，单行约 50px、支持多行扩展，Enter 发送、Shift+Enter 换行，成功后清空。已删除“测试连接”按钮及对应命令/运行时方法。最新视觉修订待 Debug 构建及运行时人工验收。
- 设备解耦方案 `docs/01-方案设计/2026/09/通讯管理器按设备解耦与收发调试优化方案-20260923.md`：M1 每设备 Workspace 已实施，接收事件按设备 ID 路由；M3 收发格式已实施，RX/TX 保留原始字节与时间戳，接收文本/HEX 显示可独立切换，发送文本/HEX 输入可独立切换，HEX 发送经校验后走原始字节发送；RX 清空和 TX 历史清空分别绑定当前设备 Workspace。HEX 解析覆盖常见分隔符、0x 前缀、非法字符、奇数半字节和空输入。2026-09-23 UI Debug 构建成功（0 error，829 warnings）；待用户验收多设备切换、HEX 收发和独立清空。M2 设置草稿事务与 M4 界面全状态验收仍待实施。
- 2026-09-23 Tab 工具项布局修正：接收/发送 Tab 名称先并排显示，格式/清空操作区固定在“发送数据”右侧，并按 SelectedIndex 切换接收/发送各自控件；格式状态提示仅显示“文本”或“HEX”，徽标前景/输入背景/边框均使用动态主题资源；两个“清空”分别只作用于各自历史。自定义 TabControl 顶栏模板，内容区不增加额外工具栏行。UI Debug 构建成功（0 error，740 warnings），待运行时检查。
- 2026-09-23 文本接收可见性修正：调试助手发送 HEX `11` 时，该字节解码为 ASCII 控制码 DC1，UTF-8 文本模式正文不可见。现在纯不可显示/无效 UTF-8 数据会显示解释、原始 HEX 和“切换 HEX 查看”提示；可读文本与控制码混合时，保留文本并转义显示控制字符。针对性 UI 测试 4/4 通过；全量 Debug UI 由测试项目依赖构建通过，0 error。
- 2026-09-23 设备列表右键菜单：列表项右键先选中目标，提供主题化“重命名/删除”；重命名使用独立轻量对话框并修改目标设备名称，删除复用现有运行时断开和配置删除流程。
- 2026-09-23 TCP Server 多客户端发送语义确认：用户要求向所有已连接客户端发送。服务端现逐个向连接写入相同负载，并返回实际成功写入数，UI 反馈客户端数量；该应用层 fan-out 不是 UDP 广播包。新增两个 TCP 客户端集成测试，验证两者都收到数据。通讯相关测试 5/5 通过；UI Debug 构建 0 error（987 warnings），产物 `dev/Debug/SunEyeVision.UI.exe`，待用户手工验证右键菜单与调试助手多客户端收发。
- 2026-09-23 完整协议接入 M1：Workflow 已接入串口打开/收发（System.IO.Ports 9.0.0，MIT）及 Modbus TCP/RTU 主站（NModbus 3.0.81，MIT）；Modbus 类型化支持 FC1/2/3/4/5/6/15/16，禁止任意原始字节发送。UI 有串口、Modbus 参数和专属读写操作。Modbus TCP 本地模拟端覆盖 FC3 读、FC6/15/16 写；串口必填校验已测。**未完成验收**：串口/RTU 尚无真实 COM/RS-485 测试。
- 2026-09-23 完整协议接入 M2：引入 S7netplus 0.20.0（MIT）与 OPC Foundation UA Client 1.5.378.176；S7 TCP Client 支持 CPU 型号、rack/slot 及 DB/Marker/Input/Output 区域字节读写；原始字节发送会明确拒绝。CIP TCP 已实现 EtherNet/IP RegisterSession、SendRRData、逻辑路径显式消息及 UnregisterSession；本地 CIP 服务器模拟器验证会话注册、Get_Attribute_Single 请求路径、响应和注销，CIP 当前是通用显式消息入口，**不包含隐式 I/O、Scanner/Adapter 或厂商 Tag 浏览层**。
- 2026-09-23 完整协议接入 M3（按用户当前范围收敛）：OPC UA Client 支持匿名身份、按配置安全模式发现端点、证书校验（默认拒绝未知证书）、Browse/Read/Write/Subscribe/Unsubscribe；`Sign` 与 `SignAndEncrypt` 两种安全模式本地端到端测试通过，包含取消订阅后无后续通知。通讯面板有独立“订阅/取消订阅”，按 NodeId 移除监视项，移除最后一项后释放底层 Subscription；断开设备统一释放剩余项。测试曾揭示并修复写响应列表校验问题。安全模式 `None` 的同版本测试服务端在空客户端证书时自身抛 NullReference；未通过放宽客户端证书验证规避。**用户名/密码及用户证书身份本阶段不实现**：用户确认当前设备未配置凭据，按需再开发；遗留配置中若身份不是“匿名”，运行时明确拒绝，不尝试连接或读取凭据。S7 尚无真实 PLC/仿真器数据读写验收；Modbus RTU 和串口尚无真实设备验收。
- 当前验证记录：2026-09-23 UI Debug 构建通过；合并通讯专项测试 11/11 通过（OPC UA `Sign`/`SignAndEncrypt` 浏览、读、写、订阅、取消订阅；Modbus TCP FC3/6/15/16；CIP 回环模拟端；TCP Server 多客户端；设备工作区）；最新 UI Release 重建通过（0 错误，869 个既有警告）。Release 目录确认包含 `System.IO.Ports.dll`、`NModbus.dll`、`S7.Net.dll` 与 OPC UA Client 栈 DLL。未启动 UI，未验证 Visual Studio 设计时和运行时交互；仍需用户/设备人工验收。
- 2026-09-23 OPC UA 显式证书信任管理已实施：通讯面板可查看可信对端证书主题/指纹，选择 `.der/.cer/.crt/.pem` 后先校验有效期并展示主题、指纹和 UTC 到期时间，用户二次确认后才导入；支持逐张撤销。未知证书仍默认拒绝，导入/撤销只影响新连接，不会静默接受或强制断开现有会话。信任存储复用 OPC UA Directory Certificate Store 的 `UA Applications/certs`；自动测试覆盖 DER 导入、列表、撤销、过期拒绝，并由 OPC UA 本地 E2E 通过同一信任存储导入测试端点证书后完成两种安全模式浏览/读写/订阅/取消订阅。按用户决定，身份收敛为匿名，新增旧用户名身份配置的快速拒绝测试；合并通讯回归 14/14 通过，UI Release 构建 0 错误（869 个既有警告）。尚未做设计器视觉检查或运行时人工 UI 验收。
- 2026-09-23 依赖边界复核：System.IO.Ports 9.0.0 包元数据标记 MIT；NModbus 3.0.81 和 OPC Foundation UA 1.5.378.176 包内均带 MIT license；S7netplus 0.20.0 的 nuspec 未声明 license 字段，但对应上游仓库 License.txt 为 MIT（应在第三方声明/发布清单中显式收录）。当前 Release 输出实查含 System.IO.Ports.dll、NModbus.dll、S7.Net.dll、Opc.Ua.Client/Core/Configuration/Security.Certificates/Types 等运行时依赖。该核查仅覆盖通讯直接依赖和 UI Release 目录，不等于全量传递依赖 SBOM/许可审计。
- 2026-09-24 串口参数 UI 优化：新建设备页和已选设备详情页的普通串口、Modbus RTU 串口号均改为不可编辑 ComboBox，数据来自当前主机 `SerialPort.GetPortNames()`；已保存但暂时未枚举到的端口会保留在列表首项，避免旧配置无法显示。UI Release 构建 0 错误（758 个既有警告），通讯专项回归 14/14 通过；尚未启动程序做实际 COM 设备人工验收。
- 下一阶段仍在进行：OPC UA 当前只验收匿名身份，不再投入用户名/密码凭据实现，待用户明确需要时另立工作；对串口/RTU、S7、CIP 及 OPC UA 用兼容设备/仿真器验证；完成全量传递依赖许可/SBOM 和安装包部署审计。真实串口硬件、PLC/设备验收仍需要可用设备或兼容仿真器。HTTP 与 OPC UA Server 不在已确认范围。
- 历史首批工作包记录：Codex C0（运行契约冻结）与 CodeBuddy B0（测试/验收矩阵）；必须使用独立 Worktree，
  且 CodeBuddy 不得修改执行核心。

## CodeBuddy 侧协作配置（2026-09-16）

- 已落地规则 **rule-021 双 AI 协作写入边界**
  （`.codebuddy/rules/02-development-process/ai-collaboration-boundaries.mdc`，`alwaysApply: true`），
  并登记进 `.codebuddy/rules/README.md` 与 `.codebuddy/README.md`。
- 规则要点：任务开始必读 `AGENTS.md` + 本文件；同一工作目录单写者；**禁区**（工作流执行链 /
  Plugin.SDK / 共享主题 / 插件加载 / 发布规则）停止并转交 Codex 并出交接单；完成后编译受影响项目；
  交接前更新本文件；提交按主题且中文 message 经 UTF-8 文件 `git commit -F`。
- **代执行说明**：此前由 CodeBuddy 提交的 `src/UI/**`、`src/Plugin.SDK/**`、
  `src/Plugin.Infrastructure/**`、`Directory.Build.targets`、`installer/**` 等改动，
  **内容来源为人工 / Codex 已修改**，CodeBuddy 仅代构建、启动与提交。
  建议后续在本文件按任务写明"**代执行（不改码）**"授权，以启用 rule-021 第 7 条。
- 待人工或 Codex 复核：授权目录口径是否按任务逐条声明。

- 2026-09-18 最新进度：全工具纯 CLR 参数迁移完成。新增/迁移点圆、点线、点点测量参数（点数组、坐标、卡尺与枚举均为直接 CLR 属性），同步改造三类工具执行逻辑；前一批 Blob、阈值、滤波、形态学、形状匹配、线/圆测量也已完成。工具源码扫描不再发现 `ParamValue<T>` 参数属性或 `BindingConfig` 依赖。
- 运行链已停止从参数对象内部应用旧绑定：`AlgorithmNode` 与 `ToolExecutor` 仅通过节点 `ParamSettings` 生成 `RuntimeParameterSnapshot` 并注入执行副本；全局变量管理器与目标参数筛选也仅操作纯 CLR 属性及 `ParamSettings`。
- 调试控件基类已统一监听 `ConfigSetting` 与 `ParamBinding`，普通标量和图形订阅均写入/恢复节点 `ParamSettings`，用于关闭调试窗口后持久化绑定。
- 验证：全部实际工具项目 Release 编译通过（FixEncoding 工具因工作区未还原而跳过）；Plugin.SDK/UI 自定义输出 Release 编译均 0 error。现有警告为历史 nullable/XML/隐藏成员警告，未新增错误。
- 下一步：清理 SDK/UI 中仅用于旧 `ParamValue<T>` 的死代码与注释，跑 SDK 测试及全量 Release 构建，再交给用户统一手动验证。

- 2026-09-18 收尾：移除 `ToolExecutionRequest.ApplyBindings` 及 `ToolExecutor` 的旧执行开关，执行 API 统一只使用 `ParameterBindingSet`/`RuntimeParameterSnapshot`；迁移测试改为纯 CLR 参数往返契约。
- Release 验证：Plugin.SDK 测试 443/443；点圆、点线、点点、线圆、线线测量测试全部通过；SDK、UI、全量 `SunEyeVision.slnx` 构建均 0 error。工具目录不再包含 `ParamValue<T>` 参数声明或 `BindingConfig` 执行依赖。
- 当前状态：代码实现和自动化验证完成，等待用户统一进行端到端手动验证（绑定持久化、全局变量传递、工具调试执行、工作流执行、结果显示）。
- **结果渲染统一 WPF 方案基线（2026-09-21）**：已记录于
  `docs/01-方案设计/2026/09/21/统一WPF结果渲染与调试预览分层优化方案-20260921.md`。
  方案要求上层按 `Pass/Fail/Neutral/Highlight` 结果状态组织显示，几何类型仅作为渲染器内部数据；
  工作流结果与调试预览分层，关闭调试窗口只清理调试预览/交互层，不误清理工作流结果；
  统一走 WPF 批量 Geometry 与少量 Visual，不按小结果/大结果切换多套公共渲染模式。
  当前由 CodeBuddy 按该方案实施；完成后由 Codex 审核代码、生命周期语义、性能回归、构建和测试。
- **渲染实施设计决策（2026-09-21）**：
  1. 本次明确授权 CodeBuddy 修改 `src/Plugin.SDK/**`、`tools/SunEyeVision.BlobAnalysis/**`、
     `src/UI/Services/Workflow/**`、`src/UI/Shell/Windows/**`；仅限本次统一 WPF 结果渲染任务，
     不修改 `src/Workflow/**` 执行核心；同一时段 CodeBuddy 为唯一代码写入者，Codex 只读审核。
  2. 调试窗口立即执行结果**不写入生产工作流的 `WorkflowContext`**，避免污染工作流结果、数据源和下游执行；
     如调试数据分析或调试链路需要缓存，使用独立的 DebugExecutionContext/调试结果缓存。
  3. 关闭调试窗口后**自动恢复已缓存的工作流结果**，但不重新执行算法；清除 DebugPreview/交互层后，
     从生产工作流结果缓存重新提交 WorkflowResult。该结论取代“重新选中节点后才恢复”的临时建议。
- **区域渲染最终决策（2026-09-21）**：本轮**不实施** CodeBuddy 提出的甲/乙/丙区域栅格化方案。
  工作流结果区域继续纳入统一 WPF 矢量结果层，按 `Pass/Fail/Neutral` 结果状态组织，沿用统一坐标、缩放、
  图像身份和命中语义。不得因为区域数量引入矢量/栅格自动切换，也不增加逐显示项的矢量/栅格配置开关。
  原始 `HRegion`/轮廓数据继续保留用于算法、结果树和下游绑定。透明栅格贴图仅作为未来有明确性能证据、
  且确认静态区域不需要逐个点选后的可选后端优化；本轮施工不得引入贴图生命周期、图像身份同步和独立区域命中路径。
- 2026-09-18 位置修正回归修复：运行快照解析现在只接收工具参数类型中真实存在的可写属性；调试控件的“位置修正”等展示绑定不会再被当作普通参数做 `Convert.ChangeType`，继续由 `AvailableDataSource/ImageSourceReference` 专用链路处理。Plugin.SDK 针对性 Release 构建 0 error。
- 2026-09-18 绑定重开修复：`ToolDebugControlBase` 在派生控件完成 `Parameters` 注入后通过 Dispatcher 再恢复一次 `ParamSettings` 绑定，并在窗口卸载前做最终持久化；全局变量选择表增加深色/浅色主题下的行、选中行、单元格颜色适配。
- 2026-09-18 绑定持久化根因修复：`ParamSettingContainer.Settings` 原先只有只读 `IEnumerable`，System.Text.Json 能写出设置但不能把设置回填到私有字典；现改为可读写 `List<ParamSetting>`，setter 统一重建字典，并放宽旧字典反序列化的枚举读取，确保调试窗口/方案重开后仍保留绑定。
- 2026-09-18 关闭时序补强：`ToolDebugControlBase` 增加公开 `PersistParameterBindings()`，标准调试窗口在 `Closing`、自定义窗口在 `Closed` 显式提交绑定；恢复阶段若模板控件尚未生成则通过 Dispatcher 重试。UI Release 构建 0 error。
- 2026-09-18 绑定问题诊断埋点：新增 `BindingDiag` 日志，记录绑定选择、当前节点、ParamSettings 设置数、控件绑定状态、关闭前后提交及重开恢复结果；用于区分“未写入、写入后丢失、恢复未命中控件”三类问题。UI Release 构建 0 error。
- 2026-09-18 发布运行修复：PointLine 插件目录残留 `SunEyeVision.HalAdapter 1.0.1`，与宿主 `1.0.0` 不一致，导致图像载入节点加载失败并引发就绪队列无可派发节点。已重建 PointLine 插件，确认其 deps 改为 `SunEyeVision.HalAdapter 1.0.0`，并清理插件目录内旧 HalAdapter 副本。
- 2026-09-18 Debug 绑定恢复时序修复：根据 `logs/2026/09/18/SunEyeVision_165750.txt` 确认绑定已写入 `ParamSettings`，但重开窗口时恢复发生在 `ConfigSetting` 模板生成前，关闭时把临时常量状态反向清除了绑定。`ToolDebugControlBase` 现使用 DispatcherTimer 以 100ms 间隔有限重试（最多 12 次），直到标量/图形绑定控件出现后再恢复；Debug UI 构建 0 error（仅历史警告）。
- 2026-09-18 绑定恢复架构调整：移除定时轮询作为主路径；`ConfigSetting` 在 `Loaded` 后通过 `InstanceLoaded` 通知，`ToolDebugControlBase` 仅对属于当前调试控件的实例注册并触发恢复。Tab 延迟创建由控件就绪事件自然驱动，未生成控件不会覆盖已有 `ParamSettings`。Debug UI 构建 0 error（仅历史警告）。
- 2026-09-18 全局变量显示名修复：恢复绑定时对 `__GlobalVars__` 统一投影为 `全局变量.变量名`；持久化 `ConfigSetting.ToParamSetting()` 时即使数据源树尚未建立也写入语义显示名，避免关闭重开后显示内部节点 ID。当前 Debug DLL 被运行中的 `SunEyeVision.UI` 占用，需关闭程序后重新构建 UI 验证。
- 2026-09-18 工具立即执行全局变量修复：`ToolDebugControlBase.CreateLiveExecutionRequest` 现在携带当前节点 `ParamSettings`，并从 `DataSourceQueryService` 注入方案级全局变量与 `__GlobalVars__` 合成结果；工具调试执行与工作流执行使用同一绑定快照解析链，不再要求先运行工作流。Plugin.SDK Debug 隔离输出构建 0 error；当前 UI 进程占用正式 Debug 输出，需关闭后由 CodeBuddy 重建 UI。
- 2026-09-18 调试全局变量缓存修复：`DataSourceQueryService` 按工作流缓存时，原先只在首次创建时复制全局变量，导致服务先创建后变量才可用时立即执行缺少 `__GlobalVars__`。`GetDataProviderForNode` 现在每次获取节点服务都刷新方案级变量快照。UI 隔离构建因未同步各项目引用输出无法完成，需关闭运行中的 Debug 程序后执行正常 Debug 构建验证。
- 2026-09-18 方案保存同步修复：发现“进程内重开正常、保存方案重启后绑定消失”还可能来自 UI 节点与引擎/方案节点实例不一致。`SaveSolutionData` 现在按稳定节点 ID 将 UI 节点 `ParamSettings` 显式同步到引擎节点，并在最终序列化集合中再次对齐，记录保存前绑定数量。
- 2026-09-19 SSOT 诊断埋点：`MainWindowViewModel` 在打开调试窗口和方案保存前记录 UI/Engine/Solution 节点对象身份、`ParamSettings` 对象身份、绑定数量及 `ReferenceEquals` 结果（日志分类 `SSOT-Diag`），用于区分实际对象分裂与纯序列化/加载问题。
- 2026-09-19 根因确认与修复：日志证明 UI/Engine/Solution 始终是同一节点对象，保存前 `ParamSettings count=2`，保存文件也包含两个绑定；重启加载后在 `WorkflowNode.FromBase` 类型转换阶段变为 `count=0`。原因是转换复制了 `Parameters` 等字段但漏复制 `ParamSettings`。已补充 `ParamSettings = baseNode.ParamSettings`，修复方案重启后绑定丢失。
- 2026-09-19 Blob 阈值语义修复：检查 `BlobAnalysisTool.Run` 确认 Halcon 实际执行的是 `Threshold(processingImage, thresholdMin, thresholdMax)`，原实现却把 `(min+max)/2` 写成“实际使用的阈值”，导致 30–100 显示 65。已移除中点计算，结果改为 `ThresholdMinUsed`/`ThresholdMaxUsed`，分析 UI 显示“阈值范围：30–100”，结果契约同步使用“实际阈值下限/上限”。
- 2026-09-19 Blob Run 逻辑修复：增加阈值下限不得大于上限的参数校验；修复 BinaryInv/形态学处理对 `using` 变量重新赋值造成的 Halcon 区域错误释放风险；`BlobRegions` 改为与最终 `FilteredBlobs` 相同的 Tab3 筛选集合，保留 Tab4 NG Blob 供颜色判断，避免图形区域和数值结果不一致；更正 `GrayImage` 语义为原始灰度输入。Blob 工具 Release 构建 0 error；Blob 参数契约测试 2/2 通过。
- 2026-09-19 区域/卡尺绘制前置校验：ROI 编辑器和测量卡尺编辑器统一由管理器检查主 `ImageControl.SourceImage`；无图像时阻止进入绘制状态、清除工具选中并弹窗提示“请先加载或选择一张图像”，有图像时保持原有交互。Plugin.SDK Release 构建 0 error（历史警告未变）。
- 2026-09-19 卡尺尺度统一：卡尺带宽/长继续使用图像坐标参与算法与几何计算；方向三角形改为固定屏幕尺寸（约 10px 高、5px 半宽），通过 `InvScale(zoom, ...)` 映射到图像坐标，且交互层与结果层复用 `BuildLineDirectionArrowTriangles`/径向箭头生成逻辑。箭头尖端落在卡尺外边缘（`bandWidth / 2`），避免按带宽比例计算造成不同分辨率或缩放下大小漂移。全量 `SunEyeVision.slnx` Release 构建 0 error。
- 2026-09-19 卡尺初始尺寸自适应：新建卡尺完成时，以当前图像短边相对 640px 的比例初始化卡尺宽度/长度（带上下限）；参数和手柄调整后仍由工具参数接管，刷新图像或视图不会覆盖用户值；重新新建卡尺时才重新计算初始尺寸。直线查找和圆查找共用 SDK 管理器入口。
- 2026-09-19 工作流核心架构审视：已形成 `docs/05-框架架构/2026/09/19/工作流与执行引擎架构审视-20260919.md`。确认工作流定义存在 UI、WorkflowEngine、Solution 多份事实源；工作流 2 日志证明节点只进入 UI，未进入引擎/方案。后续必须先统一 WorkflowDefinitionStore、定义服务和运行快照边界，再处理局部执行异常和并行优化。
- 2026-09-19 架构实施第一步：新增 MainWindowViewModel.AttachTabToWorkflowDefinition。新建/切换工作流时将 Tab 节点和连接集合绑定到 WorkflowEngine 同一实例；若 UI 临时集合已有节点/连接则在首次绑定时迁移，并记录共享引用诊断。UI Release 构建 0 error，下一步继续把节点/连接命令收敛到定义服务并补一致性测试。
- 2026-09-19 架构实施第二步：新建方案时不再 `new Workflow` 复制 Tab 节点，而是复用 WorkflowEngine 定义；保存方案时发现同 ID 旧 Solution 实例会替换为 Engine 定义，Engine 未注册则直接报告定义生命周期错误；UI Release 构建 0 error。
- 2026-09-19 架构实施第三步：新增 `WorkflowDefinitionService`，提供工作流定义注册、节点/连接增删和一致性校验入口；连接创建前验证 Tab 与 Engine 共享集合，移除重复的引擎连接同步。UI Release 构建 0 error。下一步接管节点创建/删除命令，并将执行入口改为定义校验后生成运行快照。
- 2026-09-19 架构实施第四步：节点添加/删除入口增加 `WorkflowDefinitionService` 定义查找和 Tab/Engine 共享集合校验；删除不再重复操作引擎集合，命令集合即定义集合。UI Release 构建 0 error。下一步把执行入口接入定义校验与运行快照编译前置。
- 2026-09-19 架构实施第五步：新增 `WorkflowDefinitionValidator`，执行引擎在 `BuildExecutionPlan` 前校验空/重复节点 ID、连接引用、参数动态绑定来源、子程序引用和循环依赖；校验失败不会生成运行快照。UI Release 构建 0 error。下一步补运行快照契约测试，并将执行管理器的 UI/Engine 定义一致性纳入启动前检查。
- 2026-09-19 架构实施第六步：在现有 UI 测试项目加入 `WorkflowDefinitionValidatorTests`，覆盖有效定义、未知连接端点、未知绑定源、缺失子程序四项不变量；筛选测试 4/4 通过。临时独立测试项目因 NuGet 网络不可用已移除，避免污染解决方案。
- 2026-09-19 架构实施第七步：执行管理器在正式进入引擎前增加 UI Tab、WorkflowEngine、Solution 三份定义的一致性校验；根工作流要求三者引用同一实例及节点/连接集合，子程序下钻仅校验根定义，避免把子程序画布投影误判为根定义分裂。校验失败直接生成失败结果并完成快照，不进入执行器。UI Release 构建 0 error（752 个既有警告）；下一步补运行时一致性失败的自动化测试，并继续清理剩余 UI/Engine 双向同步入口。
- 2026-09-19 架构实施第八步：`WorkflowDefinitionService` 增加 `GetOrCreate`，主流程切换、新建流程和子程序创建统一经定义服务注册；方案加载也改为经服务注册，UI 不再直接调用 `WorkflowEngine.CreateWorkflow/RegisterWorkflow`。UI Release 构建 0 error（既有警告）；测试命令受并行构建进程影响未产生新的可读汇总，之前的定义校验 4/4 通过仍有效。
- 2026-09-19 架构实施第九步：定义服务增加 Solution 工作流挂接/解除入口，并替换新建方案、保存、删除、子程序同步等 UI 直接修改 `solution.Workflows` 的路径；同 ID 方案工作流会被统一替换为 Engine 定义实例。UI Release 构建 0 error（999 个既有警告）。
- 2026-09-19 验证策略调整：开发和日常回归默认使用 Debug 构建/测试；Release 构建仅在打包、发布或最终发布验收前执行，避免开发阶段反复生成发布载荷。
- 2026-09-19 Debug 验证：`dotnet test tests/SunEyeVision.UI.Tests/SunEyeVision.UI.Tests.csproj -c Debug --no-build --filter WorkflowDefinitionValidatorTests` 通过 4/4；后续开发验证以 Debug 为准。
- 2026-09-19 版本体系实施：产品、SDK 包、工具文件版本统一使用 `2.0.0.1`；`AssemblyVersion` 迁移到 `2.0.0.0`；新增 `ToolContractVersionAttribute`，工具构建自动写入契约 `2.0`，宿主按显式契约元数据并按主/次版本规则校验，不再从 SDK AssemblyVersion 猜契约。SDK Debug 构建、阈值工具 Debug 构建均 0 error；契约测试 Debug 7/7 通过。Release 尚未执行，按约定留到打包发布前。
- 2026-09-19 大规模区域渲染方案已落盘：`docs/01-方案设计/2026/09/19/大规模区域批量渲染优化方案-20260919.md`。方案保持图像层/结果层/交互层分离，保留完整 HRegion 和 OK/NG 判定，结果层按状态和样式批量渲染，不更换 Halcon 控件。实施第一步已完成：Blob 结果显示暂时限制轻量 WPF 轮廓/矩形/十字/文本为 5000 个，算法结果和 BlobRegions 不截断，并记录显示限制日志；下一步实现按 OK/NG 和图像 Tile 的批量 StreamGeometry 渲染器。
- 2026-09-19 大结果保护补强：`ResultRenderLayer` 对轮廓绘制增加最多 5000 个轮廓的 LOD 采样，对文本布局增加 2000 条上限；Plugin.SDK Debug 构建 0 error。当前仍属于过渡保护，正式批量 HRegion/Tile 渲染器尚未完成。
- 2026-09-19 批量轮廓第一步：SDK 新增 `BatchedContourGeometry` 和 `DisplayCollector.AddContourBatch`，Blob OK/NG 轮廓按批次写入结果层，结果渲染器可将批次内轮廓合并到同一批量 `StreamGeometry`；SDK 与 Blob Debug 构建均 0 error。当前仍保留 5000 个轻量显示预算，后续继续接入完整 HRegion 分批和异步 Tile 缓存。
- 2026-09-19 显示预算 UI 化：Blob 参数新增 `MaxDisplayBlobCount`，默认 5000、范围 100~100000，结果显示页可直接修改并持久化；该值只影响轻量 WPF 图形展开，不影响完整算法结果和区域绑定。Blob Debug 构建 0 error。
- 2026-09-19 OK/NG 区域结果拆分：`BlobAnalysisResults` 新增 `OkBlobRegions`/`NgBlobRegions`；Blob 执行阶段按最终 `JudgmentPassed` 将区域分别聚合，保留完整 `BlobRegions` 作为总集合，供后续批量结果渲染器直接消费。Blob Debug 构建 0 error。
- 2026-09-19 HalAdapter 批量入口统一：`DisplayCollectorHalconExtensions.AddContours(HRegion, ...)` 现在先提取 OK/NG 轮廓列表，再通过 `AddContourBatch` 写入结果层，不再逐区域调用 `AddContour`；HalAdapter Debug 构建 0 error。
- 2026-09-19 批量几何异步缓存接入：`ResultRenderLayer` 对批量轮廓在后台构建冻结的 `StreamGeometry`，绘制时优先直接复用缓存；加入构建版本号避免旧结果异步回写新结果，并覆盖 collector、区域项和兼容项入口。SDK、HalAdapter、Blob 工具 Debug 构建均 0 error（SDK 既有 XML/空引用警告仍存在）。
- 2026-09-19 全量 Debug 自检：`dotnet build SunEyeVision.slnx -c Debug --no-restore` 完成各核心项目和测试项目输出，未发现新增编译错误；Release 仍按版本策略留到发布前。
- 2026-09-19 批次分块：批量轮廓不再生成单个超大 `StreamGeometry`，结果层按每 1000 个轮廓构建多个冻结几何块并逐块绘制；SDK Debug 构建 0 error。该阶段按批次分块，图像空间 Tile 裁剪仍作为后续优化。
- 2026-09-19 视口裁剪：批量几何块绘制前使用冻结几何的 `Bounds` 与当前图像视口相交判断，完全位于视口外的块直接跳过；平移/缩放大图时不再绘制不可见批次。SDK Debug 构建 0 error。
- 2026-09-19 批量构建取消：结果层切换 collector、区域项、兼容项或清空时主动取消旧批量几何构建；后台任务在批次间检查取消状态，避免过期大结果继续占用 CPU；SDK Debug 构建 0 error。
- 2026-09-19 图像空间 Tile 索引：批量几何块按 512×512 图像坐标归入 Tile；有视口时只查询相交 Tile，并以 `HashSet` 去重跨 Tile 几何，避免遍历全部几何块。无视口时仍完整绘制所有块。SDK Debug 构建 0 error。
- 2026-09-19 Blob 大区域执行路径优化：移除每个 Blob 的 `Clone + ConcatObj` 区域聚合；循环中只记录通过筛选的对象索引，最后对 `shapeFilteredRegions` 分别一次性 `SelectObj` 生成总区域、OK 区域、NG 区域，避免数万区域时 O(n²) 句柄复制。BlobAnalysis Debug 构建 0 error。
- 2026-09-19 Blob 特征批量计算：面积、质心、包围盒、矩形度、圆度、周长改为对 `shapeFilteredRegions` 一次调用 Halcon 算子，循环只读取结果元组；仅对显示预算内的 Blob 单独提取轮廓。BlobAnalysis Debug 构建 0 error。
- 2026-09-19 显示数量入口调整：`MaxDisplayBlobCount` 默认改为 3000，名称改为“最大显示区域数”，从“结果显示”页移到 Blob 工具“基本参数”的“运行参数”组；该值继续只控制轻量 WPF 图形展开。BlobAnalysis Debug 构建 0 error。
- 2026-09-19 查找数量语义修正：按要求移除“最大显示数量”概念，新增默认启用的 `IsBlobCountFilterEnabled` 和 `BlobCountLimit=3000`，入口移动到“Blob筛选→查找数量筛选”，无说明文字；执行流程先排序再截取数量，之后 `FilteredBlobs`、BlobRegions、OK/NG 区域、轮廓、矩形、质心和文本全部使用同一批 Blob，避免只筛轮廓造成结果不一致。BlobAnalysis Debug 构建 0 error。
- 2026-09-19 批量渲染取消源清理修复：`ResultRenderLayer` 的后台构建完成回调在释放 `CancellationTokenSource` 前清空字段，`CancelBatchBuild` 改为先摘除引用并捕获重复释放竞态；关闭调试窗口和清理结果不再抛 `ObjectDisposedException`。同时新增“轮廓提取”阶段耗时记录，用于分析 100 Blob 约 300ms 的真实构成。SDK、Blob Debug 构建均 0 error。
- 2026-09-19 Blob 显示与筛选一致性：质心、轮廓默认 `IsVisible=false`；Blob 外接矩形改为 Halcon `SmallestRectangle2` 的最小外接旋转矩形；面积筛选在 C# 结果链再次显式校验，确保 UI 参数确实参与保留集合。显示项遵循样式可见性。BlobAnalysis Debug 构建 0 error。
- 2026-09-19 Blob 输出开关：新增 `OutputBlobImage`、`OutputBinaryImage`、`OutputContours` 运行参数；二值图和灰度图按开关生成，OverlayImage 不再无条件生成，轮廓提取和轮廓图元也由轮廓输出开关控制。Blob筛选页增加“输出”区域，Blob图像默认开、二值化图像和轮廓默认关。BlobAnalysis Debug 构建 0 error。
- 2026-09-19 交互渲染优化：文本保持可见，不隐藏文本；文本绘制先按当前视口过滤，只排版视口内文本。结果层缩放/视口变化通过 Dispatcher Render 优先级合并连续重绘请求，避免一次拖动触发多次完整重绘；处理 Dispatcher 关闭竞态。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 文本绘制缓存：结果层按文本内容、颜色、字号和粗体状态缓存 `FormattedText`，最多保留 4096 条并自动淘汰，文本仍始终可见；Plugin.SDK Debug 构建 0 error。
- 2026-09-19 变换缓存：确认图像、结果层、交互层共享 `ImageContainer` 的缩放/平移变换；结果层启用 WPF `BitmapCache`，平移时优先复用已绘制轮廓和文本缓存，结果内容变化时由 WPF 自动失效重绘。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 渲染架构基线：保存 `docs/01-方案设计/2026/09/19/大批量结果渲染架构基线-20260919.md`。确认现有单体 `FrameworkElement.OnRender` 全量重绘是轮廓交互卡顿的根本架构风险；后续以 `RenderScene + TileScene + ResultTileHost` 为目标，先完成 WPF DrawingVisual Tile 后端，再评估 Direct2D，不再继续堆叠单体结果层的局部补丁。
- 2026-09-19 M1 渲染契约启动：新增 SDK `RenderScene`、`RenderTileScene`、`RenderTileKey` 和 `IRenderSceneCompiler`，建立算法结果与 WPF/Direct2D 后端之间的场景编译边界；暂不替换旧结果层，Plugin.SDK Debug 构建 0 error。
- 2026-09-19 M2 场景编译器第一版：新增 `RenderSceneCompiler`，按图像坐标边界将图形/文本结果项编译到 512×512 Tile，并提供视口 Tile 查询；当前保留结果项引用，后续再将单个大批次细分为独立 Tile 几何。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 M3 Tile 宿主第一版：`ResultRenderLayer` 改为继承 `ResultTileHost`，结果项异步编译为 `RenderScene`，仅创建和刷新当前视口内的 `DrawingVisual`；平移/缩放不再进入单体 `OnRender` 全量路径，场景编译支持取消和版本隔离。SDK、UI Debug 构建均 0 error；仍需运行时大结果验证及后续 M4 交互空间索引。
- 2026-09-19 M4 空间索引第一步：`RenderScene` 增加按矩形/点位的去重候选查询，`ResultRenderLayer.QueryHitCandidates` 暴露图像坐标命中候选；索引只缩小候选范围，精确几何命中仍由交互层执行。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 M4 精确命中第一版：新增 `RenderHitTester`，支持点、线段、轮廓/批量轮廓、圆、旋转矩形、多边形和圆弧的容差命中；`ResultRenderLayer.HitTest` 先走 Tile 候选索引，再执行几何精确测试。Plugin.SDK Debug 构建 0 error；尚未接入具体宿主的点击选择语义。
- 2026-09-19 M4 宿主接入：`ImageControl` 新增 `ResultHit` 路由事件；现有 `CanvasMouseLeftButtonDown` 未处理时才调用结果命中，ROI/卡尺等已有交互优先，命中事件处理后才阻止平移。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 M4 契约测试：新增 `RenderHitTestingContractTests`，覆盖圆周容差、旋转矩形内部/边缘命中和跨 Tile 候选去重，3/3 通过；测试构建存在既有 x64/MSIL 架构警告，无新增错误。
- 2026-09-19 M5 性能基线测试：新增 `RenderScenePerformanceTests`，覆盖 3000、10000、71905、100000 个点结果的场景编译与 Tile 候选查询，4/4 通过；当前 Debug 测试总耗时约 2.7 秒，后续需在真实轮廓、文本和 UI 平移/缩放场景下补充帧率与内存指标。
- 2026-09-19 M5 Tile 复用优化：`ResultTileHost` 视口变化时复用已存在的 `DrawingVisual`，只为新进入视口的 Tile 首次绘制；样式/缩放/场景变化仍显式强制重绘。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 M5 运行统计：`ResultTileHost` 新增 `ResultTileHostStats`，统计视口刷新、Tile 创建/重绘/复用/卸载，可在运行时验证平移是否命中 Tile 复用路径。Plugin.SDK Debug 构建 0 error。
- 2026-09-19 M5 UI 回归：主 UI Debug 构建通过，0 error；现有 XML/nullable/架构警告未因渲染改动新增错误。
- 2026-09-19 运行卡死修复：用户实测算法已成功但 UI 在渲染阶段未响应、内存约 1.5GB；日志确认卡点位于结果提交之后。根因是场景先于图像布局完成时，`ResultTileHost` 在无视口状态下默认渲染全部 Tile。现改为无视口时清空/不创建 Tile，等待 `ImageControl` 首次提交视口后只渲染可见 Tile；UI Debug 构建 0 error，需用户复测大批量 Blob。
- 2026-09-20 卡死复核与诊断：最新 `logs/2026/09/20/SunEyeVision_000038.txt` 在图像载入成功后没有出现 Blob 工具“开始执行/成功”，因此不能把该次卡死直接归因于渲染；同时确认 `RenderContours` 的批量几何缓存未完成时存在逐轮廓整批回退，会在每个可见 Tile 重复展开大结果。已新增 `RenderDiag` 阶段日志（场景编译、视口刷新、Tile 创建/重绘/复用）并移除该高风险回退：缓存未就绪时跳过绘制，缓存完成后再刷新。UI Debug 构建 0 error，需依据下一次日志判断实际卡点。
- 2026-09-20 日志 `logs/2026/09/20/SunEyeVision_001431.txt` 已完成证据闭环：Blob 算法 00:14:49.107 成功、工作流 00:14:49.112 完成；场景编译约 103 ms，但随后 100 个可见 Tile 连续完整重绘两次（1077.8 ms、882.6 ms），与批量缓存未就绪后再次 `RequestRender()` 的代码路径一致。`ResultRenderLayer` 现将场景提交延后到批量几何缓存完成，并记录缓存构建开始/完成耗时，消除首次未命中缓存的整批重绘。Plugin.SDK Debug 构建 0 error；渲染契约/性能测试 7/7 通过，待用户复测并提供新日志确认重绘次数与总耗时。
- 2026-09-20 日志 `logs/2026/09/20/SunEyeVision_003037.txt` 验证重复重绘已消失：批量缓存构建 1224.9 ms，场景提交后仅一次 `renders=100`，但该次仍耗时 2252.3 ms。代码追溯确认每个 Tile 的 `RenderContours` 原先按整个视口选择缓存几何，导致 100 个 Tile 重复绘制同一批几何。现改为使用当前 Tile 的 `(X,Y)` 只绘制对应缓存几何；SDK Debug 构建 0 error，渲染契约/性能测试 7/7 通过，待 UI Debug 构建及下一次运行日志验证单次渲染耗时。
- 2026-09-20 Tile 设计复核：日志确认按 Tile 渲染仍可能让 WPF 提交 80–100 个 Visual；同时旧批次几何按 1000 条轮廓合并后再按 Bounds 复制到多个 Tile，单个 Tile 仍承载大几何。结果层现改为按单轮廓空间范围分组、每个 Tile 独立构建几何，并增加 `tileTotalMs`/`tileMaxMs` 诊断；大视口超过 24 个可见 Tile 时合并为单 Visual，局部视口继续使用 Tile。SDK/UI Debug 构建 0 error；渲染契约/性能测试 7/7 通过，待单例运行确认大视口和局部缩放耗时。
- 2026-09-21 渲染自分析日志：`ResultTileHost` 增加 trace、刷新 reason、viewport、composite/tiles 模式、绘制总耗时、最慢 Tile、管理开销、托管内存/工作集，并在超过 100ms 时输出 `RenderAnalysis` 自动诊断（`single-tile-heavy`、`tile-count-heavy`、`visual-management-heavy`、`composite-geometry-heavy`、`batch-cache-build-heavy`、`scene-compile-heavy`）。Plugin.SDK/UI Debug 构建 0 error；渲染契约/性能测试 7/7 通过。
- 2026-09-21 单例闭环日志 `logs/2026/09/21/SunEyeVision_081158.txt`：图像路径为 `C:\Users\houzhongjie\Desktop\OK\103821989.png`，图像 5120×5120，Blob 结果 3000 个、总面积 1419758；图像载入约 979 ms，Blob 算法约 600 ms，工作流约 1.7 s。首次全视口结果提交进入 `mode=composite`，`sceneTiles=100`，实际绘制 594.2 ms、刷新总耗时 599.4 ms，自动诊断为 `composite-geometry-heavy`，管理开销仅 5.2 ms，工作集约 942 MB；随后一次大视口变化仍有 158.4 ms 绘制/160.9 ms 总耗时。完成缓存后局部 Tile/composite 缩放平移大多约 0.2–12 ms。证据表明当前首帧瓶颈是合并 Visual 内绘制 322 个 Tile 几何，而不是 Tile Visual 管理；后续优化应先做可独立测量的“首帧几何绘制”实验，不再凭感觉调整 Tile 大小。
- 2026-09-21 方案固化：新增 `docs/01-方案设计/2026/09/21/全视口大视口GPU几何批处理渲染优化方案-20260921.md`。方案明确不以位图替代矢量作为主方向，改为 GPU 连续顶点/索引 Batch；Tile 退回空间索引、命中候选和局部范围管理，不再作为独立 WPF Visual；记录 Direct3D 11 主路径、Direct2D Geometry Realization 过渡路径、图像切换的 ImageIdentity/RenderGeneration 失效规则及 M1–M4 验收指标。
- 2026-09-24 Modbus 地址空间第一阶段实施：新增持久化 `ModbusAddressPoint` 地址块模型并接入 `Communication` 克隆/方案保存；Modbus 手动调试下拉暂时移除线圈读写，仅保留离散输入和寄存器操作，底层线圈运行时接口保留供后续启用。通讯管理器新增地址空间基础视图：地址块显示名称、范围和所属单元格，点击单元格带入手动调试地址，点击末端 `×` 删除整个地址块。UI Debug 构建 0 error（1051 个既有警告）；尚未完成地址块配置弹窗、接收记录窗口和发送测试按地址块取数。

- 2026-09-21 GPU Batch 第一阶段实现：新增 SDK 后端无关的 `RenderBatchScene`、`RenderBatch`、`RenderBatchRange` 与 `RenderBatchCompiler`，按颜色/线宽/闭合状态合并普通轮廓和 `BatchedContourGeometry`，连续保存顶点和范围表；结果层后台编译并缓存冻结 WPF 几何，绘制时跳过已编译轮廓，保留变换轮廓兼容路径。新增 `ImageIdentity` 和渲染代次校验，图像切换会取消旧批次并丢弃过期回写。当前实际后端标记为 `wpf-batch`，尚未宣称为 D3D11 设备绘制。
- 验证：Plugin.SDK Release 构建 0 error；渲染相关测试 22/22 通过；此前 UI Release 构建 0 error。等待重新构建后由用户手动验证大批量 Blob 全视口、缩放/平移、图像切换、OK/NG 可见性、命中高亮及快速刷新。
- 2026-09-21 日志 `logs/2026/09/21/SunEyeVision_102232.txt` 诊断：选中 Blob 节点时同一组 6000 条轮廓连续启动两次批量/场景编译，第一次约 144 ms 后取消，第二次批量几何编译约 476 ms，首帧 WPF 几何绘制约 218 ms。结果层增加同一显示项引用集合去重，避免 `SetDisplayCollector`/`SetRegionItems` 连续调用重复编译；图像身份变化仍会强制清空并重建。Plugin.SDK Debug 构建、渲染测试 22/22、UI Release 构建均通过。
- 2026-09-21 取消竞态修复：日志报告图像切换时 `CancelRenderBatchBuild()` 对已由 Dispatcher 回调释放的 `CancellationTokenSource` 再次 `Cancel()`，抛出 `ObjectDisposedException`。取消操作现为幂等，捕获已释放源并安全忽略；Plugin.SDK Debug 构建 0 error，渲染测试 22/22 通过。
- 2026-09-21 调试预览生命周期收口：`UpdateDebugPreview()` 使用独立 debug collector，`ClearDebugPreview()` 不再调用结果层整体 `Clear()`；关闭调试后 `RestoreWorkflowResult()` 现从生产 `WorkflowContext` 同步恢复结果树、图像源和几何层，避免调试结果残留覆盖生产显示。渲染事务测试 5/5 通过；UI Release 串行构建 0 error（795 个既有警告）。
- 2026-09-24 相机参数树样式修复：为 `CameraParameterPanel` 的 TreeView 覆盖 WPF 默认的选中/失焦选中色，使用主题化深色背景和主文字色，避免节点失焦后出现白色块导致不可读；移除参数树底部“展开/折叠”按钮及对应 ViewModel 命令，保留节点自身的层级展开能力。已通过 `git diff --check`；待批准后仅构建 Debug 到 `dev\\Debug`，未构建 Release。
- 2026-09-24 设备列表主题化：列表行增加正常、悬停、选中和失焦选中状态；设备状态改为主题色圆点与文字；顶部仅保留“＋”添加入口，删除和重命名移到设备项右键菜单，重命名支持回车确认和 Esc 取消；添加相机窗口 Loaded 后自动执行搜索。UI Debug 构建 0 错误、744 个既有警告，输出 `dev\\Debug`，未构建 Release。
- 2026-09-24 设备列表启动异常修复：日志确认 `CameraManagerDialog.xaml` 将带 Click 事件的 ContextMenu 放在 `Window.Resources` 的 `DataGridRow` 样式中，WPF 加载样式资源时发生 `connectionId` 错误并报告“MenuItem 无法转换为 Grid”。已将右键菜单移到名称列的实际 DataTemplate 内，避免资源样式事件连接错误；此前首次复建发现 4 个缺少 `System.Windows.Controls` 的编译错误，已补齐引用。第二次构建因运行中的 `SunEyeVision.UI (43492)` 锁定 `dev\\Debug\\CommonControl.dll` 未完成，待关闭运行程序后重新构建。
- 2026-09-24 设备列表异常修复复建：用户关闭运行中的 UI 后重新构建成功，UI Debug 输出到 `dev\\Debug`，0 个错误、744 个既有警告，未构建 Release。
- 2026-09-24 设备列表失焦选中样式补强：截图确认白色块来自 `DataGridCell` 的系统失焦选中背景；在相机管理器窗口增加主题化系统高亮资源及 `DataGridCell` 的正常、悬停、选中和失焦选中样式。UI Debug 构建 0 错误、744 个既有警告，输出 `dev\\Debug`，未构建 Release。
- 2026-09-24 相机列表按通讯设备列表模式优化：启用列改为独立绑定 `CameraDevice.IsEnabled`，不再误用列表选中状态；设备列显示名称和型号，状态列保留主题色圆点与状态文字，列表改为单选和紧凑行高，继续支持名称区域右键重命名/删除。UI Debug 构建 0 错误、744 个既有警告，输出 `dev\\Debug`，未构建 Release。
- 2026-09-24 相机列表启用控件统一：启用列改用项目现有主题化 `ToggleSwitch`，与通讯设备列表的开关交互保持一致。UI Debug 构建 0 错误、744 个既有警告，输出 `dev\\Debug`，未构建 Release。
- 2026-09-25 方案启动日志复核：`logs/2026/09/25/SunEyeVision_085207.txt` 已证明用户点击方案后确实进入 `SolutionManager.LoadSolutionOnly`，并完成文件读取、JSON 解析及 `Workflows` 属性探测入口；随后未出现 `Repository.Load returned`、`SetCurrentSolution` 或 `MainWindow.Show`，对应进程 PID 48708 已退出。该次故障阶段位于工作流反序列化/属性探测之后、方案返回之前，不能再归因于配置窗口点击未触发。另一次最新启动 `SunEyeVision_085646.txt` 仅记录到插件目录发现 18 个程序集，未出现 RuntimeIdentity；Windows Application 日志在该时间段没有对应的 .NET/Application Error 事件，说明还需增加进程级启动哨兵/崩溃转储或逐插件边界日志，不能仅凭当前 313 字节日志断定具体异常。
- 2026-09-25 生产插件回归测试：新增 `SolutionLoadAfterProductionPluginLoadCompletesOnStaThread`，按生产插件目录加载后直接调用 `SolutionRepository.Load`，Debug 测试 1/1 通过；因此“插件加载 + 仓库加载”在测试进程中可完成，尚未复现真实 UI 进程退出，下一步需补齐 `PluginLoader.MarkAsLoaded`、节点转换器装配及实际配置窗口启动链路，并在进程级边界记录未捕获退出原因。
- 2026-09-25 启动链路完整埋点：补充“配置流程开始/配置窗口构造/ShowDialog 返回/启动命令 01~08/LoadSolutionOnly 前后/SetCurrentSolution 前后及事件回调/相机恢复/主窗口 Show 前后”的阶段、耗时、线程、方案 ID、窗口可见性和异常日志；UI Debug 构建成功，0 个错误、1002 个既有警告，输出 `dev\\Debug`，待用户按同一路径重新启动并点击方案后提供最新日志。
- 2026-09-25 双击加载复核：`logs/2026/09/25/SunEyeVision_090558.txt` 显示双击入口和 `LoadSolutionOnly` 均已进入，但停在 `Repository.Load begin`；进程 PID 58468 当时仍存活且未到 `File.ReadAllText` 日志。已在 `SolutionRepository.Load` 最前增加 `Repository.Load entered`、`File.Exists` 前后、`EnsurePluginsLoaded` 前后埋点，用于区分文件检查、插件状态检查和反序列化前置阶段。UI Debug 重建成功，0 个错误、829 个既有警告，输出 `dev\\Debug`。
- 2026-09-25 工作流连接空数组隔离：最新 `SunEyeVision_091209.txt` 已证明插件状态、文件读取、JSON 解析、节点列表均正常，卡在 `WorkflowJsonConverter` 的 `Connections begin | count=0`，未进入完成日志。对空连接数组增加直接创建空集合的 fast path，并保留连接反序列化前/后埋点；UI Debug 构建成功，0 个错误、829 个既有警告，输出 `dev\\Debug`，待用户复测确认是否继续进入方案加载后续阶段。
- 2026-09-25 真实相机连接诊断测试：新增 `RealMvsConnectionDiagnosticTests.DiscoverAndOpenPersistedHikrobotCamera`，实际发现序列号 `DA5892282` 的 GigE 相机耗时 543ms，DeviceId 匹配 `hikvision-mvs:DA5892282`，MVS 打开耗时约 13ms，关闭约 167ms，测试通过。新增 `RealCameraRestoreDiagnosticTests.RestorePersistedHikrobotCameraThroughUiManager`，真实走 `CameraManagerViewModel.RestoreConnectionsForCurrentSolutionAsync`，耗时约 1011ms，测试通过。结论：相机发现、DeviceId、MVS 工厂和 UI 管理器恢复链路均能完成；当前启动卡点不是相机连接失败，而是 UI 线程同步调用 `.GetAwaiter().GetResult()` 等待异步恢复导致的同步上下文死等。下一步补 STA/WPF 同步等待回归测试并改为异步启动链路。
- 2026-09-25 启动死等修复：`App.OnStartup`、`HandleStartupDecision`、`ShowConfigurationDialog`、相机恢复链路改为异步调用，移除 `RestoreConnectionsForCurrentSolutionAsync(...).GetAwaiter().GetResult()`；方案加载后使用 `await` 完成相机恢复，再显示主窗口。UI Debug 构建成功，0 个错误、740 个既有警告，输出 `dev\\Debug`。
- 2026-09-25 启动连接超时策略：`App` 对相机恢复增加 10 秒 `Task.WhenAny` 超时；连接成功、失败、部分成功或超时均继续显示主界面，超时任务保留后台观察并记录最终结果，避免 UI 再次同步阻塞。真实 MVS 测试串行通过、UI 管理器真实相机恢复测试串行通过（约 934ms）；UI 方案回归测试单独执行通过，但与其它测试并行/批量执行时既有生产插件 STA 测试偶发 15 秒超时，需后续处理测试全局静态状态隔离，不能归因于本次启动改动。
- 2026-09-25 相机与解决方案启动流程最终方案落盘：新增 `docs/01-方案设计/2026/09/25/相机与解决方案启动流程最终优化方案-20260925.md`。方案明确启动页只负责基础初始化；解决方案管理器显示后启动页退出；双击方案后使用管理器内加载遮罩显示读取方案、恢复相机连接和最多 10 秒等待进度；连接成功、失败或超时均进入主界面；相机连接由应用级管理器持有，参数统一在现有预览窗口调试，设备管理器和预览窗口关闭不改变连接状态；同时固化设备唯一标识、持久化、日志链路和验收标准。
- 2026-09-25 启动方案调整并开始实施：最终采用“主界面先显示 + 主界面加载遮罩”模式，不再新增独立方案加载窗口。解决方案设置完成后立即显示主界面，遮罩展示相机恢复阶段、最多 10 秒等待、成功/失败/超时结果，完成后解除遮罩；当前已接入 `MainWindow` 遮罩控件和 `App` 配置方案启动链路，待 Debug 构建验证。
- 2026-09-25 主界面加载遮罩实施完成：`MainWindow` 新增方案加载遮罩及阶段/耗时显示；配置方案流程在 `SetCurrentSolution` 后立即显示主界面并覆盖遮罩，异步恢复相机最多等待 10 秒，成功、失败或超时后更新提示并解除遮罩；不再新增独立加载窗口，现有预览窗口保持不变。UI Debug 构建成功，0 个错误、1002 个既有警告，输出 `dev\\Debug`，待用户启动并双击方案验收。
- 2026-09-25 相机预览参数类型转换修复：确认 `17301505` 是 GenICam/MVS `PixelFormat=Mono8` 枚举值（`0x01080001`），不是布尔值。根因是参数模板为所有参数创建隐藏 ToggleSwitch，WPF 仍提前读取 `BooleanValue`，旧实现无条件 `Convert.ToBoolean` 导致枚举值触发 `FormatException`。现改为 `BooleanValue` 先检查 `Kind == Boolean`，布尔转换仅接受 `true/false` 或 `0/1`；枚举值保留原值并继续走枚举显示/写入路径。UI Debug 构建成功，0 个错误、740 个既有警告。
- 2026-09-25 预览关闭保持连接修复：确认设备列表的已连接状态可能来自应用级运行时会话，而 `CameraDetailViewModel._session` 尚未同步；预览前只检查后者会误判为预览新建连接，关闭时释放原连接。现同时检查 `Runtime.TryGetSession`、详情会话和 `CameraDevice.IsConnected`，只有预览前确实未连接时才由预览关闭释放会话。UI Debug 构建成功，0 个错误、740 个既有警告。
- 2026-09-25 相机接入范围收口：按用户要求，正式相机管理和添加相机界面不再接入/显示模拟相机；`CameraRuntimeComposition` 仅注册海康 MVS 工厂和发现器，添加相机仅保留 GigE/USB3 真实接口。模拟相机底层源码暂不删除，保留给已有独立测试项目使用；UI Debug 构建待本次改动完成后验证。
- 2026-09-25 模拟相机正式入口移除已完成：`CameraRuntimeComposition` 仅保留海康 MVS 发现器/工厂，添加相机界面仅显示 GigE 和 USB3，不再显示模拟接口；模拟相机底层源码保留给已有独立测试项目。两次 UI Debug 构建均被运行中进程锁定输出文件（先是 Plugin.SDK/CvEngine XML，后是 `build_temp` 中对应 DLL），未产生代码编译错误汇总，需关闭占用进程后重建确认。
- 2026-09-25 模拟相机正式入口移除最终构建：修复 `AddCameraViewModel` 中残留的接口选择条件后，UI Debug 构建已成功完成，0 个错误、740 个既有警告；输出目录为 `D:\MyWork\SunEyeVision_V2\dev\Debug`。正式运行时相机管理仅接入真实海康 MVS GigE/USB3，相机模拟源码仍保留给独立测试使用。
- 2026-09-25 相机实现与开发说明落盘：新增 `docs/05-框架架构/2026/09/25/相机实现与开发说明-20260925.md`，整合 SDK 契约、DeviceDriver 核心实现、App 组装、UI/Workflow 调用边界、运行时会话生命周期、方案持久化、厂商适配器扩展、参数类型兼容、项目拆分策略和新增相机验收要求。
- 2026-09-24 串口参数下拉优化：新建设备与设备详情中的普通串口、Modbus RTU 波特率统一改为 ComboBox，提供 300–921600 的标准波特率并保留非标准已保存值；校验位统一改为 ComboBox，支持无、奇、偶、标记、空格五种校验。运行时解析兼容既有中文值及 Odd/Even/Mark/Space 英文值。UI Release 构建 0 error；通信回归测试 14/14 通过；尚未进行真实串口硬件人工验收。
- 2026-09-24 创建通讯设备后参数为空修复：`CommunicationDeviceEditorViewModel` 创建对象时改用 `Communication` 的类型化属性写入协议键、串口、波特率、网络端点、Modbus/S7/OPC UA 等初始参数，避免直接写 `Settings` 字典绕过属性通知导致创建后详情页 ComboBox/TextBox 显示空值。UI Debug 构建通过（0 error，既有警告）；默认 `dev/Debug` 被运行中程序锁定，本轮使用 `BuildProjectReferences=false` 完成编译验证，未执行 Release。
- 2026-09-24 串口/Modbus RTU 下拉值继续为空的根因修复：详情页刷新串口和波特率选项时，ComboBox 双向绑定在 `ItemsSource.Clear()` 瞬间把 `SelectedCommunication.SerialPort`/`BaudRate` 回写为空值或 0；现刷新前保存值、刷新后恢复，避免仅串口协议受影响。UI Debug 构建通过（0 error，既有警告）。
- 2026-09-24 VM Modbus 界面基线补充：根据用户提供的 VM 成品界面，明确正式目标为“寄存器地址（首地址、寄存器个数）+统一 0–9 地址空间表格+0/10/20…地址行+最大化/隐藏空白行/清空+数据同步/发送测试”；已更新 `docs/01-方案设计/2026/09/通讯管理器Modbus地址空间与调试最终优化方案-20260924.md`，当前代码中的地址块卡片仅作为过渡，不作为最终交付布局。
- 2026-09-24 Modbus UI 第一阶段适配 VM：移除地址块卡片式主界面，改为“首地址/寄存器个数 + 0–9 偏移列 + 0/10/20…地址行”的统一地址空间表格；已配置地址块以颜色和名称显示归属，点击空白单元格创建地址块，点击地址块末端 `×` 删除整块；数据同步/发送测试入口和手动操作已分离，手动操作收进调试折叠区。Debug UI 独立编译 0 error（768 个既有警告）；当前运行程序锁定正式 Debug 输出，因此未覆盖正在运行的文件。
- 2026-09-24 Modbus 地址配置弹窗第一步完成：点击地址单元格打开主题化地址配置窗口，支持名称、起始地址、数量、数据类型、字节序、读写属性、轮询开关和轮询周期；确认后写入并持久化地址块，校验地址范围与块重叠；地址空间显示归属条和默认寄存器值，末端 `×` 保留独立删除入口。UI Debug 构建 0 error（767 个既有警告）。
- 2026-09-24 地址空间布局修正：归属色条与寄存器单元格拆为独立上下两行，单元格只显示寄存器值，保持 VM 参考界面的行结构；使用单进程、关闭共享编译器的 Debug 构建复验通过，0 error，545 个既有警告。
- 2026-09-24 Modbus 地址空间单元格悬停优化：单元格改为显式主题化 Button 模板，默认/悬停/按下/已配置状态分别使用主题资源；空单元格悬停显示主题强调色“＋”，末端已配置单元格悬停显示“×”，移除硬编码黄色和系统默认按钮悬停表现。UI Release 构建 0 error；桌面运行时检查因当前 CUA 会话未发现可绑定窗口而未完成。
- 2026-09-24 Modbus 地址空间交互修正：归属色条改为独立可点击区域，点击已配置归属条编辑当前地址配置；寄存器值单元格点击打开该地址的接收数据记录窗口；删除入口从值单元格移到归属色条覆盖范围的最后一格，并通过显式 Tag 修正删除命中。UI Debug 构建通过，0 error，545 个既有警告。
- 2026-09-24 Modbus 地址块轮询与记录：连接成功后按启用地址块的轮询周期读取保持寄存器；成功读取后地址块归属条切换主题成功色，单元格显示字节值并追加地址级接收记录；断开连接或轮询失败不保持成功状态；同一归属范围内任意值单元格打开该地址块接收记录。UI Debug 构建通过，0 error，795 个既有警告。
- 2026-09-24 Modbus 轮询参数层级修正：按 VM 设计将“轮询使能”保留在独立地址块，将“轮询间隔”统一改为设备级通信参数；地址配置弹窗移除重复的地址级轮询间隔，地址级轮询读取统一使用设备 PollInterval。UI Debug 构建通过，0 error，547 个既有警告。
- 2026-09-24 Modbus 轮询反馈修正：地址配置窗口改用 SDK `ToggleSwitch`；地址块新增持久化 `TimeoutMs`（默认 200ms，范围 50–120000ms），轮询周期仍取设备级 `PollInterval`。轮询请求按地址块建立独立取消超时，连接后立即读取，超时/失败写入明确日志；修复旧地址级 `PollInterval` 残留。Workflow Debug 构建 0 error，UI Debug 构建 0 error（既有警告）；待用户用真实 Modbus 设备复验数据刷新。
- 2026-09-25 Modbus 轮询诊断埋点：在设备开关、连接完成、轮询任务启动、启用地址块快照、请求参数、返回原始寄存器值、地址空间回写成功/拒绝、超时和异常分支增加 `[Modbus诊断]` 日志。Workflow Debug 构建 0 error；UI 因正在运行程序锁定 `dev/Debug/SunEyeVision.UI.xml` 未能覆盖编译，源码验证待关闭运行程序后复验。
- 2026-09-25 日志双规制修正与运行时埋点：确认 `ViewModelBase` 的 `LogInfo/Warning/Error/Success` 默认目标被设为 `LogTarget.UI`，导致文件日志看不到 UI 层诊断；Modbus UI 埋点改为显式 `LogTarget.File | LogTarget.UI`。`CommunicationRuntimeManager` 新增 Modbus 连接配置、会话传输参数、FC03 操作序号、站号/地址/数量、耗时、返回原始寄存器值及异常类型日志，均写文件和 UI。Workflow Debug 构建 0 error；UI 需在关闭运行进程后重新构建并启动验证。

- 2026-09-26 Modbus 地址空间交互与布局修正：轮训成功回写不再 `Clear()` 并重建 `AddressSpaceRows`，改为对现有地址单元格发送值变更通知，避免 10ms 轮训期间不断销毁/创建按钮导致归属色条和历史记录点击失效；手动读写区默认折叠并改名为“高级手动读写（调试）”，地址空间表下方操作区保持可见。UI Debug 构建成功，0 error（既有警告）；待用户人工验证轮训期间点击色条、历史记录及数据同步/发送测试可见性。
- 2026-09-26 用户要求直接删除手动读写折叠区，并让地址空间表使用单独纵向滚动条。当前工作区 XAML 已移除手动操作控件；本轮将地址空间所在 Grid 行改为剩余高度，表格内部 `ScrollViewer` 负责滚动，底部统计/操作栏固定可见。UI Release 构建成功（0 error，771 个警告）；Visual Studio 设计时和运行时布局仍待人工验收。
- 2026-09-26 按用户要求仅执行 UI Debug 构建：`dotnet build src/UI/SunEyeVision.UI.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -v:q` 成功，0 个错误，769 个既有警告。
- 2026-09-26 已将 VM 地址空间最终优化方案保存为 `docs/01-方案设计/2026/09/通讯管理器Modbus地址空间最终优化方案-20260926.md`，内容包括连续地址归属色条、未分配单元格悬停 `＋`、单滚动条、Int 位数 16/32bit、交互逻辑和验收标准；尚未实施本方案代码改动。
- 2026-09-26 用户补充地址空间布局要求：左侧地址序号单元格必须纵向覆盖同一地址行的寄存器归属色条和寄存器值两行，方案文档已补充 `Grid.RowSpan="2"` 和对应 ASCII 图示；尚未实施代码改动。
- 2026-09-26 轮询稳定性优化已实施：同一轮 Modbus 地址块读取结果先在后台汇总，再通过一次 UI Dispatcher 批量回写；地址表不重建，仍只更新现有单元格通知；高频原始寄存器明细改为仅写文件日志，UI 保留批量回写汇总和异常日志。UI Debug 独立构建成功，0 个错误，755 个既有警告；完整依赖构建因运行中程序锁定 `dev/Debug/CommonControl.dll` 失败，未强制关闭运行程序。
- 2026-09-26 Modbus 地址空间最终方案第一批完整实施：地址块归属条和寄存器格改为轻量 `Border`，去除 Button 视觉；空白寄存器悬停显示 `＋`，地址序号继续纵向覆盖色条与寄存器两行；地址配置由 `DataType` 改为 VM 对齐的 `IntBits=16bit/32bit`，并增加旧 `DataType` 配置到位数的兼容读取。Workflow Debug 构建 0 error（117 个既有警告），UI Debug 构建 0 error（755 个既有警告）。待用户统一进行运行时视觉、点击和轮询验收。
- 2026-09-26 修复轮询期间接收记录窗口的 `ItemsControl 与项源不一致`：根因为 `ReceiveRecords` 使用普通 `List<string>`，轮询后台回写时 ListBox 无法收到集合变更通知；现改为 `ObservableCollection<string>`，轮询追加记录会正确通知 WPF。Workflow/UI Debug 构建均 0 error；待用户复测轮询期间打开接收记录窗口。
- 2026-09-26 修复未配置地址时地址归属条仍显示黄色：地址条 Border 默认背景改为透明，仅 `IsConfigured=True` 时显示主题警告色，轮询成功仍切换成功色。源码修改完成；UI Debug 编译阶段未发现源码错误，但最终输出被正在运行的 `SunEyeVision.UI.exe` 锁定，完整构建需关闭程序后重跑。
- 2026-09-26 重新执行 UI Debug 构建成功，0 个错误，769 个既有警告；最新未配置地址透明背景修复已进入 `dev/Debug` 输出。
- 2026-09-26 地址空间左侧序号视觉纵向合并修正：将地址序号从无边框 `TextBlock` 改为 `Grid.RowSpan="2"` 的主题表格 `Border`，明确覆盖归属色条行和寄存器值行；UI Debug 构建成功，0 个错误，769 个既有警告。
- 2026-09-27 地址归属色条改为按实际寄存器范围生成的行内连续段：每个地址块在每行只覆盖与该行交集的列数，不再按寄存器格逐个填色，也不固定铺满十列；跨行分段、同一行多个地址块分别显示。按最新示意图，左侧地址序号只在寄存器值行显示，上方色条行留空并保留分界线。UI Debug 构建成功，0 个错误，769 个既有警告；待运行时视觉验收。
- 2026-09-27 首地址/寄存器个数改用 SDK NumericUpDown，取值变更时地址空间按首地址和准确数量刷新；末行不足 10 个寄存器时补不可点击空白格保持列宽，色条仅覆盖有效范围，Modbus 末地址 65535 限界。UI Debug 构建成功，0 错误、769 个既有警告。新增非整十起点、跨行色条和末地址测试，但 UI 测试项目执行受 NuGet NU1301/SSL 认证失败阻断（沙箱外重试相同），尚未运行通过；VS 设计时和运行时布局待人工验收。
- 2026-09-27 测试依赖恢复：确认测试包已在本机全局 NuGet 缓存，默认源映射仅允许 nuget.org，线上 TLS 失败导致原 assets 为空；使用临时离线源配置完成 Debug 还原后删除配置文件。执行 `CommunicationDeviceWorkspaceViewModelTests` 7/7 通过（含非整十起点、跨行色条和末地址三组新测试）；未修改正式 NuGet 源配置，UI 运行时布局仍待人工验收。
- 2026-09-27 Modbus 轮询改为设备连接生命周期控制：移除最大轮询次数 UI、编辑器和模型属性及轮询循环的次数上限；设备连接成功即持续轮询已启用地址块，主动关闭、异常断开、连接失败或通讯管理器关闭时取消轮询。异常断开回调先确认运行时已断开，避免快速重连时旧事件误停新轮询；每轮成功回写摘要改为仅写文件日志，保留批量 UI 回写。旧配置中的 `maxPollCount` 键不再读取或写入，可能仍作为无效历史数据留在旧方案文件。UI Debug 构建 0 错误、869 个警告；`CommunicationDeviceWorkspaceViewModelTests` 7/7 通过。真实设备持续轮询及关闭/重连仍待人工验收。
- 2026-09-27 Modbus“数据同步”和“发送测试”接入真实操作：数据同步对当前设备启用轮询的地址块执行一次 FC03 读取、逐块超时并批量回写地址表/接收记录；发送测试新增主题化窗口，列出已配置地址块、int/float/string 数据类型和内容输入，严格按 16/32bit、字节序、UTF-8 容量及寄存器数量编码，拒绝只读或超出 FC16 上限的写入，单寄存器走 FC06、多寄存器走 FC16。UI Debug 构建 0 错误、758 个警告，编码测试 8/8 通过；真实设备同步/写入及 VS 设计时仍待人工验收。当前地址块模型未定义关联变量，数据同步已更新地址表和记录，但不宣称已更新外部关联变量。
- 2026-09-27 修复数据同步误依赖“轮询使能”：用户实测“没有启用轮询的地址块，无法执行数据同步”；现在手动同步选择全部已配置且可读的地址块，自动轮询才要求 PollEnabled，分别调用共享的结果回写核心。回写前校验地址块仍存在、可读、首地址和数量与请求快照一致，避免编辑期间旧结果写入新地址；测试覆盖关闭轮询仍可同步及地址变化拒绝旧结果。UI Debug 构建 0 错误、764 个警告，相关测试 9/9 通过；真实设备同步仍待人工验收。
- 2026-09-27 修复发送测试地址下拉显示对象类型名：不再依赖主题 ComboBox 的 DisplayMemberPath 展示记录对象，改以“序号 + 地址名称”纯文本填充，SelectedIndex 映射原地址块。标准 UI Debug 输出因运行中 CommonControl.dll 被占用未能覆盖；隔离目录的 UI Debug 编译成功（0 错误、744 个警告），隔离输出已删除，需关闭运行程序后重新构建并启动才能看到修复。
- 2026-09-27 发送测试写入数量修正：地址块 Count 改为容量上限，输入少于容量只编码并写入实际寄存器数，不补零覆盖剩余寄存器；输入超出容量时截取前面完整的 int/float 值或 UTF-8 字符，并在成功结果提示截取。32bit 值不拆分；超过 FC16 单次 123 寄存器上限时分批写入，失败时提示已写入的前缀数量。用户关闭运行程序后，标准 `dev/Debug` UI 构建成功（0 错误、758 个警告），发送编码测试 11/11 通过；真实设备部分写入/分批写入待人工验收。
- 2026-09-27 主线通讯代码同步：基线 `main=3473225c`、`feature/comm=b6b7c8ec`；将通讯分支的设备管理 UI、Workflow 协议运行时、Modbus 地址空间、收发调试、测试和设计文档合入主线，保留相机与启动流程主线实现。仅协作记录存在文本冲突，已保留双方记录。OPC UA 引入的 `System.Text.Json >= 10.0.8` 与脚本工具默认 10.0.3 冲突，已在脚本工具项目单独提升至 10.0.8。离线还原成功；UI Debug 构建 0 错误、873 警告；全量 Release 构建 0 错误、1246 警告；通讯相关 Debug/Release 测试各 30/30 通过。方案加载回归 5/6 通过，其中生产插件 STA 测试仍在 15 秒超时（合并前协作记录已记载该偶发现象），尚未作根因归因。通讯管理器关闭仍按现有实现释放通讯连接；真实通讯设备、VS 设计时和运行时 UI 验收待用户执行。
- 2026-09-27 图像源工具方案保存：新增 `docs/04-工具设计/图像源/2026/09/27/图像源工具UI与执行设计方案-20260927-200728.md`，作为后续沟通基线。确定图像来源 UI 为“本地图像/相机”；本地接入现有图像预览器，工作流只读运行快照；相机从方案设备列表关联，通过应用级运行时采集。文档含双模式 ASCII UI、旧 ImageLoad 兼容、会话复用与取帧协调、SN 语义、功能裁剪、实施顺序及验收标准。当前仅文档变更，未实施或构建。
- 2026-09-27 图像源方案补充现有预览器可见性链路：明确下方本地图片预览器与上方图像结果画布分离；本地源及其可确定的下游显示并绑定同一 `ImageInputSource`，相机源及对应下游、无选中或无可确定来源隐藏；同节点来源切换立即刷新，多上游与子程序避免错误/过期绑定。补充 `ImageSourceConfig` 本地字段合并（防止覆盖相机设置）、执行快照按来源分流及验收用例。仅更新设计文档，未改业务代码、未构建。
- 2026-09-27 图像源方案第一阶段实施：新增本地/相机来源配置和右侧轻量设置入口（含定位现有预览器、打开现有相机设置/预览窗口）；预览器仅为本地来源显示且与上方结果画布分离；本地配置增量合并保留相机字段；运行快照按来源分流，ImageLoad 异步相机取帧复用应用级会话并转换为结果链使用的图像类型，MVS 只放行明确识别的 Mono8/BGR8 格式，按当前相机触发配置取帧。全量 Release 构建成功（0 错误）；定向测试 DeviceDriver 4/4、UI 持久化 3/3、ImageLoad 2/2 通过。未做真实相机和 UI 运行/设计时验收；高级格式转换、SN、最后一张停止仍待后续阶段，不能视作完整交付。
- 2026-09-27 修正工具箱图像源不可见的显示名/构建配置：工具稳定 ID 仍为 `ImageLoad`，显示名由“图像载入”改为“图像源”，类别保持“采集”。确认用户启动 `dev/Debug`，原 Debug 插件为 9/25 旧版本；全量 Debug 构建成功（0 错误，1244 警告），已更新 `dev/Debug/SunEyeVision.UI.exe` 与 `dev/Debug/plugins/SunEyeVision.Tool.ImageLoad/SunEyeVision.Tool.ImageLoad.dll`。Debug 图像源工具测试 3/3 通过，含工具箱元数据断言；待用户重启 Debug exe 在工具箱“采集”分类做运行时验收。
- 2026-09-27 `feature/comm` 合并后本地输出更新：在基线 `313f2457` 上执行 `dotnet build src/UI/SunEyeVision.UI.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -v:q -clp:ErrorsOnly` 成功，0 错误、1159 警告；本工作目录 `dev/Debug/SunEyeVision.UI.exe` 已更新至 18:37:52。未执行 Release。
- 2026-09-27 通讯管理最终架构方案落盘：新增 `docs/01-方案设计/2026/09/通讯管理最终架构与持久化方案-20260927.md`，整合相机架构对齐、SDK/DeviceDriver 文件结构、应用级会话与轮询所有权、协议扩展、配置/运行数据持久化边界、Modbus 地址空间和操作规则、分阶段验收。基线 `313f2457`；本次仅文档，无代码修改或构建；当前目标结构尚未实施。
- 2026-09-27 通讯方案细化：原文单列 SDK `IModbusService` 易误导，现明确首批 SDK 不预建 Modbus/其他协议专用接口，按 Workflow/插件实际消费者需求统一提升；补 SDK 准入规则、DeviceDriver 内部契约、逐文件迁移、配置 JSON 示意、保存/迁移事务、会话状态机、并发与过时结果规则、新协议最小变更集及回归矩阵。仍仅修改文档，未实施代码或构建。
- 2026-09-27 通讯目录方案按用户要求收敛：第一阶段通讯引用、状态、内部契约、运行时和各协议驱动统一放在 `src/DeviceDriver/Communications/`，与相机的 `Cameras/` 并列；不预建 `Plugin.SDK/Devices/Communication/`，仅当 Workflow/第三方插件有明确跨层消费需求时再提升最小公共契约。已更新本地最终方案，未修改代码或构建。
- 2026-09-27 修正通讯目录方案误解：用户要求通讯驱动集中在 `src/DeviceDriver/Communications/`，并未要求删除 SDK 层。最终文档已恢复 `src/Plugin.SDK/Devices/Communication/` 的通用设备引用、状态、错误、能力、事件及只读状态契约；DeviceDriver 仅放内部工厂/适配器/会话/协议实现；Modbus 等协议专用 SDK 接口仍按真实跨层需求再增加。前条记录保留为修订历史，本次仅文档，无代码或构建。
## 2026-09-29 通讯管理器打开异常修复

- 用户日志显示两类独立问题：Modbus 目标端口被拒绝属于运行时设备连接失败；通讯管理器打开失败的 `connectionId` 异常来自 WPF 加载接收事件右键菜单时的样式事件连接。
- 将接收事件的 ContextMenu 从 ListBoxItem 样式 Setter 移到实际 DataTemplate 内的 Grid，右键选中逻辑改为从原始事件源向上查找 ListBoxItem；避免 WPF 样式加载阶段触发 connectionId 异常，同时保留重命名/删除功能。
- UI Debug 构建通过：748 个既有警告，0 个错误。
