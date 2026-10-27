# AI 协作交接单

- 基线提交：fa6200a5（本批提交前基线）；当前分支：main。
- 当前执行者：Codex，主线唯一产品写者；三路只读逐控件核证并自动补位。
- 任务目标：第一阶段完成Blob所需原控件及完整组合，再准入Blob重组；本批为C08原Box子集，不是完整C08。
- 允许修改目录：现有src/Plugin.Wpf、正式验证脚本、原O06方案及协作文档。Blob业务冻结；用户BlobParameterEditor.xaml索引2保持未提交。
- 已修改文件：Adapters/Regions/NativeBoxRoiSession.cs、NativeRoiImageFrame.cs；Presenters/Regions/NativeBoxRoiEditor.cs、NativeBoxRoiViewport.cs；激活原BoxROIStack/RoiSelectControl仅Box分支/LeftRightControl/ROIDefaultValueToVisibilityConvert、原RoiType及NumCompareConverter；原资源局部闭包BoxRoiResources；原图像/布局/数值必要适配；Catalog及O06WindowPresentationCases、原方案生成器和数据。

## 已完成

1. 原Box五项数值通过同一INormalizedRectROI投影连接原ImageViewBox/NormalizedRectROI；实际物理帧、宽高固定，Group+Guid及成员对象核身份。数字三层真实绑定先校验、整候选一次接纳；未编辑分量保精度，非法/宿主拒绝不半写，合法Enter失焦。
2. 真实矩形显示问题由实际PNG查出，而非内部属性断言带过：原四布局DP的SetValue覆盖OneWayToSource，容器尺寸为零；保原绑定后矩形边线/手柄已可见。ROIItem转换器声明顺序修正。没有手写第二套Canvas位置。
3. 图像读取保持原帧lease并私有压紧行显示拷贝；BGR/BGRA位图验证涵盖行padding与透明通道，算法冻结像素不改。观察者异常完成后再抛，关键释放不中断；Acquire期间同步重绑旧帧只释放自身；读回调关闭不重建旧投影。
4. 卸载原文保真：原Loaded/PopupClosed可能重装格式绑定；独立raw快照、suspended门禁、会话/对象/加载代次、单次ContextIdle恢复。旧UI命令拒绝，显式捕获仍允许读取同对象未提交raw。不是轮询重绘。
5. Catalog本批27原来源记录、五程序集身份和必要本地适配；全部101条带原SHA记录复核。NumCompare原null/值类型/比较返回规则由定向静态反编译获取，证据O06_BOX_NUM_COMPARE_SOURCE_20261027.json。归档VM框架参考与SunEye本地源码分析严格区分。

## 未完成与边界

- 仅Box子集。完整RoiSelectControl其余分支、绘制/继承、多选/复制删除撤销、真实鼠标拖动缩放旋转/顶点、检测/屏蔽状态、独立无宿主VS设计场景、宿主scene revision/ABA、完整窗口显示和产品职责命名治理均未完成。
- 当前自动图像修改是原DP/绑定/结束命令，不冒称真实指针拖动。实际WPF PNG已查看，不代VM同态截图和用户人工验收。
- LeftRightControl原ParamListView祖先暂改ItemsControl，局部闭包而非完整C01；需恢复正式公共页面宿主。不得把原图像控件固有未证行为归为已实现。
- Session的_read没有显式物理帧/根版本快照，依赖固定Frame、isCurrent和宿主accept；ABA合同仍需补齐。当前单一矩形能力不能以过滤其他形状代替完整迁移。

## 已执行构建与测试

最终规定UI013零错误6621警告76.94秒、全量Release003零错误6654警告54.30秒。Window058=31、FieldEditing063=31、RuntimeEditor043=10、DesignTime019=28、FullChain102=7、Components187=9；实际正式EXE探针exit0/22工具；实际原矩形手柄PNG已查看。O06_BLOB_NATIVE_BOX_CHECKPOINT_20261027.json核24源码增量、760发布项、174加载SHA与本批六轨源码；不套历史九轨成绩。所有自有句柄均终止。Catalog全部101个已列原SHA复核。

