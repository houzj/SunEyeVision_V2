"""Evidence-bound documentation only. Does not execute VM or alter product sources."""
from pathlib import Path
from html import escape as E
from hashlib import sha256
from datetime import datetime
from zoneinfo import ZoneInfo
import json, re

ROOT = Path(__file__).resolve().parents[2]
PAGE = ROOT / 'docs/04-工具设计/架构对照/2026/10/21/自研架构文档维护/优化方案/optimization-o06.html'
OUT = ROOT / 'artifacts/analysis/blob-backend-boundary-20261024'
FILES = [
 'src/Plugin.Contracts/SunEyeVision.Plugin.Contracts.csproj',
 'src/Plugin.Contracts/Resources.cs',
 'src/Core/Interfaces/IDeviceManager.cs',
 'src/Plugin.Runtime/SunEyeVision.Plugin.Runtime.csproj',
 'src/Plugin.Wpf/SunEyeVision.Plugin.Wpf.csproj',
 'src/Plugin.SDK/Vision/Contracts/IVisionImage.cs',
 'src/Plugin.SDK/Vision/Contracts/IVisionRegion.cs',
 'src/Plugin.SDK/Vision/Hosting/VisionBackendExtractorLocator.cs',
 'src/Plugin.SDK/Models/SdkTypeVocabulary.cs',
 'src/SunEyeVision.HalAdapter/SunEyeVision.HalAdapter.csproj',
 'src/SunEyeVision.HalAdapter/HalconHelpers.cs',
 'src/SunEyeVision.HalAdapter/HalRegion.cs',
 'src/SunEyeVision.HalAdapter/HalImage.cs',
 'src/SunEyeVision.CvAdapter/CvRegionBridge.cs',
 'src/SunEyeVision.CvAdapter/CvVisionRegionAdapter.cs',
 'src/UI/App.xaml.cs',
 'src/UI/Converters/VisionImageToBitmapSourceConverter.cs',
 'src/UI/Services/Workflow/NodeResultManager.cs',
 'tools/SunEyeVision.BlobAnalysis/BlobAnalysisTool.cs',
 'tools/SunEyeVision.BlobAnalysis/BlobAnalysisParameters.cs',
 'tools/SunEyeVision.BlobAnalysis/BlobAnalysisResults.cs',
]
VM = [
 Path('D:/MyWork/REA逆向测试/worktrees/tool-plugins/analysis/blob-analysis/identity-first/inventory.json'),
 Path('D:/Program Files/VisionMaster4.4.0/MVDAlgorithmSDK/Includes/Algorithms/BlobFindCpp.h'),
 Path('D:/Program Files/VisionMaster4.4.0/MVDAlgorithmSDK/Runtime/x64/MVDBlobFindCpp.dll'),
 Path('D:/Program Files/VisionMaster4.4.0/MVDAlgorithmSDK/Runtime/x64/MVD_Algorithm.dll'),
 Path('D:/Program Files/VisionMaster4.4.0/MVDAlgorithmSDK/MVDTools/ExportMVDCode/components/data/paramKey/BlobFind.xml'),
 Path('D:/Program Files/VisionMaster4.4.0/MVDAlgorithmSDK/MVDTools/ExportMVDCode/components/data/moduleio/IMVSBlobFindModu.xml'),
 Path('D:/Program Files/VisionMaster4.4.0/MVDAlgorithmSDK/Samples/CSharp/BasicSamples/MVD_GeoFind/BlobFindCsDemo/BlobFindCsDemo.cs'),
 Path('D:/Program Files/VisionMaster4.4.0/MVDAlgorithmSDK/Includes/Common/VisionDesigner/MVDCore.h'),
 Path('D:/MyWork/REA逆向测试/docs/architecture-recovery/tool-rendering-entry-audit.resources/IMVSBlobFindModuAlgorithmTab.xml'),
]

def table(headers, rows):
    return '<div style="overflow:auto"><table><thead><tr>'+''.join('<th>'+E(x)+'</th>' for x in headers)+'</tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+E(x)+'</td>' for x in r)+'</tr>' for r in rows)+'</tbody></table></div>'

