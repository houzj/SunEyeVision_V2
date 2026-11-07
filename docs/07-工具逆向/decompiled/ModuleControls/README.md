# 工具模块控件程序集（ModuleControls）

> VisionMaster 4.4.0 中**工具模块自带参数控件程序集**（`*Control.dll`）的反编译重建结果。
> **不是产品代码，不参与构建、不被打包**；产品源码不得引用本目录任何文件。
> 继承关系：我方从本机原件直接反编译，未经逐条复核。换机后先用下方 SHA-256 核对原件版本。

## 一、为什么需要这一批

原件工具参数面板的控件不是集中在某个公共库里，而是**每个工具自带**：

1. 工具 XML 里写的 `<Xxx 参数名=...>` 的 `LocalName` 是 **DataTemplate 的 key**，不一定是类名。
2. 全局模板字典 `Apps.XmlUI/Styles/ParamUserControlDataTemplate.xaml` 先查；查不到时由**工具自带的**
   `datatemplate/paramusercontroldatatemplate.xaml` 追加自己的模板。
   例：`IMVSBlobFindLabelsModuControl` 自带模板只有一条
   `<DataTemplate x:Key="ThresholdAreaList"><local:ThresholdAreaPanel ParentNode="{Binding Data}"/></DataTemplate>`。
3. 控件类本身多为 `NameValueItem` 薄壳（`ThresholdAreaList` 全文只有 `public object Data => this;`），
   **真正的界面、取值、回写都在 Panel（UserControl）里**。
4. Panel 通过 `ParentNode → NameValueItem → GetTopBaseNode() → AlgorithmTabRoot.UserStringData`
   读写参数，取值格式由控件自己约定（如 `ThresholdArea` 字符串 `序号#是否新增#灰度#下限#上限#是否反向`）。

因此复刻某个工具的配置界面，必须同时拿到三样：**工具自带 `*Control.dll` + `Apps.XmlParser` 基础控件 + 已入库的 7 个控件库**。
缺任何一样都会出现「类型找不到」或「模板 key 无匹配」。

## 二、覆盖验证

以原件 `Module(sp)` 下 159 份工具 XML 中出现的 **173 个控件类型**为口径：

| 证据范围 | 命中 | 说明 |
|---|---|---|
| 仅 decompiled 根 7 个控件库（入本批之前） | 0 / 173 | 公共库只有参数模板，不含工具自有复合控件 |
| 加本批 73 个 `*Control.dll` | 110 / 173 | 复合控件全覆盖 |
| 再加 `Apps.XmlParser`（365 个类） | 172 / 173 | 基础控件（Boolean / Integer / Float / Enumeration / ROISelecter / ButtonSelecter / Expand / GroupLinkItem / ImageViewConfig …）全覆盖 |
| 剩余 1 项 `GlobalRunParam` | — | 已查实是 `Global/SolutionModule/SolutionModuleAlgorithmTab.xml` 的 XML 元素名，不是控件类型；实际解释率 173 / 173 |

## 三、来源与哈希

| 项 | 值 |
|---|---|
| 原机 | 本机已安装 VisionMaster 4.4.0（V4.4.0 Build20250516@1555331） |
| 源目录（工具控件） | `D:\Program Files\VisionMaster4.4.0\Applications\Module(sp)\x64\**\*Control.dll` |
| 源目录（基础控件） | `D:\Program Files\VisionMaster4.4.0\Applications\myLibs\Apps.XmlParser.dll` |
| 反编译工具 | `ilspycmd` 11.1.0.9782（`ilspycmd -p -o <out> <dll>`，项目模式） |
| 入库日期 | 2026-11-05 |
| 写入状态 | 反编译产物原样入库，未做重命名或裁剪 |

## 四、逐程序集清单

