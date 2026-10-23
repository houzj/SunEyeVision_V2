const fs=require('fs'),crypto=require('crypto');
module.exports=function(){const folder='tools/SunEyeVision.Tool.ShapeMatch/';function ref(path,token){const t=fs.readFileSync(path,'utf8'),ls=t.split('\n'),i=ls.findIndex(l=>l.includes(token));if(i<0)throw Error(path+token);return {path,line:i+1,token,sha256:crypto.createHash('sha256').update(t).digest('hex'),snippet:ls.slice(i,i+20).join('\n')};}
const vmPath='D:/MyWork/REA逆向测试/docs/architecture-recovery/tool-plugin-map.json',text=fs.readFileSync(vmPath,'utf8'),vm=JSON.parse(text).tools.find(t=>t.key==='Location/IMVSContourMatchModu');if(!vm?.declarations)throw Error('VM shape contract');
const r={id:'ShapeMatch',name:'轮廓匹配',route:'原方案整改＋VM合同对账；算法迁移暂缓',difference:'载入入口未实现，缓存退役和最佳结果语义需整改',status:'主调试/执行/缓存已局部静态核对；VM原算法等价、native最终销毁和运行未验证',
layout:'主调试四页：基本参数、模板管理、运行参数、结果显示。模板管理左侧创建/载入/列表，右侧预览/编辑/删除所有；另开模板编辑器，不把它当主调试页。',
chain:'主DebugControl.ExecuteTool → ExecuteWithLiveContext → ShapeMatchTool.Run → 灰度/修正ROI → 启用模板 → 模型缓存 → 分组单/联合FindScaled → 跨模板NMS → 显示排序/截断 → 判定/显示/修正矩阵',
parameters:'搜索角度/尺度、最低分数、每模板候选数、重叠、最终上限、排序、层级、Greediness/SubPixel/超时；创建期角步长/尺度步长/对比度不能误当本次find参数。模板含稳定Id、ModelData和启用/提取状态。',
algorithm:'搜索域/Mask约束模型参考点，不涂黑像素。按原生模型MinContrastSignature分组：单模板FindScaledShapeModel，多模板FindScaledShapeModels；联合返回模型索引映射回稳定模板ID。候选先按分数/模板顺序NMS，再按显示排序截断。无可用模板和零匹配都统一强制NG。',
transfer:'输出GrayImage/OutputImage/SearchRegionMask和匹配列表、姿态/分数/尺度/模板信息、轮廓及修正矩阵。BestScore取列表最大分数，BestMatch却取列表首项，按X/Y/角度排序时二者可能不是同一对象；BestPositionCorrection又使用BestMatch。',
cache:'进程静态字典按模板Id+ModelData SHA256复用句柄；首次临时文件ReadShapeModel，内容变化替换。RunTemplateMatches持同一静态锁覆盖准备及native匹配，所以匹配阶段跨实例串行。模板UI缩略图另有_previewCache，不是native缓存。',
release:'临时图像/区域和输出tuple有using/finally；同Id重训时旧HTuple.Dispose。所读缓存未见按模板删除/方案关闭驱逐、容量上限和统一退出清理。不能据tuple.Dispose推断原生模型已正确销毁，也不在缺native合同前断言泄漏。Run未显式取消，使用模型timeout。',
defect:'已确认“↓载入”仅日志TODO。新Id不断进入静态缓存而删除列表不驱逐，与注释“避免无界增长”不一致。BestMatch首项和BestScore最大值的语义在非分数排序时不一致，影响默认修正矩阵所指目标；是否产品意图需明确。',
vmKey:vm.key,vm:'原轮廓匹配资源合同含参数/输入输出/专业UI声明；它不证明原版使用HALCON或同一NMS/评分/超时/缓存。恢复项目代码和测试不能代替原算法体或同图基线。',
change:'先实现载入的校验/版本/身份冲突/原子采用，或隐藏未实现入口。分开“输出首项/主目标”和“最高分目标”，让BestScore/BestMatch/修正矩阵可追同一选择规则。缓存增加显式Owner、容量/退役与最后使用者协议，未证线程合同前保留保护锁，不直接以并发优化名义删锁或迁移VM算法。',
accept:'X/Y/角度排序下首项与最高分不同时，矩阵与标签一致；同Id重训/多Id创建删除/切方案无无界常驻；并发搜索与删除不释放在用模型；无模板/零匹配NG；不同MinContrast组映射、候选NMS/截断、超时/异常和大角度轮廓一致。与VM精度/速度另需原实现及同图基线。',
sources:[['ShapeMatchTool.cs','public ShapeMatchResults Run'],['ShapeMatchTool.cs','private static readonly Dictionary<string'],['ShapeMatchTool.cs','private static List<ShapeMatchResultItem> RunTemplateMatches'],['ShapeMatchTool.cs','HOperatorSet.FindScaledShapeModels'],['ShapeMatchTool.cs','private static List<ShapeMatchResultItem> SortResults'],['ShapeMatchResults.cs','public ShapeMatchResultItem? BestMatch'],['ShapeMatchResults.cs','public AffineMatrix BestPositionCorrection'],['Views/ShapeMatchToolDebugControl.xaml','TabItem Header="模板管理"'],['Views/ShapeMatchToolDebugControl.xaml.cs','功能待实现: 从文件载入模板'],['Views/ShapeMatchToolDebugControl.xaml.cs','Parameters?.Templates?.Remove(target)']].map(([p,t])=>ref(folder+p,t)),vmEvidence:{path:vmPath,sha256:crypto.createHash('sha256').update(text).digest('hex'),key:vm.key,declarations:vm.declarations,policy:'原始声明，只作候选合同对账'}};
fs.writeFileSync('artifacts/validation/shapematch-review.json',JSON.stringify({reviewed:1,tools:[r],scope:'补主调试与执行，不以模板子入口代替'},null,2));return r;};
