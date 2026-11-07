# ImageFilter任意卷积核专项交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入，保留并存修改，未提交推送。
- 任务目标：兼容旧固定3×3核后提供任意矩形卷积核配置，遵循HALCON原生流程，保留绑定、输出及屏蔽，不动标定。
- 允许修改目录：tools/SunEyeVision.Tool.ImageFilter、src/SunEyeVision.HalAdapter及本地方案/协作文档。
- 已修改文件：新增CustomConvolutionKernel.cs；ImageFilterParameters.cs、ImageFilterTool.cs、Views/ImageFilterToolDebugControl.xaml；HalconLinearFilters.cs；本地HALCON方案、CURRENT_TASK.md及本交接。

## 兼容方案与实现

先更新本地方案再实施。旧Filter2D枚举值4、参数名/默认值、现有绑定及输出字段不变。新增UseCustomKernel默认false，旧方案缺字段时仍执行原Sharpen路径和[0,-1,0;-1,5,-1;0,-1,0]核，已有核宽高输出仍为3×3。不按原KernelWidth/Height值猜测启用自定义，不迁移用户方案或覆盖参数。

用户显式开启后使用CustomKernelText和CustomKernelDivisor，默认文本等于旧锐化核，除数1。文本按行编辑，换行/分号分行、空白/逗号分列；数字使用InvariantCulture小数点或科学计数法，逗号不作小数点。每边1至31，支持非方形、偶数及1×1；尺寸从矩阵推导，不复用其他模式宽高。内部空行、空逗号单元、行宽不同、非法/非有限系数、全零核和非正/非有限除数明确失败，不补齐、不截断、不回退默认。文本上限65536字符，不加载核文件、不访问用户提供的文件路径。

关闭自定义或切换滤波模式不会删除编辑文本，未启用时不解析自定义内容。新增系数文本和除数运行参数描述符，沿用参数快照和执行绑定通路，不改SDK；界面提供多行矩阵编辑和除数ConfigSetting，矩阵文本界面为手动编辑，不新增专用数组绑定控件。原有参数描述符保持不变。

## HALCON路径与跨模块边界

HalconLinearFilters新增ApplyKernel，只做尺寸/有限值验证并传递[行数,列数,除数,按行系数...]，所有像素计算仍调用ConvolImage。原Apply/Sharpen不传显式除数，仍使用原系数和，不改变Blob及局部阈值调用。自定义核使用用户显式正除数，不以系数和归一化，允许零和导数核配除数1。

ConvolImage原生为相关运算，不额外翻转系数；参考点由非零核位置的质心取整决定，不自行强制为矩形中心。界面及日志明确说明，非对称/偶数核的相位不保证与其他库默认anchor相同。[HALCON官方算子依据](https://www.mvtec.com/doc/halcon/2405/en/convol_image.html)。

保留real工作类型及恢复原灰度类型，byte负响应仍会截断到0，需要有符号输出应显式提供有符号或real上游输入；不为自定义核默默改变输出类型。已有int4精度保护和不支持边界类型的明确失败保持。UI除数编辑范围0至1E12、六位小数显示，0会在参数验证时拒绝；运行期绑定仍按有限正数校验。

完整图像上下文完成邻域计算后，仍按检测区减屏蔽区限制输出domain和统计，不涂黑屏蔽像素、不改变邻域读取语义。OutputImage、GrayImage、FilterTypeUsed=Filter2D、KernelWidthUsed/KernelHeightUsed、统计和检测/屏蔽几何输出字段不变，自定义启用时核尺寸字段反映实际矩阵。位置修正及本地/订阅/拼装区域处理未改；标定、通信、工作流和SDK源码未修改。

## 构建和发布

- ImageFilter插件首轮Release：16警告、0错误，6.60秒。
- 全量Release --no-restore -m:1 -clp:ErrorsOnly：1165警告、0错误，86.82秒。
- 独立UI Release：1069警告、0错误，16.65秒。
- PointLineMeasure目录内旧共享依赖按既有插件Release构建刷新：0警告、0错误，2.89秒，不改发布或加载规则。
- ImageFilter/UI/SDK/HalAdapter发布主DLL及共享副本与编译中间DLL进行SHA256核对，相关diff检查通过；标定源码无diff。

## 待人工验收与下一步

遵前序用户限制，未运行测试、Release程序或Visual Studio设计器。构建完成不代表实际图像或交互验收通过。

- 旧方案没有新字段时仍为固定3×3，已有绑定不丢失；关闭自定义时无效自定义文本不阻断原路径。
- 开启默认矩阵/除数1、1×1核、非方/偶数/非对称核、零和核、均值核及小数/科学计数；核方向和参考点对照原生规则。
- 非矩形、空单元、非法数、NaN/Infinity、超尺寸、全零、无效除数均明确失败，切换模式、保存重开和两入口执行使用同一参数快照。
- byte/int2/uint2/real及既有int4保护、边界四种有效映射、无等价映射的错误提示；检测与屏蔽订阅、位置修正和输出domain保持。

本批任意卷积核代码已实现。Triangle继续保留已审计的兼容实现；标定仍为独立未实施专项，不在本轮推进。后续以这些人工验收项目为准，不因代码构建通过声称数值逐像素等价。