| 输出目录 | 源程序集 | 源相对路径 | 大小(B) | .cs | .xaml | SHA-256 |
|---|---|---|---|---|---|---|
| `AndModuleControl` | `AndModuleControl.dll` | `Module(sp)\x64\Logic\AndModule\AndModuleControl.dll` | 21504 | 7 | 1 | `7F252D4A7E886498E76C1D339593AF3E995AB36801E5C6070AB9678200F30413` |
| `BranchModule_STDControl` | `BranchModule_STDControl.dll` | `Module(sp)\x64\Logic\BranchModule_STD\BranchModule_STDControl.dll` | 15360 | 3 | 1 | `2B7182273CFCAE26F608101C621C100AC1D5B2410E08105B0088E609A1E5CC45` |
| `BranchStringCpmLControl` | `BranchStringCpmLControl.dll` | `Module(sp)\x64\Logic\BranchStringCpmL\BranchStringCpmLControl.dll` | 14848 | 3 | 1 | `0BF9271E5157D7CFB92C73046D9EFDB429A883F2AE5B08ABCA71F180E3491797` |
| `CalculatorModuleControl` | `CalculatorModuleControl.dll` | `Module(sp)\x64\Calculation\CalculatorModule\CalculatorModuleControl.dll` | 66560 | 7 | 4 | `5F581BEDC50103B53FD223AE447D77D3872E634F6E8DC7415B89D1DF4D1419A0` |
| `CameraIOModuleControl` | `CameraIOModuleControl.dll` | `Module(sp)\x64\Communication\CameraIOModule\CameraIOModuleControl.dll` | 7168 | 4 | 1 | `C63D1624ED970B880C70EA167BE1BBA77F951F40463FDDA16E74F416184E3F36` |
| `DataAnalysisModuleControl` | `DataAnalysisModuleControl.dll` | `Module(sp)\x64\Communication\DataAnalysisModule\DataAnalysisModuleControl.dll` | 40448 | 18 | 4 | `F9D9848C7F6EB60DCD2D78535A6521B8BAE0510285EA1767D9551597341EBF7E` |
| `DataAssembleModuleControl` | `DataAssembleModuleControl.dll` | `Module(sp)\x64\Communication\DataAssembleModule\DataAssembleModuleControl.dll` | 16896 | 6 | 2 | `54ACE39B3E769F910C2D2B58EE842A23CE4246A548560E30200868DDF13EE62E` |
| `DataClassificationModuleControl` | `DataClassificationModuleControl.dll` | `Module(sp)\x64\Logic\DataClassificationModule\DataClassificationModuleControl.dll` | 110592 | 20 | 3 | `4CA918CEA43996F62DB17419C036C20655D939C3BC97BF90D68654EED6EF5DE3` |
| `DataFilterModuleControl` | `DataFilterModuleControl.dll` | `Module(sp)\x64\Logic\DataFilterModule\DataFilterModuleControl.dll` | 94720 | 19 | 3 | `E94EAD713B4AE867B077FB8C4AA07E2CEE1E01DF48FD4103A0A6B4AA2BCE071A` |
| `DataQueueModuleControl` | `DataQueueModuleControl.dll` | `Module(sp)\x64\Global\DataQueueModule\DataQueueModuleControl.dll` | 44544 | 10 | 4 | `E6E9AA2F77BD93120883744EED4E15C573919D0EC64E93FCB07187457EAB635A` |
| `DataRecordModuleControl` | `DataRecordModuleControl.dll` | `Module(sp)\x64\Logic\DataRecordModule\DataRecordModuleControl.dll` | 14848 | 4 | 1 | `C83EBD35AE3D20B3C13E43265E681F60151B23BB0DB547460ADDD71AF7F479A4` |
| `DataSetModuleControl` | `DataSetModuleControl.dll` | `Module(sp)\x64\Logic\DataSetModule\DataSetModuleControl.dll` | 14336 | 4 | 1 | `8A4E4E9F2A83AD63974BCBF17F2F539EC3ACE1304343E8C3FE1F8895B6B5F5A4` |
| `DataSortModuleControl` | `DataSortModuleControl.dll` | `Module(sp)\x64\Logic\DataSortModule\DataSortModuleControl.dll` | 84480 | 14 | 3 | `78A59C00DF287CFB9DB930CA0330EC4EFE4CCF752CD6F5DAE35485B2609B5EE0` |
| `DynamicObjectConfigControl` | `DynamicObjectConfigControl.dll` | `Module(sp)\x64\.CommonControl\DynamicObjectConfigControl.dll` | 350720 | 89 | 21 | `38832B22FD4224F73D12488C5D749AA11F3BBA2D5A31E6B4D46A603AEB0FA322` |
| `DynamicObjectConfigControl.DataFilterModule` | `DynamicObjectConfigControl.dll` | `Module(sp)\x64\Logic\DataFilterModule\DynamicObjectConfigControl.dll` | 211968 | 60 | 14 | `D9FBCD794DF4BB1F448E387F858F3440C6F0B48D8606CF9937D843B66A64B255` |
| `ExtensionParamUserControl` | `ExtensionParamUserControl.dll` | `Module(sp)\x64\.CommonControl\ExtensionParamUserControl.dll` | 13312 | 4 | 3 | `D842A44B89FABD960001C0F70465B826C074435A1E5BEAEE3B0F2F6FE832D4E0` |
| `FeatureMatchCommonControl` | `FeatureMatchCommonControl.dll` | `Module(sp)\x64\.CommonControl\FeatureMatchCommonControl.dll` | 46592 | 8 | 3 | `977A80C3BB2C548E0EC447F9E30F569026C5BCC3D4B1A46F40AA5A738645E77E` |
| `FewShotCommonControl` | `FewShotCommonControl.dll` | `Module(sp)\x64\.CommonControl\FewShotCommonControl.dll` | 201216 | 35 | 9 | `EAE9DF05B5E2795DAD29867AE394E9852952D6664EE54F96299ACF394D9BA684` |
| `FormatModuleControl` | `FormatModuleControl.dll` | `Module(sp)\x64\Logic\FormatModule\FormatModuleControl.dll` | 97280 | 26 | 6 | `3B5C9D1C43385A5665B247F16A416C78AD425DB6182A45B80A1E43B79C7DBAD4` |
| `GlobalCameraModuleControl` | `GlobalCameraModuleControl.dll` | `Module(sp)\x64\Global\GlobalCameraModule\GlobalCameraModuleControl.dll` | 113152 | 40 | 7 | `5E6A9275C63CFF8FECB1EDE0BE287F141CB72A000C4EB1431A44E6D9E950BA8A` |
| `GraphicsSetModuleControl` | `GraphicsSetModuleControl.dll` | `Module(sp)\x64\Logic\GraphicsSetModule\GraphicsSetModuleControl.dll` | 33280 | 8 | 3 | `E6F42B712BE2CA3FEDD136684CE404EE430BFBB47CCC0C9D609B46CB091A7B04` |
| `IfBranchModuleControl` | `IfBranchModuleControl.dll` | `Module(sp)\x64\Logic\IfBranchModule\IfBranchModuleControl.dll` | 16896 | 4 | 1 | `5C7446D7FF008A8089C4E38B4EB3F75BCA3910A3D3DEF02901F99DC849102C3E` |
| `IfModuleControl` | `IfModuleControl.dll` | `Module(sp)\x64\Logic\IfModule\IfModuleControl.dll` | 23040 | 5 | 1 | `A528C4D93007EC9FF4011DA66DB3E5EF7E19AC13BE6FC60389D7F0CAE7DACEB0` |
| `ImageAcquisitionModuleControl` | `ImageAcquisitionModuleControl.dll` | `Module(sp)\x64\Collection\ImageAcquisitionModule\ImageAcquisitionModuleControl.dll` | 32768 | 9 | 1 | `94DAF6CEA5641A9D997BBFA56454CFA5CAFBF13B11B142626834E23112DCE6D8` |
| `ImageCollectHikModuControl` | `ImageCollectHikModuControl.dll` | `Module(sp)\x64\Global\ImageCollectHikModu\ImageCollectHikModuControl.dll` | 11776 | 3 | 2 | `A484CF6E79FC095181ABFF768212539112060B391DFA877710BBE8BFDC52D455` |
| `ImageSourceModuleControl` | `ImageSourceModuleControl.dll` | `Module(sp)\x64\Collection\ImageSourceModule\ImageSourceModuleControl.dll` | 39936 | 7 | 3 | `04A73B135D18641B65B2815A292B795B922CB675122D6555F61947F372958906` |
| `IMVSAffineTransformModuControl` | `IMVSAffineTransformModuControl.dll` | `Module(sp)\x64\ImageProcessing\IMVSAffineTransformModu\IMVSAffineTransformModuControl.dll` | 14848 | 2 | 1 | `E59AE3CF056F229764BE59F9DC3C3A951032BBC420CEACC0407E383DE2121B36` |
| `IMVSBlobFindLabelsModuControl` | `IMVSBlobFindLabelsModuControl.dll` | `Module(sp)\x64\Location\IMVSBlobFindLabelsModu\IMVSBlobFindLabelsModuControl.dll` | 19968 | 4 | 2 | `5ED0A2D042416155DF1CAF67AD13E7A145245773858BF8630C93D1FA0DEEB09D` |
| `IMVSColorExtractModuControl` | `IMVSColorExtractModuControl.dll` | `Module(sp)\x64\ColorProcessing\IMVSColorExtractModu\IMVSColorExtractModuControl.dll` | 38400 | 6 | 2 | `6ACAE469047E7369ABF2A900423FC442804BB8249EB0C3A42E66A3F76D3E719D` |
| `IMVSColorRecognitionModuControl` | `IMVSColorRecognitionModuControl.dll` | `Module(sp)\x64\ColorProcessing\IMVSColorRecognitionModu\IMVSColorRecognitionModuControl.dll` | 115712 | 26 | 5 | `ED8B29080FB82D95CC883A25B9AF24564B27055BBB4A56CEE421263462DC192D` |
| `IMVSColorSegmentModuControl` | `IMVSColorSegmentModuControl.dll` | `Module(sp)\x64\ColorProcessing\IMVSColorSegmentModu\IMVSColorSegmentModuControl.dll` | 49664 | 6 | 2 | `B373E6B2ABE6998E27B603BC5D61D2789897F619164ED235E99D049A35E63FCB` |
| `IMVSContourMatchModuControl` | `IMVSContourMatchModuControl.dll` | `Module(sp)\x64\Location\IMVSContourMatchModu\IMVSContourMatchModuControl.dll` | 57856 | 12 | 4 | `67F4FA218C54EC9203DE8E38D21AF14A0F55C9054A079791026F84F430FAD461` |
| `IMVSEdgeFlawInspModuControl` | `IMVSEdgeFlawInspModuControl.dll` | `Module(sp)\x64\DefectDetection\IMVSEdgeFlawInspModu\IMVSEdgeFlawInspModuControl.dll` | 61440 | 9 | 3 | `5A1A623F78F75DA7C07EEA5CA486641D805376FEFB9833AC12D4B8A681DAD375` |
| `IMVSEdgePairFlawInspModuControl` | `IMVSEdgePairFlawInspModuControl.dll` | `Module(sp)\x64\DefectDetection\IMVSEdgePairFlawInspModu\IMVSEdgePairFlawInspModuControl.dll` | 74240 | 10 | 3 | `A23D06611C2FB230B46BAF7E0C4A007B6900C30A30CC4554E55F1450603542C9` |
| `IMVSFastFeatureMatchModuControl` | `IMVSFastFeatureMatchModuControl.dll` | `Module(sp)\x64\Location\IMVSFastFeatureMatchModu\IMVSFastFeatureMatchModuControl.dll` | 51712 | 9 | 4 | `7446B769F31FFD7EAFD9BFF0F35D052BEA14303348DFE5E5A94BDA9EBA160014` |
| `IMVSFixtureModuControl` | `IMVSFixtureModuControl.dll` | `Module(sp)\x64\Location\IMVSFixtureModu\IMVSFixtureModuControl.dll` | 8192 | 2 | 1 | `86227DF2FC93522CB746FB19C12BC4BCFD1FBF791E58FABA8088D0A6E457E4B0` |
| `IMVSFrameMeanModuControl` | `IMVSFrameMeanModuControl.dll` | `Module(sp)\x64\ImageProcessing\IMVSFrameMeanModu\IMVSFrameMeanModuControl.dll` | 15360 | 5 | 1 | `76B8DB8479265E2ED30CBCBCF0C3BDABDB7BEA08DA852C63EC18ABC6073E0E6A` |
| `IMVSGluePathConductModuControl` | `IMVSGluePathConductModuControl.dll` | `Module(sp)\x64\Location\IMVSGluePathConductModu\IMVSGluePathConductModuControl.dll` | 65536 | 9 | 3 | `75B4D8E8BE62322A278BBAB4CDABE433DE03B9235959AE369D465573D2C5EA81` |
| `IMVSGrayMatchModuControl` | `IMVSGrayMatchModuControl.dll` | `Module(sp)\x64\Location\IMVSGrayMatchModu\IMVSGrayMatchModuControl.dll` | 49152 | 6 | 4 | `969DF4FBA11C77C2198BF3B469BDDF8AAE57D14B45D12EDC05C11F0C7EBC0FA4` |
| `IMVSGrayMatchModuVAControl` | `IMVSGrayMatchModuVAControl.dll` | `Module(sp)\x64\Location\IMVSGrayMatchModuVA\IMVSGrayMatchModuVAControl.dll` | 52736 | 8 | 4 | `B15EEC39371929D1BB03475DBE8ABD88B6117A909CCD771453C77DD3EDFAFECF` |
| `IMVSGroupControl` | `IMVSGroupControl.dll` | `Module(sp)\x64\Logic\IMVSGroup\IMVSGroupControl.dll` | 12288 | 3 | 2 | `60877619777F14C2FEB3B4121A0A251DFE016EFDE87E18286EB8015BDA78D182` |
| `IMVSHPFeatureMatchModuControl` | `IMVSHPFeatureMatchModuControl.dll` | `Module(sp)\x64\Location\IMVSHPFeatureMatchModu\IMVSHPFeatureMatchModuControl.dll` | 115712 | 12 | 5 | `308692795D306770043F8903D2D6F1C6A9D389E08DED91D160332A94FA83DCB8` |
| `IMVSImageCombineProcessModuControl` | `IMVSImageCombineProcessModuControl.dll` | `Module(sp)\x64\ImageProcessing\IMVSImageCombineProcessModu\IMVSImageCombineProcessModuControl.dll` | 32256 | 9 | 2 | `D4DEEB822B8103AB9C84166BCE3B08DFB0A95B638D6BF5F9B44A8E004897D807` |
| `IMVSImageNormlizeModuControl` | `IMVSImageNormlizeModuControl.dll` | `Module(sp)\x64\ImageProcessing\IMVSImageNormlizeModu\IMVSImageNormlizeModuControl.dll` | 10752 | 3 | 1 | `F1C32EBF3386F550591A43DFF9A6A2D218BBD781CF3657BCC81AF028FB079652` |
| `IMVSImgStitchCalibModuControl` | `IMVSImgStitchCalibModuControl.dll` | `Module(sp)\x64\ImageProcessing\IMVSImgStitchCalibModu\IMVSImgStitchCalibModuControl.dll` | 104960 | 24 | 5 | `B2AFD9A264C9534F17ABF52070E1C2591AB83FB28E752F5E5CC9B3C7C1AA37E9` |
| `IMVSInspectModuControl` | `IMVSInspectModuControl.dll` | `Module(sp)\x64\DefectDetection\IMVSInspectModu\IMVSInspectModuControl.dll` | 176640 | 18 | 7 | `A887607BEC24FD4C373018E17B6D87F4063A72D411D736D4FBCA55957DC0DF01` |
| `IMVSMachineLearningClassifierModuControl` | `IMVSMachineLearningClassifierModuControl.dll` | `Module(sp)\x64\Recognition\IMVSMachineLearningClassifierModu\IMVSMachineLearningClassifierModuControl.dll` | 160768 | 19 | 7 | `88280159348EF9E445243229C83E9CB4EA0FF9DD0DBF33C7D9DD477D0E248A80` |
| `IMVSMarkFindModuControl` | `IMVSMarkFindModuControl.dll` | `Module(sp)\x64\Location\IMVSMarkFindModu\IMVSMarkFindModuControl.dll` | 100864 | 9 | 3 | `D7362B1B4F2C04F416AF1E3DCE5B61616067968B1CA1561FA81D6BDAF7270E2C` |
| `IMVSMarkInspModuControl` | `IMVSMarkInspModuControl.dll` | `Module(sp)\x64\DefectDetection\IMVSMarkInspModu\IMVSMarkInspModuControl.dll` | 211456 | 17 | 8 | `D7FC6459A670A182511A10C8B53E426C6BE1784BB53AB0BBBB88237A79CF848B` |
| `IMVSMarkInspModuVAControl` | `IMVSMarkInspModuVAControl.dll` | `Module(sp)\x64\DefectDetection\IMVSMarkInspModuVA\IMVSMarkInspModuVAControl.dll` | 213504 | 19 | 7 | `09D0D996ACB047BAC33C9E2EE3C656B7539C71D95D78E5D829C9C89A3E7B0E9E` |
| `IMVSNImageCalibModuControl` | `IMVSNImageCalibModuControl.dll` | `Module(sp)\x64\Calibration\IMVSNImageCalibModu\IMVSNImageCalibModuControl.dll` | 7168 | 2 | 0 | `555FE8377048ED2264F54F7172E8BA98584AC796B504491B780C073AD3BE280C` |
| `IMVSNPointCalibModuControl` | `IMVSNPointCalibModuControl.dll` | `Module(sp)\x64\Calibration\IMVSNPointCalibModu\IMVSNPointCalibModuControl.dll` | 30720 | 5 | 2 | `70866599127CDE7B424A4B696C62BB1F0719DE8C9B486A0B0BA731B6B8A146CD` |
| `IMVSOcrModuControl` | `IMVSOcrModuControl.dll` | `Module(sp)\x64\Recognition\IMVSOcrModu\IMVSOcrModuControl.dll` | 87040 | 8 | 3 | `1B9B45B11DD09427733DC797E73B522CD550E4F4015F4465DF8CB75F7DD4506E` |
| `IMVSPixelCountModuVAControl` | `IMVSPixelCountModuVAControl.dll` | `Module(sp)\x64\Measurement\IMVSPixelCountModuVA\IMVSPixelCountModuVAControl.dll` | 18432 | 6 | 3 | `1B0E731C72DCC9D3A4E497F616B6BB6F76A0B73366AFA8DC303978BBB92EF0B1` |
| `IMVSTargetTrackModuControl` | `IMVSTargetTrackModuControl.dll` | `Module(sp)\x64\Location\IMVSTargetTrackModu\IMVSTargetTrackModuControl.dll` | 7168 | 2 | 1 | `2678F8AF11D1B8FD1EA1ACBB025BEFE7D8A5DDFFB9D49DEAA0AB67FF76CDE785` |
| `LightModuleControl` | `LightModuleControl.dll` | `Module(sp)\x64\Collection\LightModule\LightModuleControl.dll` | 5632 | 2 | 1 | `E53EDC8232D9D22EAF76735BEAAFB6B6AFEE68B876AFF1A440C1E84CD912CC8D` |
| `OCRModuleCommonControl` | `OCRModuleCommonControl.dll` | `Module(sp)\x64\.CommonControl\OCRModuleCommonControl.dll` | 33792 | 4 | 2 | `18E1F487137B6930831253422EEC6007C663A811CBCF38FD347184EB988019F1` |
| `PointSetMODU_STDControl` | `PointSetMODU_STDControl.dll` | `Module(sp)\x64\Logic\PointSetMODU_STD\PointSetMODU_STDControl.dll` | 32768 | 7 | 1 | `C21CC3351D216AC9E373830C3D94D2539B5C46504C3E313E79146FA69EF8EC9A` |
| `ProcedureModuleControl` | `ProcedureModuleControl.dll` | `Module(sp)\x64\Global\ProcedureModule\ProcedureModuleControl.dll` | 10752 | 2 | 1 | `DC865095E5BB08F34598B9176B92A6D852E313FB1F4F6987AA2B669DC8EE7AC5` |
| `PyShellModuleControl` | `PyShellModuleControl.dll` | `Module(sp)\x64\Logic\PyShellModule\PyShellModuleControl.dll` | 92672 | 11 | 4 | `C15D84B5EA334FBABEAD90809B383E8955F4A4DAB5050565AC3E3AFA91F3826A` |
| `ReadCalibFileModuControl` | `ReadCalibFileModuControl.dll` | `Module(sp)\x64\Calibration\ReadCalibFileModu\ReadCalibFileModuControl.dll` | 22016 | 12 | 2 | `9C5C2C529B5242F9735A51DEF23C30FE90B2E3CBFE16C1FF31F95D0F1C129300` |
| `ReadDatasModuleControl` | `ReadDatasModuleControl.dll` | `Module(sp)\x64\Communication\ReadDatasModule\ReadDatasModuleControl.dll` | 46592 | 11 | 1 | `4E6D3E6708C276375797B28227F520C1E015D35F8022F037884F0E57D98A5D2E` |
| `SaveImageControl` | `SaveImageControl.dll` | `Module(sp)\x64\Collection\SaveImage\SaveImageControl.dll` | 20992 | 10 | 1 | `8A2B1611F192152FF1941271F32515C86ADF2F452CBEF4B1957C0A15BCB3A067` |
| `SaveTextModuleControl` | `SaveTextModuleControl.dll` | `Module(sp)\x64\Logic\SaveTextModule\SaveTextModuleControl.dll` | 15360 | 3 | 1 | `0DAF3F4B9DCCF2EF885EAAFFB4D85A729CA61ECD8747655A9FA13E98E88FC846` |
| `SCGlobalCameraModuleControl` | `SCGlobalCameraModuleControl.dll` | `Module(sp)\x64\Global\SCGlobalCameraModule\SCGlobalCameraModuleControl.dll` | 11776 | 3 | 2 | `A484CF6E79FC095181ABFF768212539112060B391DFA877710BBE8BFDC52D455` |
| `SendDatasModuleControl` | `SendDatasModuleControl.dll` | `Module(sp)\x64\Communication\SendDatasModule\SendDatasModuleControl.dll` | 104960 | 27 | 1 | `E310F5C69A0B1F3D64058DC604A079F1DDBF73ED99A0D3B988EBA5D4F58DEFD0` |
| `ShellCommonControl` | `ShellCommonControl.dll` | `Module(sp)\x64\.CommonControl\ShellCommonControl.dll` | 17408 | 3 | 0 | `299E2E7808F40B1BA9EDAD3743375032C2A3BB71D9CE847CEF4D39CD9DE1C073` |
| `ShellModuleControl` | `ShellModuleControl.dll` | `Module(sp)\x64\Logic\ShellModule\ShellModuleControl.dll` | 116736 | 15 | 5 | `6FFABF07C52E8EF0519FCCC9F88F2B05D49AE34051ECDFD350EB5AA8702D192E` |
| `SolutionModuleControl` | `SolutionModuleControl.dll` | `Module(sp)\x64\Global\SolutionModule\SolutionModuleControl.dll` | 10752 | 2 | 1 | `E785189D140E5B98051E3F010AAF5979147B4838E7710480DAFA8AA05687EA9D` |
| `StringCompareModuleControl` | `StringCompareModuleControl.dll` | `Module(sp)\x64\Logic\StringCompareModule\StringCompareModuleControl.dll` | 32768 | 9 | 2 | `B6935442321067BBB886C2DD76413D30219D27319CC92D8DF467F549B27A6B67` |
| `TimeStatisticModuleControl` | `TimeStatisticModuleControl.dll` | `Module(sp)\x64\Logic\TimeStatisticModule\TimeStatisticModuleControl.dll` | 37376 | 9 | 2 | `7CD816D1DE5740700BE196AD97317F0701BCF5DC327F1CEABFE24A02B618A18E` |
| `TranslationCalibModuControl` | `TranslationCalibModuControl.dll` | `Module(sp)\x64\Calibration\TranslationCalibModu\TranslationCalibModuControl.dll` | 29696 | 5 | 2 | `6353098907DF95E3E4F430B3372A97A9C7A5FEDAB8F656FBA50BBB1A1417B0BE` |

