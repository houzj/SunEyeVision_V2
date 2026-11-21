# PixelCount 单项目迁移与 Release 验证记录

日期：2026-11-20

## 已落地

- 单项目入口：tools/SunEyeVision.PixelCount/SunEyeVision.Tool.PixelCount.csproj
- 工具内部保留 Runtime、Wpf 命名空间，物理项目只有一个 csproj。
- 新增 Runtime/PixelCountParameterDefinition.cs，通过公共 DeclaredParameterSchema 加载参数 XML。
- PixelCountFields 改为声明门面，不再手工构造参数页字段。
- 输入、ROI、掩膜由 PixelCountParameters.xml 的 Inputs 声明生成基本参数页输入行。
- 新增并嵌入 PixelCountParameters.xml、PixelCountAlgorithmTab.xml、PixelCountTool.xml、PixelCountDisplay.xml、ToolItemInfo.xml。
- 保留 PixelCountRangePanel 作为原厂 PixelModuRangeConfigList 有证据的专属控件。
- product/ToolComponents.props 和 SunEyeVision.slnx 已指向单项目入口。

## 验证结果

- PixelCount 单项目 Release 编译：0 错误。
- PixelCount 参数专项测试：36/36 通过。
- ControlNamingProbe：PixelCount 三页签和范围列表面板通过。
- 产品 UI Release 构建：0 错误，既有警告 5888 条。
- 正式 Release 封包：通过。
- 正式清单：release-payload.json schema 2，PixelCount Runtime/Editor 两个注册均来自 SunEyeVision.Tool.PixelCount.Runtime.dll。
- 封包 SHA-256：371A72421C8ED8EB1E06241C4D4B3B84C10AD60F519C5DA73751179900D8A0F8。

## 本轮修复的封包问题

正式输出中曾残留迁移前的 SunEyeVision.Tool.PixelCount.Wpf.dll，造成同一工具组件重复。该旧产物已从正式输出根清理。完整解决方案还会编译测试工程并产生未登记的旧工具产物，因此本轮产品验证使用 src/UI/SunEyeVision.UI.csproj 作为 Release 入口，并在正式根执行封包校验。

## 未确认项

- 原厂固定输入输出黄金样本尚未登记，HALCON 与原厂算法等价状态保持 unconfirmed。
- 原厂动态范围行事件时序、位置修正显示对象和 ROIAnnulus 的本方归一化细节仍需独立样本或运行证据。
- 本记录证明工程迁移、XML/页签/端口声明和产品注册链路通过，不等同于 PixelCount 算法闭口。

## 目录迁移补充

已将源码物理迁移到 PixelCountTool.cs、Algorithms、Parameters、Results、Editor、Resources 目标目录，并删除 Runtime/Wpf 旧 csproj 与各自 packages.lock.json。根项目继续编译所有源码，命名空间和公共注册合同保持不变。迁移后单项目 Release 编译 0 错误。

最终 UI Release 重建后封包 SHA-256：C18679717EF7E89AD7EF826FA1252F0D1424287A49591A5E1FEFC6765A12143E。产品 UI Release 构建 0 错误，5888 条既有警告。


## Blob 结构回归

已将 Blob 物理源码迁移到 BlobTool.cs、Algorithms、Parameters、Results、Editor、Resources 目标分区。Blob 单项目 Release 编译 0 错误。运行行为和公共参数链未改动。

## 调试窗口修复

正式输入 page 原先被错误要求必须出现在 SettingsSchema，导致 PixelCount 打开时提示参数页组装失败。原厂 PixelCount 的基本参数页是输入-only 页面，已将公共 NativeInputPlacementMap 调整为允许由 PageOnlyPages 承载。未保留伪持久化 Field，PixelCount 准入恢复正常。最终 Agent Desktop 隔离方案验证通过，三页签基本参数、运行参数、结果显示出现，窗口正常关闭。最终封包 SHA-256：F59D842B9D6B745EE158A468AE69878417EE9CC2E25F8983574D754238A9D6E8。