def render():
    chunks = ['''<!-- BLOB-BACKEND-BOUNDARY:BEGIN -->
<section id="br-backend-migration-review" aria-labelledby="br-backend-title">
<h3 id="br-backend-title">算法库可迁移性复审：本轮 HALCON，保留未来 OpenCV 路径</h3>
<div class="br-note"><b>明确推荐：</b>保留现有双适配层和中立结果方向，调整不完整的数据/资源合同及越界消费者；不重建整套框架，也不把现有适配层视为迁移已经完成。本轮仍用 HALCON，不实现 OpenCV Blob 后端，不改产品代码。此处按用户最新约束修正此前“HALCON仅作候选”的表述。</div>
<nav class="br-nav" aria-label="Blob整体审核"><a href="#br-vm-decision-evidence">关键VM证据</a><a href="#br-backend-contract-decisions">统一合同</a><a href="#br-backend-project-changes">工程调整</a><a href="#br-blob-unified-scope">本轮与未来范围</a><a href="#br-backend-acceptance">验收与决策</a></nav>
<svg viewBox="0 0 1080 245" role="img" aria-label="中立业务合同与可替换算法实现和显示桥的责任边界" style="width:100%;background:#142c39;border-radius:12px">
<g fill="#203f51" stroke="#76c9e6"><rect x="20" y="20" width="330" height="85" rx="10"/><rect x="385" y="20" width="330" height="85" rx="10"/><rect x="750" y="20" width="310" height="85" rx="10"/><rect x="385" y="140" width="330" height="85" rx="10"/></g>
<g fill="#e2f5ff" font-size="18" text-anchor="middle"><text x="185" y="50">工具业务与参数语义</text><text x="185" y="80">输入 / ROI / 筛选 / 判定</text><text x="550" y="50">中立数据与资源合同</text><text x="550" y="80">格式、坐标、集合、寿命、身份</text><text x="905" y="50">树 / 下游 / 显示消费者</text><text x="905" y="80">消费结果，不依赖 HRegion / Mat</text><text x="550" y="170">算法实现与引擎适配</text><text x="550" y="202">HALCON 本轮 ｜ OpenCV 未来</text></g>
<g stroke="#ffad48" stroke-width="3"><path d="M350 62H385M715 62H750M550 105V140"/></g></svg>
<p>图是推荐责任边界，不代表已有代码全部符合。算法库相关类型允许留在引擎实现与专用适配器内；禁止要求公共消费者理解其原生句柄。HALCON类型出现在HalAdapter的公开专用方法中，本身不算公共合同泄漏。</p>''']
    chunks.append(table(['核对点','当前实际证据','架构判断与处理'], [
 ('独立合同工程','Plugin.Contracts为net9.0；Resources仅IReadOnlyImage/CopyPixels及Gray8创建、RetainInput；Runtime/Wpf引用Contracts。','复用工程边界。未发现这些被查合同直接引用HALCON/OpenCV；但没有完整像素格式、步长和多通道信息，不能宣称桥接能力充分。'),
 ('关联输入边界','Core.IDeviceManager.CaptureImageAsync公开返回Task<OpenCvSharp.Mat>。','这是真实的OpenCV类型公共签名耦合，说明不能宣称整个程序均已库中立。设备实现可使用Mat，但进入节点公共图像链需明确适配；本轮记录关联调整，不扩大成设备子系统改造。'),
 ('SDK公开图像/区域','IVisionImage、BlobAnalysisResults图像/区域属性无HImage/HRegion；IVisionRegion使用System.Windows.Rect/Point。','保留中立结果语义；调整区域几何与运行合同的WPF依赖。SDK旧接口与新Contracts必须有明确单向适配，不形成两套权威。'),
 ('原生后端逃逸','VisionBackendExtractorLocator公开object返回值及ToHalcon/ToOpenCv；HalconHelpers同一HImage返回既可能借用Unwrap，也可能来自转换委托。','不是签名直接暴露HImage，但语义上允许原生对象逃逸，且接口不表达新对象/借用对象寿命。限定在装配/适配边界，明确租约或所有权结果；不让业务消费者依赖object强转。'),
 ('Blob业务执行','BlobAnalysisTool.Run开头GetHalconImage；内部Threshold→可选形态学→Connection→SelectShape→特征/排序/判定；Results用IVisionRegion。','本轮保留HALCON业务实现。未来换库需要替换算法实现与桥，不是改一次工厂即可；不要求把每个HALCON算子做成公共接口。'),
 ('宿主装配与显示','App注册HalImageFactory与Cv词汇，但未注册跨后端转换；UI显示switch HalImage/CvVisionImageAdapter，缓存回填直接HalImage.FromBitmapSource。','双后端基础真实存在，生产双向转换未据此证明。装配根可知道后端；显示桥可复用，但缓存恢复的HALCON硬编码及扩展分派应调整，不能让更换算法迫使树/绑定业务跟改。'),
 ('像素/区域契约','IVisionImage仅通道数+byte[]，未声明像素顺序/位深/步长；IVisionRegion.GetMaskData无原点/尺寸/集合索引，GetContour单一Point[]。','调整现有合同：明确格式/布局/坐标域/多连通域与孔洞/集合身份/空域含义，按所需能力扩展；不强制所有后端复制成整图掩码或单条轮廓。'),
 ('现有Cv实现的语义缺口','CvRegionBridge旋转矩形按轴对齐矩形绘制；仅屏蔽无检测时返回null；Cv区域轮廓仅External且返回第一条。Hal ROI桥支持全图减屏蔽、旋转几何与区域集合。','迁移能力不能由类名对称证明。以上属于迁移前必须修复/验证的实现或合同差异；本轮不实现OpenCV修复，不把它们报告为HALCON生产故障。'),
 ('资源和结果','BlobResults使用ResultResourceOwner，公开结果以IVisionImage/IVisionRegion承载；HalRegion/CvRegion分别包装各自native对象。','复用结果owner/reader机制，补齐转换、缓存、借用与克隆同一规则。类型中立不等于零拷贝、无泄漏或可跨进程。'),
    ]))
    chunks.append('<h4 id="br-vm-blob-library-evidence">VM Blob 算法库：已证实的链与未知部分</h4>')
    chunks.append('''<div class="br-card"><b>本机4.4样本的导入链：</b><p>IMVSBlobFindModu.dll → MVDBlobFindCpp.dll / CreateBlobFindToolInstance → MVD_Algorithm.dll / MVD_Algorithm_Process。</p><p>SDK公开Run、GetResult、ROI、Mask及Blob特征接口；wrapper导入Create/DestroyHandle、ReleaseWorkingMemory、ReleaseResultMemory。导入证明依赖存在，不能单凭导入顺序断言内部执行顺序或每次都调用释放。</p></div>
<p><b>厂商归属：</b>本机模块走VisionMaster所附MVD算法SDK；<a href="https://www.hikrobotics.com/en/machinevision/visionmaster/">海康机器人官方说明</a>将VM定位为自研图像处理算子平台。因此可归为海康机器人MVD算法体系；不能据此断言本机Blob内核每个子步骤均自主实现，或不存在内部第三方依赖。DLL CompanyName字段为空，不能用它独立证明厂商；尚无证据把原Blob内核认定为HALCON或OpenCV。</p>
<p><b>原Blob核心仍未知：</b>ProcessAlg/ProcessOutput完整函数体、阈值端点/自动阈值具体方法、连通性与孔洞配置在核心中的处理次序/边界像素、排序并列规则及特征公式尚未贯通。SDK/XML能证明配置与结果能力，不能替代核心实现证据。不同版本官方宣传也不能证明本机4.4的全部实现细节。</p>''')
    chunks.append(table(['业务环节','VM证据与限制','本轮HALCON适配原则'], [
 ('图像与检测/屏蔽区域','SDK有ROI/Mask；原模块XML有来源、绘制/继承与位置修正声明。内部运算先后未完整恢复。','复用已证业务模型；HALCON解析统一坐标域后构建有效域。只迁移确有需求且证实的规则。'),
 ('分割与连通','原Blob参数声明有thresholdType/Polarity及阈值；SDK提供连通域索引。具体分割谓词/邻域未知。','本轮沿HALCON适合的Threshold/Connection路径；不把VM枚举数值直接当本地语义。邻域、边界、反选范围须显式定义和真值测试。'),
 ('筛选与特征','SDK有AreaF、Perimeter、Circularity、LongAxis/ShortAxis等；相同名称不证明公式相同。','复用按需特征、区域筛选和业务判断思路；HALCON用原生批量操作。像素面积、周长、圆度、角度规范分别记录，不强求数值逐项一致。'),
 ('结果及显示','SDK结果提供Blob信息和轮廓点能力；现有Display声明不等于完整数据生命周期。','保留同一最终集合到记录/树/下游/轮廓的映射及资源owner；VM参数/输出尽量对应，允许命名、数量、公式和算法步骤有明确差异。'),
    ]))
    chunks.append('<h4 id="br-vm-decision-evidence">影响决策的关键证据：声明、调用及推论分开</h4>')
    chunks.append(table(['证据与可信边界','新核实的事实','本轮设计决策 / 不据此推断'], [
 ('本机原BlobFindCpp.h + BlobFindCsDemo.cs:333–359（SDK合同与样例，非主程序完整调用）','输入→输出开关→ROI→清屏蔽并逐个添加→Run；SDK未设ROI表示全图，GetResult说明未运行或失败返回NULL。','建立明确有效域与失败结果，不回显旧成功数据；HALCON依本工具业务顺序实现，不把SDK样例当VM主程序所有内部步骤。'),
 ('BlobFindCpp.h:240–279；样例336–342','Blob图像、二值图像可分别关；样例按当前显示选择只开一种；旧轮廓布尔开关建议改用BlobContourType。','借鉴按需求产出。我们已有OutputBlobImage/OutputBinaryImage/OutputContours，不重复实现；计算需求应同时覆盖下游/导出/显示，不能仅按当前Tab开关，更不能把隐藏样式等同无需计算。'),
 ('本机SDK BlobFind.xml / connectivity','明确声明FourConnected=4、EightConnected=8，SDK默认8。','连通性已不再是完全未知配置：本轮必须明确我们采用何种邻域并验证对角接触样本；不为复制VM而增加未需要的模式，也不修改HALCON进程级全局状态影响其他工具。核心实际分支仍未反编译贯通。'),
 ('同XML / ThresholdType、Polarity、HoleMinArea','声明已二值、单/双/自动、固定/相对软阈值；有孔洞参数。极性旧英文别名与中文描述存在反直觉对应。','模式语义先定义、用微小真值图验证；不能把自动阈值直接说成Otsu，不能从HoleMinArea名字推定填孔谓词，也不能按英文名称或枚举序号直接迁移。高级软阈值不自动纳入本轮。'),
 ('SDK BlobFind.xml 与原模块AlgorithmTab.xml对照','FindNum默认均100，但SDK参数文件最大3000，原模块声明最大20000；默认阈值等也有差异。','版本/载体分开保留。SDK样例默认不能替代VM模块配置；本轮结果容量由业务与性能验收决定，不复制100/3000/20000硬上限，更不能渲染抽样后冒全量业务结果。'),
 ('BlobFindCpp.h:205–216；moduleio/IMVSBlobFindModu.xml输出表达式','有SequentialContourNum/SequentialContour；导出代码按同一BlobInfo[index]读Centroid、Box和其他字段。','结果须保留多段轮廓和集合元素对应关系；采用记录为主、数组兼容投影。只证实接口/代码生成映射，不宣称VM运行时天然具备跨帧稳定ID。'),
 ('BlobFindCpp.h:328–338、534–540（SDK声明）','Clear释放结果缓存后禁止继续读取；RegionImageAssistant注明与所用ROI一致才能优化。','保留owner/reader生命周期；借鉴缓存正确性约束，缓存必须绑定输入内容/ROI/配置/后端版本。区域图像复用是否收益仍需测试，不能据接口存在新增全局缓存或预热。'),
 ('本机SDK目录出现OpenCvSharp.Blob.dll；原模块实际导入证据','附近有OpenCvSharp文件，但原Blob追到的是MVDBlobFindCpp→MVD_Algorithm。','旁置DLL不等于原Blob调用库；不据目录共存认定原Blob基于OpenCV，也不证明内核绝无第三方。'),
    ]))
    chunks.append('''<div class="br-note"><b>证据对当前选择已经足够的部分：</b>可决定结果按需产出、显式有效域与失败语义、多轮廓/记录映射、缓存和生命周期边界。本轮选用HALCON不依赖恢复VM每个像素公式。<b>仍影响特定算法选型的部分：</b>如业务确实要求VM软阈值或特征数值兼容，须针对该模式补核心或受控真值证据后再纳入，不能先假定等价。现有native分析工具未就绪的限制保留，不把SDK契约当核心反编译结果。</div>''')
    chunks.append('<h4 id="br-backend-contract-decisions">统一合同：保留业务语义，允许引擎选择最合适的实现</h4>')
    chunks.append(table(['合同领域','必须统一的语义','允许随引擎不同的部分'], [
 ('输入图像','像素格式/位深/通道顺序/步长、坐标原点、只读借用或拥有、Frame身份。新Contracts与旧SDK之间只做明确适配。','HImage、Mat及内存布局留在各自适配边界；按能力借用或转换，不承诺所有格式零拷贝。'),
 ('检测区与屏蔽区','绘制/继承解析出的几何、位置修正、空区域与未指定区域的区别、有效域及区域集合/孔洞语义。','HALCON区域运算和OpenCV掩码/标签均可；不强制以某库的内部区域对象作公共模型。'),
 ('算法请求','本工具实际支持的分割、筛选、判定及参数校验；明确模式、单位、有效范围。','不将VM枚举值直接作为HALCON枚举；不抽象每一个原生算子，不强求所有后端支持所有模式。'),
 ('结果记录与消费','同一最终Blob集合、稳定字段、运行内元素身份、Run/Frame对应、失败/空结果语义，树/图形/下游同源。','特征是否可提供及计算定义须声明；数值差异用业务真值/容差判断，未支持能力明确拒绝或标不可用，不能静默填零。'),
 ('显示与持久化','显示消费几何/像素和结果身份；保存参数及可重建资产，不保存HObject/Mat句柄。','显示桥可针对后端优化，绘图样式与ROI交互不跟随算法库切换；缓存键和失效规则随内容与版本变化。'),
 ('所有权与失败','借用输入不释放；新建转换对象由明确owner或lease承担；取消/失败/替换结果后的最后reader释放。','HALCON Dispose、Mat Dispose及原生缓冲回收分别实现；不能用同一object返回值掩盖两种所有权。'),
    ]))
    chunks.append('<h4 id="br-backend-project-changes">工程与消费者调整清单：不另起平行接口</h4>')
    chunks.append(table(['现有工程/入口','复用范围','调整或替换范围 / 关联影响'], [
 ('Plugin.Contracts / Resources.cs','独立于WPF及算法库的运行合同方向，IReadOnlyImage与IOutputResources现有入口。','按实际输入格式及资源所有权完善现有合同。先映射SDK现有图像/区域语义，避免再建第二套类型、存储或类型注册权威。'),
 ('Plugin.SDK / Vision.Contracts / SdkTypeVocabulary','IVisionImage、IVisionRegion及词汇映射机制；Blob结果以接口承载。','区域边界中的WPF几何逐步适配到已有中立几何合同；明确集合/孔洞与掩码布局。绑定树、结果树的语义类型不随HalImage/CvImage具体类切换。'),
 ('HalAdapter / CvAdapter / CvEngine','保持算法实现与专用转换分层，现有HALCON算子可继续高效使用。','完善借用/转换/释放；未来Cv接入修正旋转ROI、仅屏蔽及多轮廓等差异。不得为支持未来库先强迫HALCON走全量像素复制。'),
 ('BlobAnalysisTool / Parameters / Results','本轮HALCON流水线、参数业务规则、最终集合与资源owner。','把引擎相关调用集中在算法实现与专用桥，消费者只依赖业务合同。未来换库可替换该实现；不要求当前所有HALCON调用立即迁至通用算子层。'),
 ('UI装配 / VisionImageToBitmapSourceConverter / NodeResultManager','装配根选择后端、显示桥和结果身份校验。','缓存回填的HalImage硬编码改为受控创建/恢复路径；中立像素fallback需识别真实格式，不能把任意多通道一律解释为Bgr24。'),
 ('工作流 / 绑定 / 模块结果树 / 区域与结果渲染','现有快照、稳定字段、reader/owner和结果消费者边界。','沿同一输出合同及Run/Frame/Element核对适配，禁止下游依赖HRegion/Mat强转；若特征定义变更，参数迁移和结果展示须明确标识，不能静默改变旧方案含义。'),
 ('发布 / 许可证 / 测试','沿当前发布规则与独立验收入口。','本轮保留HALCON部署与许可。未来切库另核依赖/位数/native DLL/许可和工程发布；这是迁移责任，不是本轮新增产品实施任务。'),
    ]))
    chunks.append('<h4 id="br-backend-options">同标准比较与最终取舍</h4>')
    chunks.append(table(['方案','收益','代价与判断'], [
 ('只保留现有接口与工厂切换','当前改动最少。','不能解决像素布局、区域语义、所有权和消费者硬编码；不推荐把它宣称为完整可迁移架构。'),
 ('中立业务/数据合同 + 可替换算法实现 + 专用适配桥','消费者稳定，HALCON可用原生高效路径；未来OpenCV实现按同一业务验收。','推荐。需要补齐合同与真实消费链，承担显式转换成本，但复用现有工程和合理机制。'),
 ('将所有库算子包装成一套通用算子引擎','理论上可统一调用形状。','抽象范围大，易形成最低能力公约数并掩盖语义差异；本轮无证据支持，不采用。'),
 ]))
    chunks.append('<h4 id="br-blob-unified-scope">归入Blob完整闭环：本轮必要项、未来迁移项、暂不采用项</h4>')
    chunks.append('''<p>以下是同一Blob完整闭环方案的范围收敛，不是新增实施批次，也不是每行都要重写。已实现且有证据的机制复用并验证；存在缺陷才调整。此页仍待整体审核，不能视为产品实施放行。</p>
<div class="br-grid"><div class="br-card"><b>本轮HALCON闭环</b><p>创建/开窗 → 草稿与绑定/ROI → 校验提交 → 输入快照 → HALCON执行 → 同源结果 → 树/视口/下游 → 保存重开 → 关闭/删除/最后读者释放</p></div><div class="br-card"><b>未来后端迁移</b><p>在同一公共语义下替换算法实现与专用桥；不要求用户页面、绑定树、结果树和显示业务重新实现。</p></div></div>''')
    chunks.append(table(['范围/原分析引用','本轮必须满足的结果与处理','验证及边界'], [
 ('B1 · 创建、窗口壳与编辑（BV01–05；现有漏斗）','明确节点/编辑会话/运行实例所有者；工具栏和共享控件消费宿主能力，ROI绘制、继承、数值回填进入同一草稿；确定整体提交、取消回滚。复用已有合法机制，越界管理职责替换。','从拖入画布开始，连线/重开/取消/失败不部分写正式参数；不重新设计Blob整页外观。'),
 ('B2 · 输入与HALCON桥（BV06–08）','明确支持格式/通道/域/坐标；原生解包与新对象创建进入失败清理边界；借用与拥有有明确区分。以现有合同加适配补缺，不另建图像仓库。','Gray8和彩色按实际支持范围、偏移/旋转ROI、仅屏蔽、空域、非法输入、输入未被释放；不要求现在完成所有未来格式。'),
 ('B3 · 分割/连通/筛选/判定（BV06–08）','保持HALCON路径；把阈值与反选域、实际邻域、孔洞处理、过滤/排序/截取和业务NG定义清楚；所有需要的特征可追到同一最终集合。','对角像素、阈值边界、孔洞、并列排序、0/多Blob独立真值；HALCON最优实现由正确性和成本证据决定，VM未证公式不作验收真值。'),
 ('B4 · 输出与结果消费（BV08–10；结果树/绑定/下游）','复用已有输出开关/声明记录/owner；明确所需特征、图像、轮廓的请求来源；树、数组、图形地址与下游同一Run/Frame集合。','所有开关组合、隐藏后显示、下游仍需要输出、失败/空结果不残留；已通过有限合同测试复用，不能替代完整节点验收。'),
 ('B5 · 区域交互与结果渲染（原7类/结果层分析）','复用已证VM样式和交互证据；编辑层与只读结果层分开；核大量ROI命中、轮廓批绘/缓存和视口同步，纠正影响完整性的截断或错帧。','缩放/平移、旋转句柄、选择与参数同步、大轮廓/多对象同一输入对比；允许视觉LOD但不丢业务结果，未定量测量前不承诺速度。'),
 ('B6 · 缓存、资源和节点周边（BV11–12；br-node-peripherals）','中央显示、预览、节点数据分析按已有优化复核；统一内容/ROI/配置失效与owner/reader寿命。保存配置与运行native对象分开，最后reader释放。','保存重开、图像切换、修改ROI、连续运行、关闭/删除/取消/异常；缓存字节和滞留资源可核，不拿调用Dispose或缓存字典项数当完成。'),
 ('B7 · 公共合同/工程接入与交付','HALCON类型不进入公共消费合同；修复本轮真实链必需的所有权/格式/后端硬编码，保持现有Contracts/SDK单向适配和工程职责。','公共消费者不要求HImage/HRegion或Mat强转；关联输入IDeviceManager的Mat在进入节点链处适配，不扩大设备改造。全链独验后按已定Release路径供人工反馈。'),
 ('未来迁移专属，不列本轮必改','OpenCV Blob算法实现；Cv桥旋转矩形/仅屏蔽/孔洞及多轮廓差异修复；双向转换装配；替代库部署许可与端到端成本。','本轮只保留合同能力与缺口，不实现OpenCV后端、不冒可直接切换；未来按相同业务语义和真值验证，参数/数值尽量对齐但不强制一致。'),
 ('暂不采用，需业务证据再选','VM软阈值等高级模式逐项复刻、复制VM默认容量、全算子通用引擎、全局运行区域缓存或新增预热。','不因VM提供就全部迁移，不因现有代码存在就保留；只有影响当前业务闭环的证据成立才纳入。'),
    ]))
    chunks.append('''<h4 id="br-backend-acceptance">复用、调整、替换与验收边界</h4>
<div class="br-grid"><div class="br-card"><b>复用</b><p>现有Contracts/Runtime/Plugin.Wpf职责边界、Hal/Cv适配工程、类型词汇注册、中立结果、reader/owner与稳定结果身份。复用机制，不自动认可全部实现。</p></div><div class="br-card"><b>调整或替换</b><p>补全现有图像/区域合同，隔离WPF几何、原生object逃逸和不明所有权；替换缓存回填硬编码。未来Cv算法接入须纠正ROI/轮廓语义，禁止仅换工厂宣称迁移完成。</p></div><div class="br-card"><b>本轮不做</b><p>不更换HALCON、不实现OpenCV Blob、不新建全局引擎框架，不机械抽象每个算子，不重复改造在途已验范围；本页为整体审核建议。</p></div></div>
<ol><li>依赖门：公共运行合同及树/绑定消费者无HALCON/OpenCV类型要求；专用adapter可使用原生类型。装配与UI适配的后端识别集中记录。</li>
<li>数据门：Gray8/彩色/非紧密步长、空图、偏移ROI、旋转ROI、仅屏蔽、多个连通域及孔洞均有明确语义与独立真值。</li>
<li>生命周期门：同后端借用、异后端转换、取消/失败、缓存淘汰和最后reader退出分别核释放，输入不被误释放；不靠GC猜测所有权。</li>
<li>算法门：同一业务场景和样本比较检测正确性、排序/身份、稳定性、耗时与内存。VM作为对照；独立真值为正确性依据，特征公式不同设业务容差，不强制全部参数与结果相等。</li>
<li>未来迁移门：可用替代实现接入同一消费链而无需修改业务树/绑定/显示规则，允许后端适配和算法实现调整。此门本轮未运行，不称已支持无成本迁移。</li></ol>
<p><b>需要整体审核的推荐：</b>接受“HALCON本轮固定、公共语义中立、算法实现可替换”；按以上边界调整既有合同和消费者，而非新增并行接口。VM内核未知细节保留待核，不能作为强制复制依据。未发现需要用户在本轮重新选择算法库的决策。</p>
<details><summary>源码及VM证据索引（本次只读快照）</summary><ul>''')
    import os
    for name in FILES:
        rel = os.path.relpath(ROOT/name, PAGE.parent).replace('\\','/')
        chunks.append('<li><a href="'+E(rel)+'">'+E(name)+'</a></li>')
    evidence_url = os.path.relpath(OUT/'key-evidence.json', PAGE.parent).replace('\\','/')
    chunks.append('</ul><p><a href="'+E(evidence_url)+'">VM关键声明、样例调用和原模块配置对照（路径、行号与SHA）</a>。此处为静态复审，不是运行性能或独立验收报告。</p></details></section><!-- BLOB-BACKEND-BOUNDARY:END -->')
    return '\n'.join(chunks)

