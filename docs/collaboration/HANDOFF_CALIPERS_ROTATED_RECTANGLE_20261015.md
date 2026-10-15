# AI 协作交接单：全卡尺交互与旋转矩形

- 基线提交：ce71b606f83331f6d508147ec060cd3ce46bf6d1。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，未跨AI交接、未提交推送。
- 任务目标：统一消除卡尺交互回写重入，补齐面板写回；修正Blob旋转外接矩形方向与检测区隐藏后残留箭头。
- 允许修改目录：src/Plugin.SDK、src/SunEyeVision.HalAdapter、src/UI、tools及内部协作文档。
- 已修改文件：MeasureEditorControl.xaml.cs、MeasureGuideParameterPanel.xaml.cs、RotatedRectangleHelper.cs、RegionConverter.cs、NodeResultManager.cs、BlobAnalysisTool.cs及CURRENT_TASK.md、本交接单。前序并存修改全部保留，尤其plans/plan.json。

## 已完成与覆盖

| 对象 | 排查结果与处理 |
| --- | --- |
| 直线查找 | 前批定位起点写回触发SetGuide覆盖终点。本批把防护扩展到SDK共享层，原工具局部保护保留 |
| 圆／圆弧查找 | 卡尺尺寸回写通知可触发中途SetCaliberParams；共享层保护。圆／圆弧面板原SetGuide不通知工具，补齐显式通知 |
| 线线、线圆、圆圆、点线、点圆 | 均使用MeasureElementsEditorControl和MeasureEditorControl，继承同一通知重入保护及面板写回修复；不声称所有工具此前都能复现直线查找的同一种端点症状 |
| 点点 | 无卡尺，未无差别修改 |
| 其他区域工具 | 使用ROI编辑器，不使用卡尺事件链；本批矩形转换与结果箭头样式共享修复适用公共区域路径 |

MeasureEditorControl在GuideGeometryChanged通知期间阻止SetGuide反向回填，在CaliberParamsChanged期间阻止SetCaliberParams反向回填。两者分开，绘制完成后的卡尺尺寸初始化不受几何通知保护影响；try/finally复位，不锁定执行或正常参数面板编辑。圆／弧新增带通知重载，保留原单参数公开签名及其无通知行为，只有面板主动操作请求通知。手柄绘制使用与QueryCursor相同的CursorForHandle，统一端点、半径、卡尺缩放与整体移动提示。

旋转矩形保持原持久化坐标、宽高和角度，SDK屏幕角顺时针为正，HALCON phi方向相反。RegionConverter.RoiToRegion补上负号，与现有ToolRegionHelper保持一致；Blob SmallestRectangle2结果转屏幕顶点时第一轴改为(cos(phi),-sin(phi))，法向为(sin(phi),cos(phi))。不通过交换长短轴或修改用户参数掩盖镜像方向问题。辅助类中与代码相反的宽高方向注释已纠正。

隐藏箭头的实际原因是NodeResultManager.BuildDirectionArrowItem克隆了独立GraphicsStyle，未复制或追踪区域IsVisible。现在箭头与轮廓引用同一个GraphicsStyle，其显隐、颜色、透明度和线宽同步，不再保留独立箭头默认样式。此处是结果显示层显隐；检测计算是否启用、ROI编辑层选中与命中仍由原有编辑器规则管理，不以结果显隐开关禁用算法。

## 构建、验收与边界

- 已执行构建与测试：全量Release成功，0错误1275警告（118.18秒）；随后独立UI Release成功，0错误1064警告（22.59秒）。23个插件主DLL及SDK／HalAdapter／UI发布DLL与相应编译中间DLL的SHA256全部一致，git diff --check通过。遵用户前序要求不新增／运行测试，不启动程序、VS设计器或连接设备。
- 人工验收结果：本轮暂无，不用源码检查或历史自动化通过代替本轮视觉验收。
- 未完成：真实图像与真实窗口下的交互、视觉人工验收；完整运行轮次／模板版本／统一撤销设计仍不属本批。
- 已知风险或禁止改动：不操作真实方案／设备，不重放TCP报文，不覆盖既有改动，不交换旧方案矩形宽高或重写角度。
- 下一步：后续如获准运行，按正负角、圆弧面板、双卡尺平移及隐藏后再显示检查实际画面；不得用编译通过宣称实际画面已验收。