基础参数控件库（与本目录同级，单独放 `../../XmlParser/`）：

| 输出目录 | 源程序集 | 源相对路径 | 大小(B) | .cs | .xaml | SHA-256 |
|---|---|---|---|---|---|---|
| `XmlParser/Apps.XmlParser` | `Apps.XmlParser.dll` | `myLibs\Apps.XmlParser.dll` | 713728 | 365 | 0 | `78EC7AE1E5ADEFFF29197099C7109B153A3F75D092B7C3E9DC253D8C1B25A8D1` |

## 五、未入库项

| 源程序集 | 原因 |
|---|---|
| `Global\LightControl\LightControl.dll`（523,776 B） | 原生程序集，PE 无 CLR 运行时头，`ilspycmd` 无任何产出；其界面逻辑不在托管代码里 |

## 六、两份 `DynamicObjectConfigControl`

同名程序集在原件里存在两个**内容不同**的副本，均已入库，不要合并：

| 输出目录 | 源路径 | 大小(B) | 差异 |
|---|---|---|---|
| `DynamicObjectConfigControl` | `x64\.CommonControl\` | 350,720 | 公共完整版（111 文件 / 89 .cs） |
| `DynamicObjectConfigControl.DataFilterModule` | `x64\Logic\DataFilterModule\` | 211,968 | 模块私有精简版（75 文件 / 60 .cs），`GlobalRunParam` / 组合对象等分组参数在这两份里的实现范围不同 |

比对时以 `x64\.CommonControl\` 版为准，模块私有版只作该工具的特化差异参考。

## 七、使用约定

- 本目录**只读**：不复用其命名空间、不复制到产品源码、不引入构建。
- 查控件先按 `LocalName` 找同名 `.cs`；找不到再找同名 `datatemplate/paramusercontroldatatemplate.xaml` 的 key。
- 每个程序集目录里 `datatemplate/` 是该工具追加的模板字典，`*Panel.xaml` / `*Pannel.xaml` / `*Dialog.xaml` 是实际界面。
- `_sources.json` 是机器可读清单（含全部 74 条记录、状态与哈希），本文件表格由它生成。
- `_import.ps1` 是入库用的可重放脚本（需本机装了 `ilspycmd`），按 `_sources.json` 的同一规则重跑反编译；它只重建各程序集子目录与 `_sources.json`，不改本文件。换机或原件升级后重跑即可得到新哈希。
