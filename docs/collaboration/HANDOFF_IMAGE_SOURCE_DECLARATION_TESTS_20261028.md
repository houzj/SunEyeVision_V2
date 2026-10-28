# 图像源声明与格式行为补迁测试交接（2026-10-28）

- 独立worktree `workflow-timing-footer/SunEyeVision_V2`，分支`codex/image-source-session`；只新增`tests/SunEyeVision.ImageSource.Runtime.Tests/ImageSourceDeclarationAndFormatTests.cs`和本单，不改产品、共享加载器、项目或其他测试。root已独立完成精确宿主根组件加载入口与相机宿主整链测试。
- 本批为 **10个未运行case**（2 Fact + 3格式颜色 + 3格式灰度 + 2本地元数据）；不是旧工具兼容测试，不保留ToolAttribute/GetOutputVariables/旧PixelFormat字符串。

## 保迁与扩展的行为

1. 运行角色程序集真正的ToolComponentAttribute声明：ComponentId/ToolId/Role/契约版本/EntryType/无Companion；Catalog显示名图像源、采集、image语义图标及顺序。保持真实业务稳定ID ImageLoad，不保旧工具实现。
2. 全11输出端口按新TypeRef资源/Int64/Text及必选非null语义核验；ResultSchema与执行Schema对齐，不存在图像输入回退。
3. 真实Bgr24输入分别执行original/mono8/rgb24，使用带行padding的非对称颜色fixture，校验通道顺序、实际灰度值、原输入未处置/未改、metadata和序列号不变。
4. 真实Gray8输入分别执行三格式，核RGB三通道复制及Gray别名/独立帧行为。不复制产品转换实现或HALCON算子调用，预期来自固定像素fixture。
5. 上述每个格式使用非满幅Domain：两条run `(0,0,1)`与`(1,1,1)`；核输出真实Domain保留、物理帧绑定正确并携本轮Run及真实SourcePort，不把矩形bbox当Domain。不对Domain之外由原算子未承诺的灰度buffer内容作无关断言。
6. 本地图像分别在没有相机metadata以及故意携带旧相机metadata时，DeviceId/FrameId/Timestamp都必须空；正式file/seq、像素、输出类型和Run身份正确。测试数据只放HostDebugRoot/test-data/image-source/<唯一目录>，finally清理，不用另一份程序产物或仓库。

## 发现与生产修正依赖

只读检查时原候选`ImageSourceSession`的FrameId/Timestamp无条件读入参，local可能泄漏旧相机metadata。已明确发root：应按SourceKind隔离这两个字段；本批保留强断言，不通过放宽测试隐藏差异。root产品写者负责修正。

## 已核与未验

- 本批文件仅做Roslyn ParseText语法核查、diff --check；未构建、未加载新Runtime、未运行HALCON、未占用Release。
- 有色fixture的标准灰度固定期望47/208、中性80，严格断言。统一正式产物测试若失败，应检查真实BGR/HALCON行stride/Domain链和合法算法差异证据，不先修改期望或容忍度求通过。
- 新组件完整测试项目/ModuleInitializer/精确宿主根EnsureComponentLoaded均由root整合，本文件不另加加载器或自有初始化。
- 真实设备、主界面、主题、VS设计器和人工验收不在这些自动case范围，不据此宣称已完成。
