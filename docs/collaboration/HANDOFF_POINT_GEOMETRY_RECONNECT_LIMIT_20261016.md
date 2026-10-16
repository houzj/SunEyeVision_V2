# 点类测量规范和五次重连上限交接

- 基线提交：a444b21325eeb6b121cfe6bd059383b65eb172eb。
- 当前分支：feature/comm。
- 当前执行者：Codex，单写入，未提交推送，保留所有既有修改。
- 任务目标：核对本地算法批次，实施下一小批点类测量规范；同时完成用户中途追加的通讯五次失败上限，保留配置、绑定、输出和屏蔽，标定不动。
- 允许修改目录：SDK测量执行辅助、PointPointMeasure/PointLineMeasure/PointCircleMeasure、UI通讯应用服务及本地方案/协作文档。
- 已修改文件：新增PointMeasurementGeometry.cs；PointPointMeasureTool.cs及Results.cs、PointLineMeasureTool.cs、PointCircleMeasureTool.cs；CommunicationApplicationService.cs及Presentation.cs；本地HALCON方案、CURRENT_TASK.md、COMMUNICATION_CLOSEOUT_SCOPE_20261016.md和本交接。

## 已完成

第一批卡尺/屏蔽、第二批阈值/滤波和第三批形态学/Blob/ShapeMatch的已有实现及交接已核对，HALCON方案新增分批状态表，统一标明代码完成与人工验收未完成。此前原生最终模型读取记录是历史，当前卡尺仍取点后统一轮廓拟合，不恢复支持率门槛。

点类测量共享有限值与单点解析，非法枚举、空图形、多图形、NaN/Infinity和不可表示的几何量明确失败。多元素图形仍不向量化、不取首项替代；原坐标数组绑定转换路径保持不变。新增辅助类只服务本批，不修改通用Point2d/Line2d或标定调用。

点点仍无需引入HALCON依赖，采用普通距离及角度公式，Hypot避免中间平方溢出，连线文本中点避免直接坐标相加溢出；同点距离0和原有角度约定不变。点线保留HALCON距离算子与已有拟合，用单位方向计算垂足，沿用原长度退化边界但改为明确失败而不是返回起点，PerpendicularT仍为端点比例、不限制在线段内。点圆复用原生圆心距离计算最近点，圆心重合仍失败，修正后引导圆及最终几何有限性显式验证。点点/点圆判断按稳定Key选择测量值，显示名称改动不改变判断含义。

通讯最多连续失败5次自动重连，五次后不再排队；公共OnConnectionClosed、重试循环与finally补调度均受预算限制。错误仍保留，列表显示自动重连已停止及五次失败。初次连接、手动连接和主动取消不计入；连接恢复成功清零。显式手动连接、关闭再开启AutoReconnect、禁用再启用可开始新预算，切换方案/重启应用开始新运行期；窗口关闭重开不重置，不暗改AutoReconnect配置。

## 构建和发布

- UI首轮：1170警告、0错误，21.65秒；包含测量辅助SDK编译，通讯UI可部署。
- 全量Release：dotnet build SunEyeVision.slnx -c Release --no-restore -m:1 -clp:ErrorsOnly；1165警告、0错误，65.50秒。
- 最终独立UI：dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore -m:1 -clp:ErrorsOnly；1069警告、0错误，23.29秒。
- PointLineMeasure插件既有规则补刷：0警告、0错误，5.30秒；未修改发布规则或插件加载规则。
- SDK/HalAdapter/UI及三插件发布主DLL与build_temp中间DLL SHA256一致；点线目录残留的SDK/HalAdapter副本刷新后与宿主一致。相关diff检查通过，标定源码无diff。

## 未完成与人工验收

遵前序用户限制，未新增或运行测试、未启动Release/Visual Studio设计器、未连接设备，不能将构建成功当作运行效果证明。

- 测量：真实上游点和X[]/X[n]绑定、两个入口一致性、正常及同点、非法/非有限输入、拟合线退化、圆心重合、显示名称调整后的判定、原图取点及位置修正需人工确认。
- 通讯：连续五次失败后无第六次，窗口重开不恢复重试；成功和显式重新开启后重新计数；取消/切方案及迟到事件不复活旧预算，最后失败原因保留，均待人工确认。

## 已知风险与下一步

普通投影和距离数值运算次序调整可能带来浮点末位差异，不承诺逐位相同。计算结果不可表示时不以Infinity报成功。屏蔽、参数、结果字段和坐标约定未变，不降低阈值、不新增拟合算法、不重放通讯写入。

本批已收尾，后续依本地方案安排多模板联合搜索等保留专项，不将未实施的Triangle原生等价替换或任意卷积核编辑记作完成；标定保持独立。通讯除本次明确追加上限外不再扩展。