def apply():
    OUT.mkdir(parents=True, exist_ok=True)
    import xml.etree.ElementTree as X
    selected={'thresholdtype','polarity','connectivity','holeminarea','findnum','sortmode','sortfeature','blobcontourtype'}
    param_evidence=[]
    for f in [VM[4],VM[8]]:
        tree=X.parse(f).getroot()
        nodes=[]
        for node in tree.iter():
            if node.get('Name','').lower() in selected:
                nodes.append({'tag':node.tag,'name':node.get('Name'),'default':node.findtext('DefaultValue'),'max':node.findtext('MaxValue'),'entries':[{'name':e.get('Name'),'value':e.findtext('Value'),'description':e.findtext('Description')} for e in node.findall('EnumEntry')]})
        param_evidence.append({'path':str(f),'sha256':sha256(f.read_bytes()).hexdigest(),'nodes':nodes})
    excerpts=[]
    for f,needles in [
        (VM[1],['SetShowBlobImageStatus','SetShowBinaryImageStatus','GetSequentialContourNum','GetSequentialContour(','virtual void Clear()','GetRegionImageAssistant','virtual IBlobFindResult* GetResult()']),
        (VM[6],['ShowBlobImageStatus =','ShowBinaryImageStatus =','m_stBlobFindToolObj.Run();','m_stBlobFindToolObj.ClearMasks();','m_stBlobFindToolObj.Dispose();']),
        (VM[5],['GetBlobInfo($index)','BlobInfo[$index]']),
    ]:
        lines=f.read_text(encoding='utf-8-sig').splitlines()
        matches=[]
        for i,line in enumerate(lines):
            if any(n in line for n in needles):
                lo=max(0,i-7);hi=min(len(lines),i+2)
                matches.append({'line':i+1,'context':[{'line':k+1,'text':lines[k]} for k in range(lo,hi)]})
        excerpts.append({'path':str(f),'sha256':sha256(f.read_bytes()).hexdigest(),'matches':matches})
    (OUT/'key-evidence.json').write_text(json.dumps({'authority':'shipped SDK declarations, code-generation mapping and sample source; not VM product runtime or recovered core body','parameters':param_evidence,'sourceExcerpts':excerpts,'decisionScope':'B1-B7 are one proposed Blob lifecycle scope, not new implementation batches; existing implementations need revalidation, not automatic rewrite'},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    before = PAGE.read_bytes()
    page = before.decode('utf-8')
    start = '<!-- BLOB-FIVE-LAYER-REVIEW:BEGIN -->'
    end = '<!-- BLOB-FIVE-LAYER-REVIEW:END -->'
    a,b = page.index(start),page.index(end)
    region = page[a:b]
    region = re.sub(r'<!-- BLOB-BACKEND-BOUNDARY:BEGIN -->.*?<!-- BLOB-BACKEND-BOUNDARY:END -->','',region,flags=re.S)
    region = region.replace('当前保留HALCON算法作为基线候选，不批准引擎替换','本轮固定使用HALCON，设计保留未来后端迁移能力，不实施引擎替换')
    section = render()
    region = region.replace('<h3 id="br-node-peripherals">', section+'<h3 id="br-node-peripherals">',1) if '<h3 id="br-node-peripherals">' in region else region+section
    if 'href="#br-backend-migration-review"' not in region:
        region = region.replace('<h3 id="br-basis">','<p><a href="#br-backend-migration-review">本轮HALCON与未来OpenCV迁移边界复审 →</a></p><h3 id="br-basis">',1)
    after = page[:a]+region+page[b:]
    assert after[:a] == page[:a] and after[after.index(end):] == page[b:]
    from html.parser import HTMLParser
    class Parse(HTMLParser):
        def __init__(self): super().__init__(); self.ids=[]
        def handle_starttag(self,tag,attrs):
            if 'id' in dict(attrs): self.ids.append(dict(attrs)['id'])
    p=Parse();p.feed(after)
    for key in ['br-backend-migration-review','br-backend-title','br-vm-blob-library-evidence','br-backend-acceptance']:
        assert p.ids.count(key)==1,key
    assert all((ROOT/f).is_file() for f in FILES)
    evidence=[]
    for f in [ROOT/x for x in FILES]+VM:
        evidence.append({'path':str(f),'sha256':sha256(f.read_bytes()).hexdigest()})
    PAGE.write_bytes(after.encode('utf-8'))
    report={'time':datetime.now(ZoneInfo('Asia/Shanghai')).isoformat(),'scope':'read-only source/native inventory and official attribution; documentation only; not migration/runtime acceptance','recommendation':'HALCON now; reuse neutral contracts/adapters; adjust ownership, layout and consumers; future OpenCV requires algorithm implementation and semantic verification','pageBefore':sha256(before).hexdigest(),'pageAfter':sha256(PAGE.read_bytes()).hexdigest(),'outsideReviewBytesUnchanged':True,'evidence':evidence,'official':'https://www.hikrobotics.com/en/machinevision/visionmaster/','nativeLimit':'Original Blob ProcessAlg/ProcessOutput not fully recovered; imports prove dependency, not execution order or absence of third-party internals'}
    (OUT/'review.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='evidence'},ensure_ascii=False))

if __name__ == '__main__': apply()