保留失败：Window045名称域定位、046资源闭包、047资源前引用；048/049旧内部尺寸断言假绿但实际PNG缺矩形，050新实际容器断言暴露；051修后27组；052彩色/lease增加为30组；053/054卸载raw失败及缺绑定测试方法错误；055确认ClearBinding仍暴露模板表达式，056模板层覆盖注入仍不成立；057改坏路径绑定注入且raw保真后31组通过。UI008作用域3错误已修。以上失败不可删除或改成通过。

## 下一步原件核证（只读已返回）

- Sector是原Blob必需：安装AlgorithmTab第105—109行ROISelection=Box SectorRing Polygon Sector、SelectType=Mul、Max16。原SectorROIStack六项为中心X/Y、Radius、MinRadius、BeginAngle、AngleRange；X和半径乘实际W、Y乘H。角度度数，起角[-180,180]、范围默认360，OverSector才720；半径原MaxRadius=10000。不得搬HikCoverCircle2整套仅为常量。
- Sector栈XAML SHA 6e81bbbd3cda4b9693dde007612aa6529238237902ce2066dcc260ae8607bd7b，代码SHA 525f2106889abc6eedfb24f38c02f63a0d88e791218ffe56709a92b2490be38c，SectorROI.cs SHA e95855627d806f8cfa66429f69c4fb53fe112c3f9842ab04b75fe49973765c99。其布局SetValue和交互取消有同类风险；厂商SectorROIModel角度setter联动正定向补证。不要按短弧/radians猜映射，当前arc合同无内半径不可硬接。
- Polygon最小单位为原PolygonROIStack、SliderArrangePoint及PolygonROI/NormalizedPolygonROI/MoveAreaPage/原样式。原点列表归一化，图像Converter及PolygonROI只处理Replace、不正确处理新集合/数量；应非视觉修订退订/整形刷新。原SliderArrangePoint按Point.GetHashCode首次匹配有重复/碰撞问题，应顶点槽位+列表代次，不能按坐标/hash当身份。
- Polygon原栈无顶点增删按钮；绘制左键加点、右键撤回、默认最大32，可CustomMaxPointNum。尚未证已有形状单顶点插入删除UI，不能自己添加近似按钮冒充原件。GetIndexConverter已定向补齐：Apps.BaseData.GetIndexConverter、TypeDef02000010、Convert06000027/RVA2840，null返回0，否则unchecked(Convert.ToInt32(value)+1)，ConvertBack抛NotImplementedException；静态IL等价恢复非厂商源码文件。
- 绘制/继承原来源主证Module(sp)/Group.xml：RoiSelectGroup、InheritWayGroup2、ExternROITypeGroup；普通模式RadioSelecter，继承四方式RadioSelecter3，图形/图像RadioSelecterEx2，MaskImage才EnumerationG。所谓circle继承实际ROIANNULUS，不是CircleMes；屏蔽继承InputBlindPolygonString为字符串，不能套检测几何来源模型。位姿AssistGroup Boolean Assist0+RadioSelecter3 SelectWay，FIXTURE/点/坐标三分支，未实现。全部精确后续证据另归O06_ROI_NEXT_COMPOSITIONS_STATIC_20261027.json。

## 人工验收结果与禁止改动

真实VS/VM同态/用户本批均未验，不代签，第一阶段不退出。按需先让用户检查时必须停止重建程序集。不得改用户SelectedPageIndex=2、推送远端、恢复旧SDK可视后备或提前改Blob业务。所有验证只用主仓同源正式Release，不复制产品/算法库/仓库。

本批另补Loaded中同步选择ROI取消旧恢复后解除挂起门禁，最终Window058已覆盖。Sector角度精确CIL和缓存副作用已闭合，详见下一组合静态证据；不能按字段同名猜接。
