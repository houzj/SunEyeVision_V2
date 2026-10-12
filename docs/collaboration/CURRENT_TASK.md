# 当前协作任务

## 2026-10-12 变量选择窗口item主题修复

- 用户反馈复用变量选择窗口深色主题数据行白底、浅色字看不清。Codex单写入，基线b2beb4017d6607ee44d6d5ba1aeb0376eb60572a；保留既有未提交工作，不改取数、SDK、绑定、调度、主项目或标定。
- 根因是DataGrid.Resources内仅设置IsEnabled=True的隐式DataGridRow样式覆盖窗口主题行样式。移除覆盖，行／单元格／表头改命名样式并显式引用；透明普通单元格及主题选中状态，普通／悬停／选中／失焦选中随主题变化。按钮动态引用共用样式，仍复用原窗口，不新增变量选择器。
- 新增实际WPF窗口回归，深→浅→深同窗口动态切换、行／单元格、悬停、失焦选中、解除选中、分组搜索及渲染均通过；最终相关串行64项通过。两主题预览已目视检查，证据artifacts/validation/variable-select-theme-20261012；VS设计器、高DPI及用户最终验收待完成。
- 用户确认已关闭后核验无运行进程，常用通信Release已更新：全量0错误1147警告，UI0错误961警告；实际toolbox-probe退出0，9类23工具，三通信工具正常加载，产品2.0.0.8／SDK2.1不变。不连接或写用户设备、不提交推送。
- 文档维护技能同步接收方案与本记录，交接HANDOFF_VARIABLE_SELECT_THEME_20261012.md。此次64项不代表全套回归，也不重复计入上一轮353项。

## 2026-10-12 接收当前值与变量多行优化实施

- 用户授权实施最新方案并强调复用已有变量选择窗口。Codex单写入，基线b2beb4017d6607ee44d6d5ba1aeb0376eb60572a，分支feature/comm；保留全部既有修改，未改主项目／标定、提交或推送。
- 当前值服务已替代接收快照：Modbus／OPC UA执行期主动读，TCP等当前连接完整报文，普通变量当前集合；删除接收年龄参数及触发报文传递。驱动读写锁、取消／超时、会话／配置变化校验复用现有能力，读取期间变化拒绝发布。
- ReceiveData实现IAsyncToolPlugin，正式根／子工作流与单步注入服务；异步单步防重入，通信服务延后至输入就绪后取得，不影响输入等待期间的执行租约。其他算法参数快照不变。
- 接收界面460×600，设备和地址／点分层，变量名称／数据多行、＋紧跟Grid、稳定字段及类型自动声明；直接通过宿主回调复用GlobalVariableSelectDialog分组／搜索窗口，插件不引用UI宿主。无常驻说明、年龄或手填类型控件。
- 生产者变量按本次成功节点结果或方案运行变量视图读取，不用设计初值；接收订阅纳入局部和跨流程依赖图，作用域防止同节点ID跨流程混用。多行原子发布，失败不沿用上次输出。
- 最终UI串行291项及SDK／流程62项通过，共353项。TCP新报文、Modbus关闭轮询两次读得不同值、OPC UA停订阅后主动读、变量执行期变化、生产者成功／缺失／无连线依赖／跨作用域均覆盖。广范围并行WPF回归曾失败，单独及最终串行通过，不宣称并行稳定或全套通过。
- 常用通信Release已更新：最终全量0错误1150警告，UI0错误1046警告。实际toolbox-probe退出0，9类23工具、通信接收／解析／组装正常加载；产品2.0.0.8／SDK2.1不变。深浅主题渲染已检查；未连接／写入用户设备，VS设计器、高DPI及用户验收待完成。
- 文档维护技能同步六份方案和实施计划。发送、队列、客户端级身份、文本编码与部分输入宽度细节仍待完成。交接HANDOFF_RECEIVE_CURRENT_VALUE_IMPLEMENTATION_20261012.md。

## 2026-10-12 接收执行期当前值方案更新

- 用户要求保存最终优化方案，并完全删除不再采用的接收策略历史描述。Codex使用文档维护技能，仅更新本地docs，保留既有全部代码改动；不构建、部署、启动程序、连接设备、提交或推送。
- 当前定稿：触发只决定启动，接收节点执行时取当前来源。Modbus／OPC UA复用连接主动读一次；变量通过无UI服务一次读取当前集合并原子输出；TCP等读当前连接最近完整报文，不清空、不隐式请求或等待新报文。值可不同于触发条件，工程负责时序。
- 设备与地址／点分开选择，设备模式一项；变量模式分组／搜索选择、多行名称／数据映射，＋紧跟Grid。输出类型随来源定义，不重复手填，说明进入Tip／帮助，队列未实现不显示可用入口。
- 读取完成输出固定，解析／组装消费本次成功结果；失败清空，不沿用上次结果。主动读取与轮询串行协调，保留取消／超时、会话／配置／方案失效；其他算法参数启动快照不改。
- 六份方案、实施计划及相关历史交接已同步清理。以上为项目取数契约，不声称VM内部读取如此；VM官方仅确认来源布局和PLC轮询／数据同步。新契约尚未代码实施，既有344项专项与Release记录不能替代新验收。
- 下一批优先当前值服务改造、分层设备与变量多行UI、联合回归及常用Release部署；详情见HANDOFF_RECEIVE_CURRENT_VALUE_DESIGN_20261012.md。

## 2026-10-12 接收数据首版与协议联合链实施

- 用户授权按照最新计划实施。Codex单写入，基线b2beb4017d6607ee44d6d5ba1aeb0376eb60572a，分支feature/comm；未修改主项目／标定、提交或推送。
- 已新增无UI接收服务、真实ReceiveData插件和设备／单项变量基础；共享Binary和原生类型输出、稳定字段键、正式工作流和单步路径已有基础。当前值取数新规则尚须改造，已有测试不代表新契约验收。
- 解析String／Binary、组装String／Integer／Double联合筛选已接入；修复候选VariableType复制遗漏及匿名协议行空参数名导致绑定串行的问题，其他节点单类型筛选不变。
- 本机TCP及Modbus模拟对端通过正式接收→解析→组装引擎，模块结果分别RESULT,ABC,2与RESULT,92,AD。最终UI专项290项、SDK专项54项通过，共344项；不是全套测试或用户硬件验收。
- 常用通信Release已更新。最终全量Release0错误1147警告，UI Release0错误766警告；实际无设备toolbox-probe退出0，9类23工具，通信按接收／解析／组装排列，产品2.0.0.8／SDK契约2.1不变。深浅主题运行时渲染已检查；VS设计器、高DPI与用户验收待完成。
- 本地六份方案／计划已同步；后续按最新执行期当前值方案改造。队列、多项变量映射、服务端客户端级身份、文本编码条件UI与宽度细节、发送插件及完整收发未完成。详情见HANDOFF_RECEIVE_DATA_IMPLEMENTATION_20261012.md。

## 2026-10-12 接收插件优先与组装来源方案同步

- 用户要求整合上下文更新本地方案和实施计划。Codex单文档写入，使用文档维护技能，仅修改docs，不修改代码、不构建、启动、部署、操作设备或提交推送；保留全部既有工作。
- 最新优先级取代前条“先做R4收窄再做R3”：下一批优先无UI接收服务和真实ReceiveData插件，同批补齐解析String／Binary与组装String／Integer／Double候选过滤及实际校验，先验证设备→接收→解析→组装→模块结果，宽度等UI细节后置。
- 组装列表每行订阅本次上游成功结果，不限紧邻节点，也可取变量执行快照或常量；不是固定接收全局触发out或事件out。Binary不能直接自动文本组装，先解析为标量。组装固定String与UTF-8 Binary输出，保留身份和失败清空。
- 主基线、接收／解析／组装方案、结果约定及实施计划已同步；保留业务分帧与原生类型边界。接收取数按最新当前值方案实施，历史277项与Release记录不算新规则验收。
- 第一批联合链终点是模块结果，不代表实际设备写回；后续发送、队列及完整收发计划仍保留，不加入脚本、视觉控制器、相机IO，不改标定或主项目。交接见HANDOFF_COMM_RECEIVE_PRIORITY_20261012.md。

## 2026-10-12 接收载荷与协议解析最终方案保存

- 用户要求保存／更新完整上下文的最终优化方案。本轮Codex使用文档维护技能，仅更新本地docs，保留全部既有代码和未提交修改，不构建、部署、启动程序、写设备或提交推送。
- 最终入口仍为docs/01-方案设计/2026/10/通信接收与协议解析优化基线-20261012.md，同步接收、解析、模块结果约定和实施计划，不另建平行方案。
- 定稿TCP／UDP／串口业务报文及Modbus数据区接收out为Binary，不自动追加String；原生点／变量／队列保留实际类型。解析内容同时接受String／Binary，文本Binary按编码解码，字节Binary直接读取，String字节输入明确HEX／字符文本格式；联合候选筛选和实际类型校验不放宽其他算法参数。
- 下一批R4补齐为绑定兼容类型集合、文本Binary的UTF-8／ASCII条件编码设置、约260宽左对齐解析内容与长路径Tip，再推进R3接收服务和插件。当前文本Binary固定UTF-8、输入无DataType筛选且铺满宽度；这些新目标未实施。重排序号修复已部署，不重复计算此前277项验收。
- 保留事件out与节点out的独立身份、同次样本快照、无数据失败及失败清空；服务路径与队列待完成。不加入脚本、视觉控制器、相机IO，不改主项目或标定。交接见HANDOFF_COMM_PROTOCOL_INPUT_DESIGN_20261012.md。

## 2026-10-12 协议列表重排序号修复

- 用户授权修复上下移动item后序号出现2147483647。Codex单写入，保留全部既有未提交工作；跨模块影响仅SDK协议编辑器局部WPF行为及两协议插件视图，不改算法、工作流或契约版本。
- 解析／组装共享样式移除AlternationCount最大整数，序号和上下移边界均改用ProtocolRowPosition的实际Items位置。重排结束后刷新现有行，LoadingRow处理滚动生成／复用的行，不向参数加入持久化序号。
- 新增7项回归：文本／字节／组装，4项及30项列表反复按按钮上下移、跨首末移动、增删和实际滚动；逐行检查显示序号和首末按钮，另验证文本重排改变片段映射、字节重排保持区间结果及FieldId。最终相关UI277项全部通过。
- 用户确认关闭后，核验RunningCount=0，完成常用通信Release部署。标准全量Release0错误1148警告，标准UI Release0错误1060警告；实际常用Release无硬件toolbox-probe退出0，加载两协议插件，产品2.0.0.8／SDK2.1不变。新增证据artifacts/validation/protocol-row-position-20261012/standard-release-probe.json；此前隔离验证保留。git diff --check通过；VS设计器和用户人工重排验收待完成。
- 使用文档维护技能记录修复与验证；不改主项目、Debug、设备、不提交推送。详细交接见HANDOFF_PROTOCOL_ROW_POSITION_20261012.md。

## 2026-10-12 通信解析优化实施与取数规则确认

- Codex单写入，基线a0f92a8732f31a2ffe20e10fcc6feb7c792a093f，feature/comm。保留全部既有未提交工作，不改标定、主项目、产品版本或SDK契约。
- 完成R1无UI的ProtocolValueCodec，管理器与解析工具共用HEX、int／float文本转换、字节闭区间、顺序、编码和整数解释；Workflow HEX入口复用该核心。
- 完成R2首批适配：原始文本可转int／float；原始字节和Modbus字段支持int／float／string／Binary，组装Value保留实际对象，HEX仅展示。OPC UA原生类型校验保留。未新增Binary触发／回复或事件参数到工作流接线。
- 完成R4：解析520×600，高级设置默认收起；固定0基闭区间，移除Signed列，按选中字段配置Auto／Signed／Unsigned。格式和编码按声明类型条件显示，重排与替换列表不串字段。深浅主题实际渲染及STA布局回归通过；VS设计器、高DPI和用户人工验收仍待完成。
- 本轮最终UI范围270项、SDK专项68项通过。标准全量Release0错误1148警告；标准UI Release0错误947警告。常用Release已更新，实际无硬件toolbox-probe退出0，9类22工具，通信仍为解析／组装两项；启动加载不代表整链设备验收。git diff --check通过。
- 无设备连接／写入、Debug部署、主项目同步、提交或推送。完整SDK前阶段两项失败未重跑或修复，不宣称全套通过。详细交接见HANDOFF_COMM_PROTOCOL_OPTIMIZATION_20261012.md。

## 2026-10-12 通信接收与协议解析官方复核方案保存

- 用户要求结合VM官方帮助给出优化方案并保存本地。Codex本轮仅文档，使用文档维护技能，保留既有代码及未提交工作，不构建、不运行程序、不写设备。
- 最新入口：docs/01-方案设计/2026/10/通信接收与协议解析优化基线-20261012.md。官方接收数据、协议解析、接收事件、全局触发和通信示例明确两条路径：事件out用于触发参数；设备模式接收工具直接选设备／地址，不要求事件out作为必经桥。
- 保存完整字节、文本、高级设置ASCII图与交互；主界面收起格式、编码和符号，固定0基，宽度按可读性收敛，不承诺500。默认符号规则拟沿用管理器口径，属于项目约定而非VM事实；尚未实施。
- 同步接收／解析方案、模块结果约定和实施计划，优先按R1共用解析核心、R2管理器能力修正、R3接收载荷交付、R4高级设置UI推进。原生OPC UA类型检查不放宽，取样时效和队列策略待确认。已发布两协议编辑器与新目标明确分层。
- 交接：HANDOFF_COMM_PROTOCOL_OFFICIAL_REVIEW_20261012.md。本轮仅文档回读及链接／格式检查，前轮34项测试和Release通过不计为新优化验收。

## 2026-10-12 协议编辑器说明收起与宽度收敛

- 用户要求界面不常驻说明，宽度尽量收敛但可读性优先，并反馈400宽解析页文字拥挤。Codex单写入；用户确认关闭Release后完成部署。
- 最终组装400×550、解析580×600 WPF逻辑单位。组装功能说明和结果查看说明移入标题Tip；解析字节说明移入字节设置Tip。解析参数左列120宽、控件同一起点、独立行和10行间距，配置／列表分隔；默认短列表完整显示全部列，窄化时滚动而非挤压。
- 34项布局和协议工具回归通过，深浅色实际渲染图已检查，含无常驻说明、标签对齐、参数间距、默认表格完整与行操作。全量Release0错误1145警告，标准UI Release0错误1045警告；实际发布探针退出0，22工具含两协议工具。
- 两份本地方案和HANDOFF_PROTOCOL_EDITOR_LAYOUT_20261012.md已更新。只更新通信Release，未同步主项目、修改算法、连接／写设备或提交推送；保留先前未提交工作。VS设计器及用户最终界面验收待完成。

## 2026-10-12 协议编辑器窗口与列表主题优化

- 用户授权组装窗口及列表优化，并追加协议解析同样处理；已关闭Release。Codex单写入，保留既有未提交修改。
- 两编辑器收窄为组装520×440、解析620×580 WPF逻辑单位；列表按内容高度，长列表内部滚动；＋紧跟表格，行内↑↓×，重排保留身份和绑定。文本／字节配置、解析与组装算法、模块结果位置不变。
- 两插件共用SDK局部主题与布局转换器，修复ParamBinding调用方样式被覆盖及模板重建误清空绑定问题；不修改工作流调度、全局表格、标定或设备。
- UI相关265项、SDK绑定与主题26项通过；实际浅深色渲染检查完成。全量Release0错误1148警告，UI Release0错误960警告；发布程序toolbox-probe退出0、9类22工具包含两协议工具。
- 已更新两份本地方案和通信Release，Debug及主项目未更新。VS设计器、主窗口用户验收仍待完成；无设备连接或写入。交接见HANDOFF_PROTOCOL_EDITOR_LAYOUT_20261012.md。

## 2026-10-12 工具箱分类排序与图标优化

- 用户确认9类归属及使用流程顺序，授权实施；指定以后只在本通信项目Release测试。唯一入口D:/MyWork/SunEyeVision_V2-comm/Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe，不混用主项目Release或dev/Debug。
- Codex单写入，更新22个工具的Category／Order／Icon展示特性；SDK共用分类目录和矢量控件，工具箱、画布及标准配置标题统一图形。移除分类红色数量点，数量改为悬停提示及展开文字；保留节点状态色和子程序参数设置点击，不改算法、工具ID或参数。
- 新增App --toolbox-probe在设备初始化前加载真实发布插件并输出工具箱状态；实际Release9类22工具顺序正确、退出0，产品2.0.0.8、SDK2.1、Dark。浅深色SDK控件渲染图已检查，证据artifacts/validation/toolbox-presentation-20261012。
- 专项32通过（新工具箱5＋协议工具27）、扩展UI258通过、SDK专项76通过。最终全量Release0错误1145警告，标准UI Release0错误947警告；git diff --check通过。VS设计器和主窗口人工验收仍待完成。
- Release已更新，Debug未更新；未连接／写用户设备、未同步主项目或提交推送。接收／发送仍未实施，未提供不可用占位。方案及交接见工具箱分类排序与图标基线-20261012.md、HANDOFF_TOOLBOX_PRESENTATION_20261012.md。

## 2026-10-10 协议解析与协议组装首版实施

- 用户要求开始下一步，Codex继续作为唯一代码写入者。本轮新增ProtocolParse／ProtocolAssemble真实插件，接入现有工具注册、参数持久化、配置编辑器、ToolExecutor、固定／动态输出和共用主界面数据分析。只有配置窗口，不新增运行结果预览；模块结果和下游绑定消费同次CommunicationToolResults。
- 解析支持文本／字节，按配置输出、缺项或转换／区间失败原子失败；字节编号0／1、HEX／字符输入、UTF-8／ASCII、整数符号均显式配置。组装按列表顺序和分隔符输出String及UTF-8 BinaryPayload，不发送设备，不猜常量类型，不实现脚本。共用分隔符目录从UI下沉SDK，UI仍保留既有API。
- 修复Core ParameterTypeRegistry共享JsonPolymorphismOptions被多个JsonTypeInfo重复占用导致的保存异常，每份类型元数据使用注册目录快照。影响是插件／工作流参数保存，不涉及相机和标定算法。测试参数注册移到测试程序集初始化，模拟宿主先注册后使用序列化选项的顺序。
- 新插件专项27项通过；通信／OPC UA／相机宽范围253项最终串行和常规并行均通过；SDK专项76项通过。最终全量Release0错误1145警告，标准UI Release0错误1009警告。只读核验发布目录21个工具均声明2.1，实际Release无硬件探针退出0、产品2.0.0.8、Dark，证据artifacts/validation/protocol-tools-20261010/release-probe.json。
- 接收、发送插件及无UI通信服务注入／队列仍未完成。VS设计器和真实主窗口人工验收待完成；WPF编辑器已有STA加载／布局测试，启动探针不等于端到端硬件或人工UI验收。未更新dev/Debug、未连用户设备、未同步主项目、未提交推送。SDK全套前轮两项失败未在本轮修复或重跑。详细范围与下一步见HANDOFF_PROTOCOL_TOOLS_20261010.md。

## 2026-10-10 开发期SDK统一契约修订

- 用户明确尚未正式发布，无需兼容旧插件。本轮移除RequiredToolContractMinor过渡选择，全部插件构建统一标记2.1，运行时主次版本完全一致才接受，2.0不再放行；保留版本检查用于阻止旧产物混用，不增加迁移或回退逻辑。产品版本不改。
- 同步契约及Binary测试、实施计划、统一约定。之前“旧2.0兼容”记录已被本条决策替代；不影响原始载荷和结果模型能力。验证和基础交接记录随后更新。

- 修订验收：76项专项通过，全量Release0错误905警告，标准UI Release0错误847警告；只读核验Release输出的19个工具DLL均声明2.1。最终实际Release无硬件探针退出0、契约2.1、产品2.0.0.8、Dark；证据artifacts/validation/comm-sdk-contract-20261010。git diff --check通过，未更新Debug、未连设备或同步主项目。SDK全套前述两项未修复失败不隐瞒，本轮未重跑全套。

## 2026-10-10 通信工具链共享基础实施

- 用户再次授权按计划实施，Codex为唯一写入者，保留此前方案及P0测试。完成不可变BinaryPayload载荷、Binary独立词目、变量保存／重载及HEX只读显示，不恢复Byte标量，不将byte[]直接混入数值tuple。
- 新增CommunicationOutputField稳定字段键、CommunicationToolResults生产结果模型、原子业务输出、失败有效性及只读分析行；共用CommunicationDataAnalysisControl用于主界面，不在配置窗口展示运行值。
- SDK兼容契约升至2.1（产品版本未改）；旧插件默认仍声明2.0，新能力插件可通过RequiredToolContractMinor=1明确要求2.1。共享影响覆盖SDK、Workflow变量转换器、UI变量显示及构建元数据，未修改标定算法、工作流调度或主程序工作区。
- 这是P0载荷／结果基础与P1分析基础的增量，不表示P0全部完成或四工具已注册。通信服务注入、四插件编辑器／运行及P2至P6仍未完成。验证及详细交接见HANDOFF_COMM_TOOLCHAIN_FOUNDATION_20261010.md。

- 最终专项76项通过，通讯UI／OPC UA／相机及字节显示串行206项通过。全量Release0错误1005警告，标准UI Release0错误947警告；实际Release无硬件探针退出0、契约2.1、Dark。SDK全套473项中471通过、2失败（旧Int32快照测试、标定验证控件设计字典），相关实现未改且未在干净基线重跑，不宣称全套通过。不改标定、不更新dev/Debug、不连用户设备。手工UI验收待完成。

## 2026-10-10 按工具配置输出口径同步

- 用户要求更新本地文档，已同步四工具方案、结果分析共同约定和实施计划。统一按配置产生结果，不猜类型或新增字段；文本解析按配置项消费，多余片段不输出、缺项或类型转换失败及区间越界失败。模块结果、分析和绑定来自同一次执行。
- 原字段严格一致建议已删除；计划改为用户已授权实施、P0契约原型已启动，7项验证与UI Release结果保留，不冒充四工具已完成。框架原始字节适配由实施者负责，不能恢复Byte标量或用HEX字符串绕过共享契约。
- 索引、编码、样本时效、队列交付和消费等仍为独立事项，没有自动确认。Codex单写入，本轮仅文档，不修改P0测试或生产代码、不构建、不操作设备、不提交推送。交接补充见HANDOFF_COMM_TOOLCHAIN_PLAN_20261010.md。

## 2026-10-10 通信工具链P0启动

- 用户已授权按计划实施，Codex为本工作区唯一写入者，保留此前未提交方案。先做P0输出契约验证，不在待定字节类型与阶段规则确认前修改共享SDK。
- 核查统一SDK的Integer／Double数组规范及Byte已删除约束，原Byte[]设计尚不可直接绑定；已请求确认独立字节序列类型、解析组装规则和队列交付范围。不恢复旧Byte标量，不把HEX字符串冒充原字节。
- 开始新增P0契约原型回归，验证稳定字段键、显示改名、失败空值、标量／列表及现有类型限制。生产插件、通信服务注入和P1仍未接通。

- P0首轮验证：CommunicationToolchainContractProbeTests共7项通过，实际覆盖稳定键改名／排序、空值不复用、标量／列表和Integer／Double词汇归并；Byte[]尚未注册且不能静默动态注册。标准UI Release构建0错误、847警告，git diff --check通过。本轮只新增契约测试，未修改SDK生产代码或注册通信插件。
- 用户要求直白说明问题，并强调数据类型按通信设备及解析输出决定；不将该回复视为接受全部解析规则或新增SDK词目的明确确认。后续由实现者提出能力驱动适配，原始载荷保留字节、OPC UA保持原生类型，避免让用户选择框架细节。队列范围和解析字段数量等行为仍未确认。

## 2026-10-10 通信工具链实施计划保存

- 按用户请求制定并保存docs/01-方案设计/2026/10/通信工具链实施计划-20261010.md，依据四工具设计和结果分析共同约定，分P0契约原型、P1骨架结果绑定、P2纯解析组装、P3接收、P4发送、P5队列、P6全链路发布验收。
- 明确各阶段任务、交付、决策门槛、共享SDK／执行层影响评估、变量生产者、动态输出、无UI运行时接入及发送禁缓存／禁默认重试。队列保留整体计划，首批范围待确认；脚本、视觉控制器、相机IO继续排除。
- 基线a0f92a8732f31a2ffe20e10fcc6feb7c792a093f，Codex单写入。本轮仅计划文档，不开始编码、不改SDK／发布规则、不构建、不操作设备、不提交推送；实施需要另获授权。交接见HANDOFF_COMM_TOOLCHAIN_PLAN_20261010.md。

## 2026-10-10 四工具模块结果与数据分析契约补齐

- 按用户要求同步四份通信工具方案，新增通信工具链模块结果与数据分析统一约定-20261010.md：各工具业务字段、原生类型、来源／解析／组装／提交分析明细、同一ToolResults发布关系及主界面数据分析ASCII。配置和调试窗口仍不展示结果，不新建独立结果源。
- 核对现有SDK：ToolResults、IToolControlDataAnalysis和宿主结果接收接口；既有ExecutionStatus含PartialSuccess，发送方案改为复用而非新建状态。动态输出、字节／列表绑定等具体适配仍需实施评估，不能宣称已接通。
- 本轮仅文档，保留已有未提交方案，无代码、SDK、构建、设备、提交或推送操作；四方案均链接共同约定。交接补充在HANDOFF_COMM_TOOL_RESULTS_20261010.md。

## 2026-10-10 协议组装保存及四工具模块结果统一修订

- 用户补充模块结果截图并明确所有工具运行结果不在配置／调试窗口查看。保存 docs/01-方案设计/2026/10/协议组装工具方案与沟通基线-20261010.md，文本组装配置无结果框，新增宿主三列模块结果ASCII及变量生产者绑定说明，脚本组装排除。
- 同步修订接收、发送、协议解析三份基线：移除解析本次结果／状态列和发送独立结果页设计，新增各工具模块结果图。发送截图的结果显示页签只可能为显示设置，不作为运行值区；执行结果位置从待定改为已确认。配置校验可保留，单步结果使用宿主通道，不注入其他执行上下文；变量绑定不因查看结果自动赋值。
- 保留其他未定数值格式、编码、转义和字节输出规则，不把用户确认结果位置扩大为全部设计确认。基线 a0f92a8732f31a2ffe20e10fcc6feb7c792a093f，Codex单写入，仅文档，无代码、构建、设备操作、提交或推送。交接见 HANDOFF_COMM_TOOL_RESULTS_20261010.md。

## 2026-10-10 协议解析工具沟通基线保存

- 按用户要求保存 docs/01-方案设计/2026/10/协议解析工具方案与沟通基线-20261010.md，包含文本／字节完整 ASCII 界面、输入格式、类型转换、闭区间、字节顺序、ASCII 与二进制示例、原子发布和执行／确定交互，并链接接收与发送方案。
- 脚本解析明确排除，不能用全局脚本替代。VM 官方事实与索引基准、字段数量、类型宽度／符号、字符串编码及结果展示等项目建议分开；保存不等于确认全部建议或授权实施。
- 基线 a0f92a8732f31a2ffe20e10fcc6feb7c792a093f，Codex 单写入；本轮仅文档，保留此前接收／发送方案及未提交记录，不构建、不修改共享 SDK 或代码、不连接设备、不提交推送。交接见 HANDOFF_PROTOCOL_PARSE_DESIGN_20261010.md。

## 2026-10-10 发送数据工具沟通基线保存

- 按用户要求保存 docs/01-方案设计/2026/10/发送数据工具方案与沟通基线-20261010.md，作为发送工具后续优化与讨论基线。包含公共窗口、队列、普通设备、Modbus、OPC UA、变量、发送事件及结果页的完整 ASCII 图，交互、忽略错误单元、发送计划、副作用和与接收／组装工具衔接说明。
- 视觉控制器与相机 IO 排除。VM 事实、项目建议、未接通能力与待确认项明确区分；保存不代表确认全部建议或授权实现。特别记录 UI 发送服务不得被插件直接依赖、变量唯一生产者、队列完整性、最终报文与字段格式化区别。
- 基线 a0f92a8732f31a2ffe20e10fcc6feb7c792a093f，Codex 单写入。本轮仅文档，保留上一轮接收方案及未提交记录，不改代码或共享 SDK，不构建、不发用户设备、不提交推送。交接见 HANDOFF_SEND_DATA_DESIGN_20261010.md。

## 2026-10-10 接收数据工具沟通基线保存

- 通信项目后续开发通信工具链，主程序项目开发标定工具链；本轮通信工具范围为接收数据、发送数据、协议解析、协议组装，相机 IO 暂不纳入。
- 按用户要求保存 docs/01-方案设计/2026/10/接收数据工具方案与沟通基线-20261010.md，包含数据队列、Modbus、TCP/UDP/串口、OPC UA 和变量管理五份完整 ASCII 图及交互、输出、快照、保存、失败和实施边界。VM 官方事实与项目建议分开，保存不等于确认全部建议或授权编码。
- 基线 a0f92a8732f31a2ffe20e10fcc6feb7c792a093f，Codex 单写入；本轮仅文档，不改代码、SDK、执行层或主程序工作区，不构建、不连接用户设备。队列服务、手动读取时机、多客户端筛选及其他执行细节继续待确认。交接见 HANDOFF_RECEIVE_DATA_DESIGN_20261010.md。

## 2026-10-10 主程序与通讯项目统一开发基线

- 用户要求将主程序的通讯优化同步至通讯管理项目。Codex执行；同步前main=a7c0d8a9ba11fa75c4fd67313bbdde8b51a4f2f6，feature/comm=1173baedc23eb879811f53242282c6072b10101a。通讯工作区干净、没有独立提交，采用fast-forward，不复制文件、不重写历史。
- 通讯项目D:/MyWork/SunEyeVision_V2-comm已取得主线全部129项受影响的已跟踪文件，包含最新触发求值、上传快照、OPC UA监控、心跳类型锁定／失败提示／删除样式及其主程序依赖。新旧全局规则边界不变，旧String规则须重建。
- 已保留恢复分支codex/pre-sync-main-20261010和codex/pre-sync-comm-20261010；主目录5个用户未跟踪文件保持原样，不进入提交。feature/script-tool及其它工作区未同步。
- 通讯工作区全量Release1261警告0错误，独立UI Release1060警告0错误；通讯195、心跳11、触发UI1、发送UI2共209项回归全部通过。实际通讯Release EXE无硬件探针退出0、2.0.0.8、Dark；设计时输出属性查询通过。未执行真实设备业务，可见设计器、明暗主题及高DPI人工验收待确认。
- 本轮创建有明确说明的同步交接提交，再将feature/comm快进到同一提交；共同基线恢复引用codex/baseline-sync-20261010指向该提交。双方保留main与feature/comm各自继续开发，禁止在同一工作目录同时写入。
- 详见HANDOFF_BASELINE_SYNC_20261010.md；证据在通讯工作区artifacts/validation/baseline-sync-20261010。本轮未推送、打包、增号、强制结束进程或修改用户方案数据，构建输出分别留在各自工作区。

## 2026-10-10 心跳类型锁定与失败提示及删除样式完成

- 用户授权实施，Codex 当前唯一写入者；基线 main 1173baedc23eb879811f53242282c6072b10101a，保留其它未提交改动。
- 修正心跳删除按钮24×24继承12,6内边距的问题，设置Padding=0、居中及删除提示，复用DangerButtonStyle；未修改共享主题或SDK。
- OPC UA心跳类型采用已获取的节点标量类型，以只读文本显示，不允许手动改成其它类型；未确认或发送格式暂不支持的类型显示未知类型，给出原因并阻止启用。节点类型变化同步配置，普通值更新不重建树、不重启任务。Modbus仍显示编码类型选择，普通文本设备不显示类型选择。
- 心跳失败或等待原因在表格下方按完整目标路径显示，保留原tooltip和应用级Warning日志去重；切换目标清除旧目标状态。未新增模态告警、每周期刷日志或隐式强制转换，不改后台写入保护。
- Release通讯195项、心跳11项、触发UI1项、发送UI2项，共209项通过。最终独立UI Release1060警告0错误；设计时输出查询通过；实际EXE无硬件探针退出0、2.0.0.8、Dark。真实设备、可见VS设计器、明暗主题及高DPI待人工验收。
- Release已更新，未强制结束进程、打包、增号、提交推送或同步工作区；证据 artifacts/validation/heartbeat-feedback-20261010，详见 HANDOFF_HEARTBEAT_FEEDBACK_20261010.md。

## 2026-10-10 全局匹配与心跳最终优化实施完成

- 用户授权开始实施，Codex 为当前唯一代码写入者；基线 main 1173baedc23eb879811f53242282c6072b10101a。保留其它未提交改动，未跨 AI 交接、提交推送或同步其它工作区。
- 全局规则仅识别新 Source=GlobalData，增加不等于；先质量和类型适配成功后再比较，不把失败结果取反。移除 SourceDeviceId、UploadScope、旧匹配名称转换和全部迁移 UI／分支。旧 String 规则不加载执行，通讯页给出重新创建说明；不自动转换，不擅自改写用户方案文件，其它事件兼容保持原样。
- 心跳恢复单数据／多数据，发送内容为单行一个 TextBox 或两个等宽 TextBox 加短横；移除值 A/B 标签和未使用的 HeartbeatValueInput.xaml/.cs。内容仍按发送类型解析校验，启用时无效值拦截；Single／Alternate 后台定时和交替逻辑保持不变。
- 心跳父节点整行和28×32箭头区单击展开／收起，叶子单击提交关闭。透明箭头背景使周围空白可命中，统一预览事件避免箭头与默认双击重复处理；右键不改目标，键盘导航不提交，Enter确认、Esc取消。重开定位已选目标及展开父节点，不产生配置提交。
- 最终 Release 通讯195项、触发UI1项、心跳10项、发送UI2项，共208项通过。全量Release851警告0错误；独立UI Release1024警告0错误。设计时输出属性查询通过，实际EXE无硬件启动探针退出0、2.0.0.8、Dark。可见VS设计器、亮暗主题、高DPI、实际鼠标边界和真实设备待人工验收。
- Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe 已更新；未强制结束进程、打包或增号。证据 artifacts/validation/communication-final-refinement-20261010，详见 HANDOFF_COMMUNICATION_FINAL_REFINEMENT_20261010.md。下方待实施和旧迁移记录为历史，不作为现行要求。

## 2026-10-10 全局匹配与心跳最终优化方案更新

- 用户要求整合上下文并更新本地方案；本次 Codex 仅修改文档，未改代码、构建、打包、增号、提交推送或同步。基线 main 1173baedc23eb879811f53242282c6072b10101a，保留其它未提交改动。
- 现行基线仍为 docs/01-方案设计/2026/10/通讯事件条件与动作条件及参数绑定优化基线-20261010.md。全局匹配改为等于／不等于／包含／不检查内容；先适配本次原生类型，再比较。参数无效、坏质量和集合不执行，不把解析失败当作不等于。接收事件触发的仅等于和 ParamBinding 自动判断保持不变。
- 移除全局旧来源、LegacyText范围、旧名称转换、待迁移按钮及分支，不新增迁移系统；旧规则不按新语义执行，需重新创建，以新格式识别拒绝旧规则。仅针对全局触发，不擅自清理其它兼容或改写用户方案文件。
- 心跳恢复单数据／多数据，发送内容为一个 TextBox 或两个 TextBox 加短横的单行布局；移除值 A/B 标签及专用 Boolean 编辑器，内容仍按发送目标类型校验。多数据是两份内容周期交替，不用 Range、不做索引或范围递增；本机 VM 4.4 心跳 HTML 原文已核对。
- 用户反馈箭头点击无反应及难命中：父节点箭头、名称及整行空白单击展开／收起；至少28×32逻辑单位箭头命中区、32行高。叶子单击提交并关闭，右键不更改；统一事件避免重复切换，覆盖键盘、重新打开定位、重复选择及滚动。
- 新增优化待用户明确实施。上一轮191项测试和Release启动为历史，不覆盖本次新增要求；此前被中断的树补丁本次只读检查未发现 Target_MouseDown 处理，未继续修改。后续按项目规则更新Release人工验收，不强制结束进程。

## 2026-10-10 全局数据触发与心跳优化实施完成

- 用户明确恢复实施，Codex 为本工作区唯一代码写入者；基线 main 1173baedc23eb879811f53242282c6072b10101a。保留既有未提交改动，无跨 AI 交接、提交推送或其它工作区同步。
- 全局数据触发已接入逐次不可变上传快照，保留来源、目标、会话、类型、质量及原始数据；Boolean／数值按本次类型解析常量再等于比较，String 支持等于／包含，不统一转字符串。重复有效通知不自行去重，数据上传关闭不影响接收事件。旧文本规则保留文本范围，旧限定来源规则暂停，确认迁移后才扩展。
- Modbus 本轮接入既有保持寄存器模型：单寄存器 UInt16、双寄存器按 IntBits32 和 ByteOrder 解码为 Int32；多值、未知类型及坏质量不直接放行。未新增线圈配置或浮点类型猜测，不向工作流节点赋值，不改 SDK／调度／发布规则。
- 心跳树使用本页局部模板修复子项缩进、折叠及叶子箭头；固定值／两值交替保留实际 A/B 行为，增加 A/B 标签、Boolean 选择及按发送类型校验的数值文本输入。清除目标停止并禁用心跳，保留参数草稿；确认弹窗统一主题。
- 最终 Release 通讯核心178项、触发界面1项、心跳10项、发送界面2项，共191项通过。全量 Release 895警告0错误；最后 UI Release 1060警告0错误。实际 EXE 无硬件启动探针退出0，版本2.0.0.8、Dark；设计时属性查询通过。真实设备、可见 VS 设计器、亮暗主题和高 DPI 人工验收待确认。
- 用户确认关闭后已更新 Release/SunEyeVision_v1.0.0/SunEyeVision/SunEyeVision.UI.exe；未强制结束进程、打包或增号。证据 artifacts/validation/global-data-heartbeat-20261010；详见 HANDOFF_GLOBAL_DATA_HEARTBEAT_20261010.md。下方待实施记录为历史，不代表当前状态。

## 2026-10-10 全局数据触发方案再次整合

- 用户要求再次整合并更新本地文档；Codex 仅修改方案及协作记录。现行基线改为全局数据触发：上传保留原生类型，触发参数按本次类型适配，Boolean／数值等于、String 等于或包含，不统一转字符串。
- Modbus 明确类型的位／寄存器值可直接通过全局接口触发，不必先创建接收事件；未解析多值或字节数组不猜类型、不取第一项。全局规则默认匹配所有有效上传来源，明确跨设备及混合类型可能命中，来源元数据保留。
- 撤销此前仅文本及 OPC UA String 接入、Modbus 数值只走事件触发的限制；旧规则迁移必须同时保留来源及文本类型范围，未经确认不能扩大。VM 转换细节尚未确认，原生类型适配为本产品设计选择。心跳树修复、固定值／两值交替输入优化保持待实施。
- 此前用户暂停实施时只完成代码读取，没有代码改动；本次保存文档不恢复实施，无构建、新测试、打包、增号、提交推送或同步。事件自动等于已实现，新增全局数据及心跳方案待后续明确实施；默认 Release 验收。

## 2026-10-10 全局字符串触发与心跳优化方案更新

- 用户要求更新本地方案，Codex 仅修改方案及协作记录；基线 main 1173baedc23eb879811f53242282c6072b10101a。不改代码、不构建、不新增测试结果、不打包、不增号、不提交推送或同步工作区。
- 现行基线 docs/01-方案设计/2026/10/通讯事件条件与动作条件及参数绑定优化基线-20261010.md 已替换字符串来源设备设计：规则面向统一上传文本通知，移除来源列、不增加设备树；TCP／UDP／串口文本及 OPC UA String 接入，Boolean/数值走事件触发。逐次通知非单个全局变量，数据上传关闭不影响接收事件；旧限定设备规则需确认迁移，不能扩大范围。
- 新增心跳局部树修复方案：缩进、折叠和叶子展开箭头；设备与数据点分层，父节点不可作为 OPC UA／Modbus 发送目标。模式改称固定值／两值交替，显示值 A／值 B，按类型输入；保留 Single/Alternate 实际行为，不新增范围递增模式。
- 事件触发无开关自动等于判断已实现；本次全局上传、来源列移除及心跳优化待实施。后续更新 Release 供用户测试，主题/设计器/真实设备人工验收单独记录。本次没有跨 AI 交接。

## 2026-10-10 触发参数自动比较完成

- 用户要求移除判断开关。Codex 当前写入者，基线 main 1173baed。有手填非空内容或绑定即比较，空输入且无绑定不比较；失效绑定拦截，不把绑定结果为空误当作未配置。旧 ParameterComparisonEnabled 仅保留序列化兼容、运行忽略，明确清空覆盖旧 FieldId。
- 已移除开关及 CheckedChanged 适配，ParamBinding 常态可编辑；保持单输出隐藏、多输出可选择。未改节点赋值、工作流调度、SDK、字符串来源或发布规则。
- Release 通讯/OPC UA 158项及独立UI 1项，共159项通过；最终 UI Release 1060警告0错误，实际启动探针退出0、2.0.0.8、Dark；设计时输出查询通过。真实设备、可见设计器、高 DPI 人工验收待确认。
- 已更新 Release，无打包、增号、强制结束进程、提交、推送或同步其它工作区。证据 artifacts/validation/communication-trigger-auto-condition-20261010；详见 HANDOFF_TRIGGER_AUTO_CONDITION_20261010.md。方案基线已替换旧开关说明，保留其它未提交变更。

## 2026-10-10 触发参数界面收简完成

- 用户要求移除固定等于的重复配置显示。Codex 为当前写入者，基线 main 1173baed。已移除独立判断输出列；启用开关、必要时的输出选择和 ParamBinding 合并为触发参数列。单输出有效时不显示选择框；多输出、未配置或选择失效时显示以供修复。不修改运行比较逻辑、序列化和 SDK。
- Release 行内 UI 1项及动作等于判断27项通过；UI Release 1024警告0错误，实际 EXE 启动探针退出0、2.0.0.8、Dark；设计时输出查询通过。可见设计器、亮暗主题、高 DPI 仍待人工验收。
- 用户确认关闭 Release 后更新，无强制结束进程、打包、增号、提交推送或同步。详见 HANDOFF_TRIGGER_COMPACT_UI_20261010.md；证据 artifacts/validation/communication-trigger-compact-20261010。方案基线同步当前 UI，保留此前未提交改动。

## 2026-10-10 动作等于判断实施完成

- Codex 当前代码写入者，基线 main 1173baedc23eb879811f53242282c6072b10101a。已实现显式 ParameterComparisonEnabled、稳定 ComparisonOutputId、按类型适配的等于比较；关闭判断不受残留条件绑定阻止，不向工作流节点赋值。
- 触发页增加主题开关、判断输出选择和禁用状态；修正本页 ToggleSwitch 交互回写并保持绑定，不改 SDK。字符串规则新增 SourceDeviceId：新建未选设备不执行，旧 null 保留全部范围，显示不检查内容并兼容旧不匹配。执行目标只显示工作流名，通讯响应编号映射保留。
- Release 通讯/OPC UA 157项、独立触发 UI 1项、发送 UI 2项、心跳10项，共170项通过；全量 Release 964警告0错误，最终 UI Release 1060警告0错误。原 Release EXE 启动探针退出0、2.0.0.8、Dark；实际 DLL 确认派发前调用 EvaluateEquality。设计时属性查询通过，可见设计器、高 DPI、真实设备仍待人工验收。
- tests/bin/Release 残留 hostfxr.dll、hostpolicy.dll、coreclr.dll 导致测试宿主无法启动；仅这三个文件移至 artifacts/runtime-host-backups/Tests-Release/62becf1b607f40778423fe65f50a11fe，逐一校验哈希，可恢复，未改发布规则或系统 .NET。
- 证据 artifacts/validation/communication-action-equality-20261010；详见 HANDOFF_ACTION_EQUALITY_20261010.md。用户确认关闭后更新 Release，没有强制结束进程。未打包、增号、提交、推送或同步其它工作区；保留既有发布及用户改动。

## 2026-10-10 动作等于判断最终方案更新

- 用户要求更新本地方案；Codex 仅修改文档，基线 main 1173baedc23eb879811f53242282c6072b10101a，无代码修改、构建、打包、增号、提交、推送或其它工作区同步。
- 已更新 docs/01-方案设计/2026/10/通讯事件条件与动作条件及参数绑定优化基线-20261010.md：事件触发后按判断输出类型适配 ParamBinding 的手填或绑定值，仅做等于判断；单输出自动选定，多输出明确选择；显式启用开关区分不限制与空字符串；不向节点赋值。
- 字符串触发保留完全匹配和部分匹配，将不匹配改称不检查内容，增加来源设备及兼容策略。执行目标移除数字后缀，通讯响应编号映射保留并独立说明。
- 当前 Release 触发参数仍仅取值携带，不参与动作判断，用户已复核；新增方案待实施。后续更新 Release 供用户测试，不默认 Debug，不自动打包。旧复杂比较规则和映射弹窗仅为历史，不是现行要求。

## 2026-10-10 触发参数收简

- 用户收窄范围：触发配置暂不向工作流节点运行参数或输入赋值；未来由通信工具链接入。行内触发参数使用 ParamBinding，可绑定当前接收事件的普通解析输出或组装输出，也可手填常量。
- Codex 为本工作区唯一代码写入者，基线 main 1173baed。已移除复杂执行条件和参数映射弹窗，通讯触发入口不再执行此前新增的独立条件或节点映射；旧数据保留兼容，通用执行副本能力不作无关回退。触发值仅随请求携带，目前宿主不据此过滤动作或修改节点。
- 通讯/OPC UA 130项和独立行内 UI 1项通过；独立 Debug UI 1058警告0错误，UI Release 881警告0错误，Debug 启动探针退出0、2.0.0.8、Dark；设计时属性查询通过。可见 VS 设计器、高 DPI 和真实设备待人工验收。未打包、增号、提交、推送或同步其它工作区。
- 详见 HANDOFF_TRIGGER_PARAMETER_SIMPLIFICATION_20261010.md。下方复杂条件与参数映射记录为此前实施历史，不再作为当前触发配置要求。

## 2026-10-10 通讯事件条件与动作条件及参数绑定优化基线

- 实施及自动化验证完成，待用户人工验收。核心通讯/OPC UA 130项、独立触发/发送/心跳 UI 13项、执行基线/相机流程5项，共148项通过；共享静态 WPF 宿主的并发 UI 超时已保留诊断，改为分组验证。Debug 已更新，最终单独构建841警告0错误；全量 Release 1060警告0错误、最终 UI Release 777警告0错误；Debug/Release 实际 EXE 启动探针均退出0、2.0.0.8、Dark。设计时属性查询通过，可见设计器与真实设备尚待人工验收。
- 交接详见 HANDOFF_COMMUNICATION_TRIGGER_OPTIMIZATION_20261010.md；证据 artifacts/validation/communication-optimization-20261010。复用现有 ParamBinding 和删除样式；目标绑定限制为契约声明的同类型标量。不改变文本严格类型规则，不打包、不增号、不提交/推送或同步其它分支。

- 用户已授权开始实施，Codex 为当前代码写入者。接入独立动作条件与普通/组装输出快照、契约声明的参数/输入映射、执行副本绑定覆写、OPC UA 可配置监视与 Boolean 边沿、触发 UI 列宽及复用删除样式；验证进行中。保留全部原发布改动和汇报文档改动，不打包、不增号、不提交推送。

- 用户补充删除按钮对比截图：触发配置仅见红框，其它位置能见居中“×”。已将两张原始图片保存到方案同目录 assets/communication-trigger-20261010 并在基线中引用；删除样式要求调整为复用现有正常删除按钮，不另做无边框图标，补充两页及主题/失焦/高 DPI 验收。仅文档和图片保存，未改代码或构建。

- 用户要求保存最终方案作为后续沟通和优化基线；执行者 Codex，main 基线 1173baedc23eb879811f53242282c6072b10101a。仅文档写入，保留现有代码和未提交变更，无构建、打包、版本递增、提交或推送。
- 基线文档：docs/01-方案设计/2026/10/通讯事件条件与动作条件及参数绑定优化基线-20261010.md。状态为待实施，不表示功能已完成。
- 设计边界：接收事件条件、独立动作条件（可无附加条件）、独立运行参数/模块输入绑定；本次事件快照贯穿比较与执行。普通解析和组装输出均可绑定，OPC UA 支持可配置变化订阅/周期读取及显式边沿规则，兼容旧配置。
- 保存了触发配置布局、主题适配、实施顺序、跨模块边界及验收标准；VM 行内触发字符的内部消费尚未核实，不作为确定机制。后续按本基线实施，先 Debug 测试并按规则补验 Release，发布需用户单独申请。

## 2026-10-09 普通 Release 直接启动修复

- 执行者 Codex，基线 main 1173baed；用户要求普通 Release 构建也能正常启动。保留普通 Release 输出路径与框架依赖模式，不再打包、不递增版本。
- Directory.Build.targets 新增 QuarantineStaleRuntimeHost：普通 UI 构建完成后仅在本工作区默认 Debug/Release 根目录，将残留 hostfxr.dll、hostpolicy.dll、coreclr.dll 移至 artifacts/runtime-host-backups 保留；跳过自包含、PackagingRoot、DesignTimeBuild、自定义输出目录、非 UI 项目，不清空目录或移动用户配置。
- 实际旧 Release 的三个冲突文件已备份到 artifacts/runtime-host-backups/Release/ac07a333-a911-47dc-bcd1-d50700add77a，原哈希逐一核对一致，没有删除。全量普通 Release --no-restore 1146 警告、0 错误。
- 原路径 SunEyeVision.UI.exe 直接启动 --release-probe 退出 0，版本 2.0.0.8、Dark。宿主日志确认使用 C:/Program Files/dotnet 及 NETCore/WindowsDesktop 9.0.20，不再把 Release 当 dotnet root。
- 只读观察到用户启动的 Release PID 41724 显示“SunEyeVision 解决方案管理器”、Responding=True，实际加载系统 hostfxr/coreclr；未强行终止用户进程。用户确认关闭后，已补验普通 UI Release：928 警告、0 错误；随后原 EXE 直接启动探针再次退出 0、2.0.0.8、Dark。首次重复构建的 MSB3027/MSB3021 是运行中 DLL 占用，现已解除；重复构建无新增宿主备份、无版本或安装包变化。
- 构建边界回归 utilities/Test-RuntimeOutputGuard.ps1 52 项通过（默认 Release/Debug 正向、自包含/打包/设计时/自定义路径/非 UI 跳过、备份哈希、用户文件保留、重复执行幂等）；installer/Test-Release.ps1 90 项通过。设计时 TargetPath/OutDir 不变。已发布 2.0.0.8_alpha 安装包 SHA256 未变化。
- 详见 HANDOFF_RELEASE_STARTUP_20261009.md；可见 VS 设计器及用户明确人工验收待确认。保留此前所有发布改动和用户未跟踪文件，无提交/推送。

## 2026-10-09 正常发布 2.0.0.8_alpha 与图标关联核验

- 执行者 Codex，基线 main 1173baed；用户明确授权正常打包并关注图标关联。发布成功后 version.json.fileRevision=8，releaseDate=2026-10-09；未提交、推送、安装或修改本机文件关联。
- 修正安装包图标旧来源，使用隔离载荷 Icons/suneyevision.ico；开始菜单/桌面快捷方式显式 IconFilename/索引 0，卸载条目同用带背景应用图标；.solution 保持独立折角文件页图标，ChangesAssociations=yes 通知 Shell 刷新，保持 UserChoice 防劫持规则。
- Directory.Build.props 常规还原也保留 RuntimeIdentifiers=win-x64，补齐 54 份依赖锁。原依赖版本及内容哈希变化为 0，后续发布仍使用 locked-mode。第一次因旧锁无 RID 失败，未递增版本；重试成功。
- 全量隔离 Release 1144 警告、0 错误，发布回归 238/238，脚本检查 90 项通过。自包含 EXE 探针退出 0，版本 2.0.0.8、alpha、Dark；19 个插件程序集载荷通过。安装包/EXE 提取 48px 图标均为冰蓝背景 ARGB=255,220,239,252；两份 ICO 载荷与源码哈希一致。
- 产物 dist/2.0.0.8_alpha/SunEyeVision-Setup-2.0.0.8_alpha-x64.exe，249360316 字节；SHA256 A1366218D19E1EFE9E48AC9C3FB1C5F08A048FC1904415DF6938C5612B645FB1，与清单及 sidecar 一致。完整交接见 HANDOFF_PACKAGE_ICONS_20261009.md，实际安装/升级/卸载及 Explorer 关联待隔离环境验收。
- 用户截图旧 Release/SunEyeVision_v1.0.0/SunEyeVision 双击提示缺 .NET：本机已有 9.0.20，旧目录框架依赖 runtimeconfig 混有历史 hostfxr/coreclr，宿主跟踪确认把该 Release 目录当 dotnet root，No frameworks were found，诊断退出 -2147450730；不是应用代码/设备错误。仅诊断，未清理、覆盖旧目录；新包来自干净独立 staging，真实启动探针已通过。日志 artifacts/validation/baseline-sync-20261009/old-release-host-trace.txt。

## 2026-10-09 主程序与通讯管理统一开发基线

- 执行者：Codex；main 与 feature/comm 合并后指向同一个提交，之后各自在原分支继续开发。
- 同步前基线：main d03579d4；feature/comm d24db93b。已建立两个 codex/pre-sync-*-20261009 恢复引用。
- 保留主程序方案管理、单实例、预览、Logo、发布规则；整合通讯心跳、触发、响应。无版本递增、安装包或推送。
- 合并及回归完成：全量 Release、主程序 Debug 构建通过，关键回归 213/213 通过；Release 无硬件启动探针确认暗色主题并正常退出。同步交接提交完成后，通讯分支只做 fast-forward，不另造一套源码。详见 HANDOFF_BASELINE_SYNC_20261009.md。

### 主程序此前记录
## 2026-10-09 桌面快捷方式实际图标修复

- 用户现场截图仍为透明 Logo。复核桌面 SunEyeVision.UI - 快捷方式.lnk 目标已是当前 dev/Debug，但 IconLocation=",0" 自动从 EXE 取图标。按既有桌面图标替换要求，先将原 lnk 备份到 artifacts/validation/desktop-icon/shortcut-before-explicit-icon-20261009-a4684f9c50a54d0ca6c3df977e2eeef4.lnk，再仅将 IconLocation 显式设为当前 dev/Debug/Icons/suneyevision.ico,0；读回目标、参数、工作目录保持不变。定向 SHChangeNotify UPDATEITEM/路径/FLUSH，不重启 Explorer、不删除缓存或其他快捷方式。通过 Windows SHGetFileInfo 对该 lnk 读取实际 Shell 图标为 48x48，顶部背景 A255/R220/G239/B252，确认 Shell 已返回带冰蓝背景图标，屏幕实际重绘仍请用户确认。未改程序代码、未构建/打包/增号，原文件可从备份恢复。

## 2026-10-09 解决方案文件图标更新（实施中）

- 用户明确授权“切换”，已将本机 .solution 临时切到当前 Debug：确认仅 .solution 使用 Applications\SunEyeVision.UI.exe 后，仅修改 HKCU Software\Classes 下该应用身份的 shell\open\command 默认值与 DefaultIcon 默认值，分别为当前 dev/Debug/SunEyeVision.UI.exe "%1" 和 dev/Debug/Icons/solution.ico,0。原值/键存在性/类型及新值备份于 artifacts/validation/solution-icon/association-debug-backup-20261009-7bd165.json；写前检查旧值未变化，写失败恢复原默认值。未改 UserChoice/Hash/扩展名默认身份、HKLM 安装位置或其他扩展名，未启动真实方案或设备。已通知 Shell 刷新；Windows AssocQueryString 指定 open 动作读取 COMMAND/EXECUTABLE/DEFAULTICON 均 S_OK 且指向新 Debug（未指定动作的一次 EXECUTABLE 查询返回 NO_ASSOCIATION，不能以此声称所有查询成功）。此为用户授权的本机测试切换，不修改产品内“开发版不自动劫持关联”规则；桌面视觉刷新/双击实际方案待用户测试，正式安装/修复可能更新此应用身份，未来需按发布状态恢复。未构建/打包/增号，未触及方案内容。

- 资源交付完成：SolutionIcon 为折角文件页 + 共用 C 几何；solution.ico 16/24/32/48/64/128/256 七帧，19162 字节，已部署 dev/Debug/Icons，预览 artifacts/design/branding/c-logo/solution-icon-preview.png。Debug UI 编译及 43 项图标/关于/方案管理回归通过，TRX artifacts/validation/solution-icon/solution-icon.trx；隔离 UI Release 0 错误、930 警告，git diff --check 通过。首次嵌套单项 StaticResource 隐式集合解析失败，改显式 DrawingCollection 后通过；导出器补 Stop 与原子替换，避免解析失败覆盖有效 ICO。应用图标/内部透明 Logo 不变，未打包/增号/提交/推送；可见设计器和实际 Explorer 文件显示待人工验收。
- 系统关联只读发现：UserChoice ProgId=Applications\SunEyeVision.UI.exe，有效打开命令仍指 D:\MyWork\SunEyeVision_Dev\Release\SunEyeVision_v1.0.0\SunEyeVision\SunEyeVision.UI.exe（文件存在），没有显式 DefaultIcon；不是当前 V2 dev/Debug。因此本轮资源更新不能宣称本机桌面文件图标或双击目标已更新。未写注册表、UserChoice 或安装目录；是否临时将本机关联转到当前 Debug 测试版需用户明确确认，默认保留已有安装关联防劫持策略。

- 用户补充解决方案文件图标；新增独立折角文件页 + 同源 C 深蓝/金色 Logo 的 SolutionIcon 原生矢量，输出 Icons/solution.ico 七帧及 PNG 预览。文件关联服务与安装器已引用此物理路径，沿用既有内容部署；不改系统注册表/用户默认关联，Debug 防劫持安装版关联规则保留。仅资源与导出脚本及测试，应用带背景图标和内部透明 Logo 不变，不打包/增号。发现独立旧 installer/Build-Icons.ps1 与安装包 SetupIcon 的默认源仍指旧 Logo，此次不修改发布规则；后续用户批准发布时需统一安装包图标源，不能让旧手动脚本覆盖新 solution.ico。

## 2026-10-09 桌面 Logo 带背景预览（待确认）

- 已完成正式替换：Logo.xaml 拆出冻结共享 BrandDrawing，透明 BrandLogo 不变，新增冰蓝圆角 AppIcon；Export-ApplicationIcon.ps1 从 AppIcon 原 512 画布生成七帧 suneyevision.ico。Debug UI 编译及 17 项图标/关于/单实例回归通过（含所有 ICO 帧实色背景、透明圆角、渐变及与内部透明图形同源断言）；TRX artifacts/validation/desktop-icon/desktop-icon.trx。隔离 UI Release 0 错误、928 警告；git diff --check 通过。实际从 dev/Debug/SunEyeVision.UI.exe 用 Windows ExtractIconEx 提取 48x48 图标，顶部背景 A=255/R220/G239/B252，确认 EXE 内嵌带背景图标，而非只改旁置 ICO。VS 设计时 ApplicationIcon/TargetPath 查询通过，可见设计器及用户桌面缓存显示仍待人工验收。正式 Debug 已更新，程序内部四处透明 Logo 不变，未修改现有用户快捷方式/图标缓存/安装版、不打包/增号/提交/推送；保留原稿、基线和单写入者不变。

- 用户已确认冰蓝背景稿并要求替换。实施为单独 AppIcon 矢量资源，复用同一 BrandDrawing；四处界面仍使用透明 BrandLogo，只有可执行/任务栏/桌面 ICO 加背景。原几何与色板不变，不动工作流、单实例、SDK、安装发布规则。生成 16/24/32/48/64/128/256 帧 ICO，按原 512 画布保持圆角外透明。源 SVG 收入 src/UI/Icons/suneyevision-app-icon.svg；本轮将构建 Debug 验证，不打包、不增号，保留此前全部变更。

- 用户认为浅灰/极浅冰蓝底板变化不明显，要求直接看带背景 Logo。沿用已确认 C 版深蓝/金色/白 Ai 原生几何，新增明确冰蓝渐变圆角底板（#E8F6FF/#B7DAF2/#77B0D9）及细描边，无光晕/模糊。SVG 与实际渲染 PNG 位于 artifacts/design/branding/c-logo/suneyevision-c-background-preview；本轮仅预览，不更换应用资源或 ICO、不构建/打包/增号，保留此前稿件及全部源代码变更。

## 2026-10-09 单实例与旧进程清理（实施中）

- 最终交付：单实例 UI 入口与 IPC 已实施；同一用户/登录会话共享身份，不按目录或版本放行第二实例。重复启动激活可见窗口，方案路径经已有停止/保存/身份冲突/切换流程处理；启动选择、模态框、准备切换、退出及无响应均拒绝新方案或给出提示，第二实例不会进入设备初始化；过期 IPC 不迟到切换，准备转换新增重入保护并在 finally 复位。窗口关闭确认后标记 Closing，后台实例锁持有到进程结束，原卸载保护保持不变。本轮未改设备释放超时策略，App.OnExit 同步等待风险仍需独立定位，不能宣称全部退出风险已解决。
- 验证：最终全量 Debug 成功（0 错误、1010 警告），dev/Debug/SunEyeVision.UI.exe 更新；隔离 UI Release 成功（0 错误、1041 警告）。82 项相关回归通过（含 10 项单实例与重入/转换链回归、卸载锁、方案/Logo/图像），TRX artifacts/validation/single-instance/single-instance-final.trx。四项跨进程检查通过：互斥及路径转交、关闭拒绝、退出后重新取得锁、真实 Debug 第二实例 PID87996 转交后 exit=0；真实程序走启动前拒绝分支，不载入真实方案、不连接设备、不写真实用户配置。结果 artifacts/validation/single-instance/cross-process-results.txt；测试宿主首次直接程序集引用缺失 System.Text.Json10，改完整 ProjectReference 后通过，其异常留下的测试快照 PID78656/88640 已精确核对测试路径与参数并结束，后续宿主受控树清理。不得把初始宿主依赖失败称为产品验证通过。
- 环境复核：旧六个 SunEyeVision PID 与本轮两个测试快照均通过原生进程 WaitForSingleObject(0) 确认为 Exited；未终止其他应用、未删除方案/配置/资源。外部句柄持有的已退出系统对象仍可被任务列表枚举，不能保证列表条目清空，但没有这些旧实例继续运行。git diff --check 通过，VS 设计时属性查询通过；可见设计器、实际窗口唤醒和真实方案保存取消/设备退出均待用户现场验收。未打包/增号/提交/推送，基线及单写入者不变；跨 AI 交接前仍按模板创建明确临时提交。

- 用户明确要求实施单实例并清理旧残留后 Debug 测试；Codex 单写入者，基线 main/d703960764e0f1cdd287c8398def759ad638251d，保留此前改动，未提交/推送。六个精确 PID 已核对名称/日期/时段：五个系统等待对象已退出（46004/60576/28256/72988/81700），85836 唯一未结束，已按用户授权 TerminateProcess 成功并等待到退出；不触及其他应用、不删除文件。已退出对象被外部持有时仍可能被列表枚举，不能宣称系统列表完全空。
- 跨模块评估：新增 UI 启动前互斥与当前用户命名管道，限定同一 Windows 用户/登录会话、跨安装路径版本统一标识；独立后台线程持有实例互斥，避免 async 线程归属错误，保持直到进程结束。原卸载维护 Running/Maintenance 标记及安装脚本不改。重复实例只转交激活/方案路径，不初始化设备；IPC 限长度/期限/当前用户，忙碌/关闭拒绝，UI 只调用已有停止/保存/方案切换流程，不直接调度工作流。设备退出释放超时改造不在本轮范围，禁止自动强杀无响应主实例。无在线更新、打包、增号。

## 2026-10-09 C 版 Logo 透明与主题适配

- 最终交付：四处界面采用同一个原生 DrawingImage（Resources/Branding/Logo.xaml），源 SVG 已保存 src/UI/Icons/suneyevision-logo.svg；亮暗主题均保留用户选定深蓝 #0964AC/金色 #F2AE16/白色 Ai，透明无滤镜。多尺寸 suneyevision.ico 已作为 ApplicationIcon，旧资源保留。UI 隔离 Release 编译 0 错误、1056 警告；正常全量 Debug 最终 0 错误、1004 警告，dev/Debug/SunEyeVision.UI.exe 已更新。Logo/关于/管理器最终回归 31/31 通过，TRX artifacts/validation/c-logo-tests/c-logo-verified.trx；独立测试宿主补齐 WPF pack 注册以消除测试顺序依赖。实际亮暗 Logo 离屏预览已核对，关于窗口实际 Logo 类型断言通过；VS 设计时 ApplicationIcon/TargetPath 查询通过，可见设计器和现场运行效果待用户验收。未打包、增号、提交、推送；未主动启动真实程序。首次 Debug 遇 PID85484 锁定失败，用户要求关闭时该 PID 已不存在，未实际杀进程，随后重构建通过。

- 用户追问旧 PID 残留：本轮只读诊断、不修改退出实现、不清理进程。六 PID 来源于旧测试链：父 PID85368/85792 分别与 _capture_log.txt（11:01:48 启动截图）/_capture_log2.txt（11:05:46 启动截图）一致；后三个命令行指向旧 src/UI/bin/Debug/net9.0-windows 可执行程序及 1.solution，非当前 dev/Debug。原生 OpenProcess/GetExitCodeProcess/WaitForSingleObject(0) 检查：46004/60576/28256 的进程对象已 signaled（退出），虽然 exitcode=259，因此不可单凭 STILL_ACTIVE 判定；72988/85836/81700 返回 WAIT_TIMEOUT，未完成退出。Win32/.NET 未枚举到后三者线程，实际卡住位置和暂停施加来源未证实，不能归因正常关闭泄漏。当前 App.OnExit 同步无总超时等待相机/通讯释放，存在阻塞风险但未证实为本次原因；来源链明确，根因仍需具体挂起/退出现场或堆栈确认。Microsoft 官方进程终止/等待对象说明已核对。不要将六 PID 一概当作正在运行或一概当作已退出。

- 当前追加实施：用户选定亮色版深蓝/金色，所有主题统一该配色，要求正式应用并构建 Debug。跨模块评估：Logo 为 UI 独立矢量资源，不修改 ThemeKeys/SDK 色板、插件、工作流或安装发布规则；AppResources 合入原生 DrawingImage，四处品牌图像消费同一资源。Windows 可执行/任务栏图标从同源矢量导出 16/24/32/48/64/128/256 透明 ICO。只改应用品牌资源和 UI 引用，保留全部旧稿与工作区变更；未打包/增号。验证及构建结果待完成补记。

- 用户选择 C 流线智眼，要求透明并适配主题。使用 imagegen 技能尝试透明提取及亮暗两版，真实 Alpha 已验证，但生成结果含非预期光晕，不作为正式交付；转为按 C 轮廓手工重绘纯路径/圆形 SVG，同一几何亮暗两套蓝金色，Ai 采用不透明白色。文件 artifacts/design/branding/c-logo/suneyevision-c-light.svg 与 dark 对应文件；透明 PNG 与主题底预览由 Render-CLogo.ps1 实际渲染。提示与制作说明 production-record.json。保留原稿，不替换软件资源、不构建/打包/增号；小尺寸应用图标及正式集成未实施。当前仍 Codex 单写入者，基线不变。

## 2026-10-09 矢量 Logo 设计提案（待用户确认）

- 用户要求先提高设计质量、四元素差异较大方案并参考同类成熟品牌。本轮使用 imagegen 技能及内置图像工具，四个初始概念 A 留白融合/B 光束解析/C 流线生长/D 几何识别；C 初稿像植物、D 初稿像光圈，定向修订为 C 流线智眼（太阳内 AI 留白）、D 几何识别（两块眼形与特征切口）。精选 A/B/C/D 为 PNG 概念图，不是 SVG，不替换软件正式资源；源提示/修订提示/官方参考来源保存 artifacts/design/branding/imagegen-20261009/prompts-and-research.json。参考 OpenCV 官方媒体包（https://opencv.org/media-kit/）、MVTec 软件资料（https://www.mvtec.com/press/press-material）、NVIDIA 品牌规范（https://www.nvidia.com/en-us/about-nvidia/legal-info/logo-brand-usage/，仅一般留白/尺寸/单色规范，不借用硬件意象或商标）及 Roboflow 视觉 AI 定位（https://roboflow.com/press）。computer-use 浏览器视觉检查两次 helper_unknown_error 失败，已改官方网页/媒体资料研究，不能宣称全部参考 Logo 都完成截图审查。四元素以眼为主、太阳为焦点、光与智能结合结构，避免拼四图标；小尺寸/单色/亮暗测试和最终矢量重绘留待选择后实施。未构建/打包/增号，旧稿均保留。

- 用户认为光与智能张力不足，要求多个方案。新增四个独立方向：A 光束解码（斜向入射/识别焦点/像素输出）、B 神经光场（特征网格/聚光）、C SV 光轨（品牌字母/光轨/数字化）、D 像素光谱（光流/图像矩阵/高亮特征）。纯 SVG 源及同名 512 PNG 位于 artifacts/design/branding/alternatives/；Render-Comparison.ps1 生成 comparison.png，四版实际统一尺寸对比图已核对。保留全部旧稿，未更换正式 Logo 或修改运行代码、未构建/打包/增号；等待用户选择方向再深化。

- 用户要求视觉软件而非硬件意象，并同时包含光、视觉、智能、科技。第三版“光智视域”已完成：两道渐变光带形成抽象视域，内部特征网格汇聚至明亮识别核心；不使用镜头、光圈、芯片或引脚。文件 artifacts/design/branding/suneyevision-light-intelligence.svg 与 suneyevision-light-intelligence-icon.svg；同名 PNG 为实际渲染预览，图标版已核对。仍为待确认设计稿，保留前两版、不替换正式资源、不构建/打包/增号。

- 用户认为第一版偏通用眼睛图标，要求同时体现智能和视觉。第二版已制作：六叶镜头光圈环绕中央芯片，芯片内四个神经连接节点；蓝/青/绿色、透明版及深蓝应用图标版。源文件 artifacts/design/branding/suneyevision-intelligent-vision.svg 与 suneyevision-intelligent-vision-icon.svg，同名 PNG 为实际渲染预览，图标版已视觉核对。保留第一版；渲染脚本增加 Names 参数，不改程序资源，不构建/打包/增号。第二版仍为待确认设计提案。

- 用户请求设计矢量 Logo，已沿用蓝青色和光学之眼意象制作原创几何提案：眼睛轮廓、镜头核心、三条日光线。透明版 suneyevision-logo-concept.svg 与深色应用图标版 suneyevision-app-icon-concept.svg 保存于 artifacts/design/branding/；均为纯路径、圆形、渐变，无位图、字体依赖或模糊滤镜。
- 附 Render-Concept.ps1 将矢量提案渲染为同名 512 PNG 供预览；两版实际预览已核对。SVG 为可编辑源文件，PNG 仅用于沟通预览。此为设计草案，不代表品牌定稿；小尺寸 ICO 简化及 WPF DrawingImage 适配留待用户确认后实施。
- 未替换 app_logo.ico、应用图标或任何 UI 引用，未构建、打包、增号、操作真实方案数据；此前代码及测试成果保留，单写入者及基线不变，未提交推送。


## 2026-10-09 图像预览器交互与失效引用优化

- 最终构建：全量 Debug 成功，0 错误、772 警告，dev/Debug/SunEyeVision.UI.exe 已更新。本节下文“构建进行中”为过程记录，当前已完成，等待用户现场测试；未创建安装包、未递增版本。

- 当前执行者 Codex，单写入者；基线 main/d703960764e0f1cdd287c8398def759ad638251d；保留此前全部改动，未提交/推送。范围：ImageInputSource、ImagePreviewControl、ImageResultControl、MainWindowViewModel、WorkflowExecutionManager 及相关 UI 测试。修改执行前已检查请求入口、输入快照、单次/连续执行和成功推进链：UI 只发起明确请求，执行管理器通过每次请求的 advanceLocalImage 参数决定是否推进；不引入全局临时开关，不改 SDK/插件/工作流引擎契约，连续运行和执行按钮默认策略不变。
- 已实施：添加/恢复时过滤不存在的路径，恢复按原选中路径重映射索引，选中图片失效时为 -1；原配置对象不就地改写，正常保存使用有效列表。移除预览器“移除失效/定位”按钮及命令；保留兼容输入源 API，不再提供 UI 入口。普通 SelectionChanged 只同步高亮；左键和明确键盘入口各请求一次，右键仅选中并提供主题化“移除图片引用”，不执行。手动选图执行不推进，默认成功执行才允许推进，失败/取消不推进；正在执行时拒绝新的执行请求，不改变正在运行的输入快照。索引、模式、自动切换使用 SetCurrentValue 保留双向绑定，移除单图只采用输入源已修正的索引，避免重复扣减；重复/无效添加不改索引或配置。引用操作不删除磁盘文件。
- 验证：33 项相关回归通过后增加双向绑定/移除身份回归，最终更广泛 Debug 集成 190/190 通过（管理器含本轮 5 项新增回归）；TRX：artifacts/validation/image-preview-policy/tests/houzhongjie_SZDKSNB076_2026-10-09_11_10_01_net9.0.trx；git diff --check 通过。测试提供合法隔离参数及测试工具，临时方案服务/工具注册均还原；没有读取真实方案或连接真实相机。当前全量 Debug 构建进行中，完成后记录最终结果。版本仍 2.0.0.7_alpha，未打包/增号；可见 VS 设计器和现场鼠标/键盘/真实节点运行需用户验收。


## 2026-10-09 当前实施：恢复旧版卡片框架与右键交互

- 卡片等高最终构建：用户再次确认关闭并保持关闭后，重试全量 Debug 成功，0 错误、772 警告；dev/Debug/SunEyeVision.UI.exe 为本轮等高版本，可开始现场测试。以下首次构建文件占用为过程记录，已解除。21/21 管理器回归及亮暗实际渲染通过，未打包、未增号。

- 卡片等高追加修复：用户反馈有/无描述的卡片参差不齐，统一 SolutionCardPanel 的 CardHeight=112 逻辑像素、行距=12，排版不再根据描述改变行高，Arrange 使用统一高度；描述 TextBlock 固定保留 16 高度，即使为空也不折叠，修改时间及状态基线保持一致。只修改面板、管理器描述布局及新增等高回归；隔离 Debug 管理器测试 21/21 通过，亮暗 1040 实际渲染已核对，预览目录 solution-cards-uniform；git diff --check 通过，版本仍 revision=7。用户先确认关闭后启动正常全量 Debug 构建，期间用户再次启动程序（PID 74328），Threshold 插件 DLL 被锁，最终 MSB3027/MSB3021：2 错误、1014 警告。主程序已更新，但全量 Debug 未成功，已请用户再次关闭并保持关闭后重试；不得宣称本轮全量构建通过，不强制结束进程，不打包/增号/操作真实方案数据。用户现场等高验收及可见 VS 设计器待验证，单写入者/基线不变、未提交推送。

- 10:12 现场空引用后续修复：用户完整堆栈明确为 SolutionCardPanel.MeasureOverride 原第 60 行，generator.GeneratorPositionFromIndex 调用时 generator 为 null；不是前轮 WindowChrome 跨线程缺陷。原实现先读取 ItemContainerGenerator，再通过 InternalChildren 触发首次宿主连接，导致局部变量仍保留 null。现调整为先访问 InternalChildren 建立连接、再读取生成器，并保护尚未连接面板。只修改 SolutionCardPanel 与 SolutionManagerCardTests：重开回归移除 Render 预热，增加最小 ItemsPresenter 宿主首次测量测试，断言非空实际卡片；测试宿主显式提供数据源，避免未加载绑定的假空列表；撤回试验性的构造时 IsItemsHost 赋值，保留框架模板管理以免影响主题复用。20/20 Debug 管理器回归通过；全量 Debug 0 错误、838 警告，dev/Debug/SunEyeVision.UI.exe 已更新，19 插件目录保留，git diff --check 通过。用户已明确关闭程序后更新，未强制结束进程。未打包/增号/改真实配置；version.json 仍 revision=7。现场“打开方案→主界面重新打开管理器”仍待用户复测，可见 VS 设计器未验证；旧预渲染测试覆盖不充分，本轮已补首测及直接显示路径。

- 10:05 后续故障修复：用户运行反馈删除确认时后台列表白底、从主界面重开管理器失败。白底源于默认 WPF ListBox 禁用模板，新增局部 SolutionList 模板保持透明/主题色（不改共享色板），删除/移除及恢复确认传递管理器 Owner。真实窗口回归在完整测试序列复现共享静态 WindowChrome 的跨 Dispatcher 异常，改为每次构造独立实例并验证启动窗口关闭后有宿主重复重开。原 09:57:40 用户日志只有概括消息、文件通道无堆栈，尚不能认定同一根因；主界面错误现在附完整 ex 并写 UI+File，若现场仍失败可进一步定位。Debug 管理器测试 19/19 通过；全量 Debug 0 错误、772 警告；dev/Debug/SunEyeVision.UI.exe 已更新，版本仍 2.0.0.7_alpha。git diff --check 通过。没有打包、增号、结束用户进程或操作真实方案文件；删除弹窗/主界面重开现场复测与可见 VS 设计器仍待用户验收。修改管理器 XAML/CS/VM、MainWindowViewModel 日志及 SolutionManagerCardTests；基线和单写入规则不变，未提交/推送。

- 追加优化已完成：用户指出固定卡宽造成右侧大量空白，改为最小卡宽 240 逻辑像素、列数按视口宽度计算、各列均分剩余宽度并填满整行。保留最后一行同列宽及视口虚拟化；修改 SolutionCardPanel、管理器 ItemsPanel 与布局回归断言。最新 Debug 管理器回归 17/17 通过（整行右边界、响应式列数、500 项滚动、亮暗切换等）；实际亮暗 1040 预览已核对为三列并填满首行，预览目录 solution-cards-responsive。停止本任务旧 Debug 构建后已重新完成全量 Debug，0 错误、780 警告；程序 dev/Debug/SunEyeVision.UI.exe，19 插件齐全。git diff --check 通过。未打包、未增号、未自动启动真实程序；用户运行验收和可见 VS 设计器仍待验证。以下固定 280 宽度的交付说明属于追加前历史状态。

- 本轮交付状态：代码及自动验证已完成。按最终用户追加要求，左侧最近记录只显示名称，不显示打开时间；仍按 LastAccessTime 排序。固定宽度 280 逻辑像素卡片按视口自动换行，不强制双列，500 条目录仅生成视口及缓冲容器；长描述单行摘要保留完整工具提示；卡片修改时间、已打开和启动默认分别表达；移除详情底栏、三点、编辑铅笔及重复功能按钮。右键先选中目标，另存仅当前选中方案提供，失效方案保留定位/移除，键盘 Enter 限列表范围、Shift+F10/菜单键可管理；最近选择及新登记方案清除隐藏搜索并滚入视口。目录/关联/内部故障恢复收进设置，技术统计只在主题化只读属性窗口出现。主题模板更换的容器复用问题已修复。
- 已修改文件：SolutionConfigurationDialog.xaml/.xaml.cs、SolutionConfigurationDialogViewModel.cs；新增 SolutionCardPanel.cs、SolutionIdentityVisibilityConverter.cs、SolutionInformationDialog.xaml/.xaml.cs；SolutionManagerUiTests 改为 partial，新增 SolutionManagerCardTests.cs 共 10 项回归；本记录及原最终方案追加本轮实施记录。只改 UI 消费和验证，不改共享主题色板、方案存储、执行调度、SDK/插件/发布规则。此前所有工作区改动保留，未提交或推送；跨 AI 写入交接前仍需按模板创建明确的临时提交。
- 构建与测试：隔离 UI Release 0 错误（1056 警告）；全量 Release 0 错误（849 警告，增量构建计数），19 插件齐全；最新代码发布级集成回归 181/181 通过（含 17 项 SolutionManagerUiTests）；TRX 位于 artifacts/validation/solution-cards/tests；亮暗、最小窗口、三列/更多列、大列表滚动及主题复用、正常/失效卡片、局部 Enter、右键目标和另存限制均已自动验证。真实 Release exe 隐藏无设备探针 exit=0，2.0.0.7/alpha/Dark/#FF1E1E1E；新程序位于 artifacts/validation/solution-cards/app/SunEyeVision.UI.exe。版本计数仍为 7，未生成新包；旧 dist/2.0.0.7_alpha 不含本轮 UI。
- 人工验收结果：亮暗离屏 PNG 已核对，正常四方案预览位于 Codex visualizations 的 solution-cards/SolutionConfigurationDialog-Dark-1040.png 和 Light 对应文件；用户未反馈现场运行验收。未运行常规程序载入真实配置/相机、未实际安装/卸载、未写真实关联注册表或用户配置，未结束用户进程。VS 设计时 MSBuild 属性查询通过；可见设计器、实际键鼠/DPI/真实设备仍待人工验收。
- 发布流程（2026-10-09 用户明确要求）：先构建 Debug 供用户测试；测试确认后，再由用户明确批准发布打包。不得随每次修改自动打包或递增发布版本；如需打包必须先申请，用户直接要求打包时视为该次授权。当前全量 Debug 已完成，等待用户测试；不生成安装包、不修改 version.json；不要用旧 7 号安装包判断本轮界面。

- 当前执行者 Codex，单写入者；基线 main/d703960764e0f1cdd287c8398def759ad638251d。用户最终确认：小卡片按宽度自动换行、保留左侧最近记录、最近只显示名称不显示打开时间、卡片保留修改时间、删除下方详情和重复按钮、取消三点及编辑铅笔、管理操作统一右键；启动模式置于右上，目录/关联/内部故障恢复收进设置，工作流数量和格式版本按需属性查看。
- 已进入实现：相关管理器 XAML/VM/交互、SolutionCardPanel 虚拟化换行面板、状态及摘要转换器、只读 SolutionInformationDialog；增加现有 SolutionManagerUiTests partial 回归。仅消费主题资源，不改共享色板、存储格式、工作流执行或插件契约；保留先前全部改动，未提交/推送、未改真实配置或注册表。首轮隔离 UI Release 构建 0 错误；交互/布局测试进行中，尚未宣称验收完成。后续交付记录更新本节；没有新安装包，旧 2.0.0.7_alpha 不含本轮界面。

以下为历史交付记录。

## 2026-10-08 本轮交付：解决方案管理器完整实施

- 基线提交：d703960764e0f1cdd287c8398def759ad638251d；当前分支 main；当前执行者 Codex。当前仅一个 AI 写入，保留全部既有改动，未提交或推送，未向其他 AI 交接。
- 任务目标：完整实施 docs/01-方案设计/2026/10/08/解决方案管理器最终优化方案-20261008.md，并交付包含全部改动的正常安装包。
- 允许修改目录：src/Workflow、相关 UI、installer、tests/SunEyeVision.UI.Tests、方案及协作文档。
- 已修改文件：Workflow 的 ApplicationDataPaths/ApplicationDataLocationService/Persistence/AtomicJsonFile、Solution/Metadata/Settings/Repository/Manager/StartupDecisionService；UI 的 App、ServiceInitializer、DataDirectoryDialog、SolutionConfigurationDialog/VM、MainWindow/VM、LayoutConfig、ImageInputSource/ImagePreviewControl、WorkflowExecutionManager、FileAssociationService；installer/SunEyeVision.iss/Pack-Release.ps1；SolutionManagementTests/SolutionManagerUiTests/SolutionTransitionTests 及旧 CommunicationArchitectureTests 恢复策略断言；方案文档和本记录。此前发布/关于/主题等改动保留。
- 已完成：数据根目录确认/恢复和身份检查；Config 与方案/安装位置分离；布局并入用户设置；旧配置显式导入/验证/回滚；任意路径方案及相对资源处理；名称描述随方案传递；新建/复制/另存/改名冲突和保存失败保护；运行停止等待、未保存切换及重复关闭保护；空工作区退出保存走完整方案而非旧工作流 JSON 导出；失效图像可清空、重定位且空配置持久化；预览卸载不清空真实输入；主题化管理器、虚拟化/失焦选中/长描述滚动、三种启动模式；安装版关联修复及开发版防劫持；内部一份上一保存版本恢复。不创建 Backups/Config、不自动搬动桌面方案、不收集打包资源。
- 构建与测试：最终隔离全量 Release 0 错误、1143 警告；171/171 发布集成测试通过；46 项方案专项加 6 项通讯架构定向共 52/52；82 项发布脚本检查通过；实际自包含 Release 探针为 2.0.0.7/alpha/Dark/#FF1E1E1E；亮暗及最小窗口选中长描述实际离屏渲染核对；VS 设计时属性查询 Version/FileVersion=2.0.0.7。1208 个源码文件快照与当前代码、安装包/清单/SHA256 三方核验通过。
- 最终产物：dist/2.0.0.7_alpha/SunEyeVision-Setup-2.0.0.7_alpha-x64.exe（249475561 字节，19 插件，未签名）；SHA256 CBC615AD67D4DEEFAECA546C5EB752811246735A01F848FE9A27A38C210B4FD7；配套 release-manifest-2.0.0.7_alpha.json 和 exe.sha256。实际安装产品名 SunEyeVision 2.0.0.7_alpha，无 Preview。version.json.fileRevision=7；下一正常预检为 2.0.0.8_alpha，未实际增号。构建ID 20261008-185857-d7039607-cc66f9ce。
- 过程记录：首轮旧测试要求旁置 .bak，与新方案冲突，改为内部恢复路径并保留内容校验，失败未增号；6 号包通过 170 项测试后最终核查补正退出保存入口，6 号产物已可恢复地移入 artifacts/diagnostics/solution-manager-pre-final-2.0.0.6_alpha，不作为最终交付，不回退计数；最终以 7 号包为准。没有停止用户程序；尝试停止已确认构建ID的编译器时它已结束，实际没有终止任何进程。
- 人工验收结果/未完成：没有实际安装/卸载新包、没有写真实文件关联注册表、没有修改用户桌面配置、没有连接真实相机或外部通讯设备。另一台电脑首次启动/数据选择/旧配置迁移/重装定位/文件图标和双击、关闭取消/运行中卸载、真实设备及可见 VS 设计器仍待人工验收；不能把自动探针或离屏渲染称为现场安装验收完成。
- 已知风险或禁止改动：保持用户数据与既有源码变更；不自动清理桌面旧文件/旧安装目录；不改 Windows UserChoice/第三方默认应用；未签名包可能需用户确认运行；源代码未提交且清单如实记录。跨 AI 写入交接前仍须按 HANDOFF_TEMPLATE 创建说明明确的临时提交。
- 下一步：请使用 2.0.0.7_alpha 安装包做外机现场验收；首次启动选择例如非系统盘的 SunEyeVisionData，再新建/传递/重新定位/清空保存方案。旧包不会自动获得本轮实现。

以下为历史记录，当前交付状态以上述记录为准。

- 2026-10-08 Codex 正在完整实施解决方案管理器最终方案（覆盖上一记录中的“仍待实施”状态）。基线 main/d703960764e0f1cdd287c8398def759ad638251d；同工作目录当前仅 Codex 写入，保留全部既有改动，未提交、推送或交接其他 AI。实现范围：用户确认数据根目录、Config/user-settings.json 与 Solutions/Logs 分离、可重建定位及目录身份检查、旧桌面配置显式导入及迁移回滚、名称描述随方案传递、任意路径/相对资源载入、新建复制另存及改名保护、运行停止等待/未保存切换退出保护、失效图像清空持久化/重新定位/缓存取消、主题化虚拟化管理器和三种启动模式、安装版文件关联保护、一份内部上一保存版本恢复。窗口布局改为用户设置中的 WindowLayout，旧 LocalAppData/layout.config 仅读兼容；不创建 Backups/Config，不自动删除桌面文件或收集资源。44 项专项回归通过，亮暗及最小窗口实际离屏渲染已核对；82 项发布脚本回归通过，VS 设计时属性查询成功（可见设计器仍待人工）。正在正常发布 2.0.0.6_alpha，流水线负责全量 Release、集成测试、自包含程序探针和 ISCC；源码已冻结，发布结果待下一更新。未运行实际安装/卸载、未修改真实注册表和用户配置、未连接真实设备；另一台电脑首次启动/重装恢复/文件图标、真实相机通讯与可见 VS 设计器仍需人工验收。允许修改 src/Workflow、相关 UI、installer、tests、方案及协作文档；跨模块影响已覆盖启动/退出、主题消费、执行租约、资源快照及安装关联，未改变插件宿主依赖边界。跨 AI 写入交接前仍需按 HANDOFF_TEMPLATE 记录并创建说明明确的临时提交。

- 2026-10-08 应用户要求实现运行中禁止卸载：新增 ApplicationLifetimeGate，程序在服务/设备初始化前通过全局维护互斥门登记 Global\\SunEyeVision.Running 标记，静态持有直到进程结束，关闭取消/退出保存/设备清理期间不提前释放；维护期间拒绝启动，拒绝启动的 OnExit 跳过未初始化服务。Inno 设置 AppMutex，并在 InitializeUninstall 先持有 Global\\SunEyeVision.Maintenance，运行标记或旧版本 SunEyeVision.UI.exe 的 WMI 检测未确认退出则提示重试/取消；检查失败也阻止卸载；静默模式直接中止，不强杀进程。在 usAppMutexCheck 再检查，早于 usUninstall 的文件关联清理；取消及退出释放维护门。跨模块影响为程序启动/退出和发布卸载规则，未修改设备会话/工作流业务。定向运行标记测试 3/3（多实例、维护阻止启动、释放后恢复），脚本检查 82 项，小载荷真实 ISCC 脚本编译通过；完整隔离 Release 0 错误、1138 既有警告，发布定向测试 125/125；新 Release 程序隐藏无设备探针实测维护锁存在时 exit=1 且无探针输出，释放后 exit=0、版本 2.0.0.5/alpha/Dark，旧进程 WMI 查询可用。生成 dist/2.0.0.5_alpha/SunEyeVision-Setup-2.0.0.5_alpha-x64.exe，实际产品名 SunEyeVision 2.0.0.5_alpha，SHA256 38E1208F69A0F1DAEB5C5DA2A74BCA0C51A5131BD093FCD5D84328F6712D3116，19 插件、未签名，源码前后快照通过；计数器已更新为 5，下一正常预检 2.0.0.6_alpha（未增号）。未执行实际安装/卸载，未结束用户程序或删除旧残留；真实运行中卸载、软件取消关闭、卸载取消/重试后的文件/注册表不变及多会话/权限异常仍待隔离 VM/人工验收，不能宣称取消卸载现场验收完成。已安装的旧卸载器不会自动更新，须退出旧程序后安装新包才获得保护；解决方案管理器方案仍待实施。基线 main/d7039607，既有改动保留，未提交；跨 AI 写入交接前按项目规则创建说明明确的临时提交。

- 2026-10-08 应用户要求保存解决方案管理器最终完整优化方案：新增 `docs/01-方案设计/2026/10/08/解决方案管理器最终优化方案-20261008.md`，状态待实施。确定用户选择非强制系统盘数据根目录，Config/user-settings.json 与方案及安装路径分离，Solutions 仅为默认位置；不建 Backups/Config，不做资源打包/自动收集/复杂备份管理。方案携带名称描述及业务配置，任意可读路径可载入，资源单独传递后重配置；文件失效仍允许移除/清空且清空须持久化，旧异步加载不能恢复已清空内容；图像预览器现象根因待核查。方案包含已核查的另存 ID、复制参数、覆盖保护、切换保护、事件释放问题，启动/文件关联、旧路径迁移、四阶段实施、验收及按交接模板记录的边界。仅文档写入，无代码实现、构建、安装或桌面文件变动；基线 main/d7039607，既有改动保留；跨 AI 修改交接前仍需按项目规则创建说明明确的临时提交。

- 2026-10-08 关于页与完整安装显示版本（覆盖上一条“名称仅 SunEyeVision”的显示策略，固定目录/升级身份保留）：关于页重排为主题化、可调整大小/滚动布局，动态主题覆盖窗口、卡片、文字及按钮；从当前运行程序集显示完整发布版本/渠道/日期/文件版本/代码基线/实际 .NET 与架构/系统/程序目录，支持复制诊断信息，明确提交号仅代码基线。用户明确无在线更新服务，删除检查更新按钮及假“已是最新版”处理，不提供自动更新。关于窗口设置主窗口 Owner。安装名称、快捷方式、卸载条目为 SunEyeVision 2.0.0.4_alpha；AppId/ProgID/默认 C:\Program Files\SunEyeVision 不随版本变化；通过 Inno PreviousData 记录并仅精确替换上一版本快捷方式，兼容无版本与 Preview 旧快捷方式，不清理安装目录/用户数据。跨模块影响仅 UI 信息展示/主题消费与发布可见名称，未修改共享色板、SDK 契约和工作流。新增 AboutSoftwareInformation/AboutWindowTests，亮→暗→亮相关资源及离屏实际渲染已核对（测试实际顺序 Dark/Light/Dark）；定向关于测试 2/2，完整隔离 Release 0 错误、1138 既有警告，发布定向测试 122/122，脚本回归 75 项通过，VS 设计时 MSBuild 版本属性为 2.0.0.4。首次定向测试因共享测试目录残留 app-local hostfxr/hostpolicy 失败，两个生成文件可恢复地移至 artifacts/diagnostics/about-test-runtime-10e1b7bf53e5417fa0986bac6cb09296 后通过；未移动源码/用户数据。新包 dist/2.0.0.4_alpha/SunEyeVision-Setup-2.0.0.4_alpha-x64.exe 成功生成，实际版本资源 ProductName=SunEyeVision 2.0.0.4_alpha；SHA256 FB59852A2A5AD753FB19DD83C7E212F620739C742C4123BCC157713B742C29F2，19 插件、默认 Dark、源码快照前后不变，计数器成功更新为 4。未签名、未安装/卸载用户程序；安装后显示名/快捷方式升级/卸载和 VS 可见设计时、剪贴板现场人工验收仍待用户确认。基线 d7039607；既有工作区变更保留，未提交；跨 AI 写入交接前按模板记录基线并创建说明明确的临时提交。

- 2026-10-08 安装品牌修正（覆盖历史正常/Preview 分离策略）：alpha/beta/rc/stable 统一安装名、快捷方式、卸载名称为 SunEyeVision，默认目录 C:\Program Files\SunEyeVision；渠道仅留在版本与文件名。共享此前 alpha 已分发的 AppId 7A2E9C41，以保持该安装的升级身份；local 独立。安装器禁止复用旧 Preview 默认目录/程序组，仅清理明确的旧 Preview 快捷方式，不删除旧安装目录或用户数据。程序与安装器正常 ProgID 统一 SunEyeVision.SolutionFile，默认扩展名关联仍仅 stable 写入，local 独立；VM 验收脚本同步。跨模块影响为发布身份与文件关联，未改插件契约/工作流。发布回归 72 项通过，全量 Release 0 错误、1138 既有警告、UI 定向测试 120/120；程序探针 2.0.0.3/alpha/Dark/#FF1E1E1E。新包 dist/2.0.0.3_alpha/SunEyeVision-Setup-2.0.0.3_alpha-x64.exe（237.9 MB，19 插件，未签名）成功生成，实际版本资源 ProductName=SunEyeVision、FileDescription=SunEyeVision 安装程序；SHA256 26A43D0988D053D6F05EBD809E7EEB74D8C5265F19670DF323E8451395E12FCA。清单增加 displayName/defaultInstallDirectory/installerAppId/associationId；源码前后快照核验通过，fileRevision=3，下一正常发布预检 2.0.0.4_alpha（不增号）。未安装/卸载用户程序，旧 Preview 目录迁移、安装向导/快捷方式可见效果、VS 可见设计时尚未验收；建议用户先卸载旧 Preview 再安装新包并保留个人方案/设置。基线 d7039607，既有工作区变更保留，未提交源码；跨 AI 交接前仍须按规则创建说明明确的临时提交。

- 2026-10-08 按用户新要求恢复正常发布打包（覆盖前述只读固定版本/强制签名门禁方案）：默认入口按 version.json 渠道发布，产品与文件版本统一四段数字，渠道作为安装包下划线后缀；fileRevision 是上次成功发布号，默认正常发布预留 +1，失败不写回，成功移动产物后原子更新版本声明与当天发布日期。保留 -Local 临时包和 -Repack 原版本重打（源码快照必须一致、不增号），移除强制证书/VM/CHANGELOG/干净工作区门禁；签名、安装验收、RequireClean 为可选，清单如实记录未提交源码和 1196 个输入文件哈希，不自动提交/打标签。NuGet 包独立映射为 2.0.0-alpha.2，CLR/ToolContract 不变。默认正常发布自动流水线成功，生成 dist/2.0.0.2_alpha 下 exe/sha256/json 三个文件；安装包 SunEyeVision-Setup-2.0.0.2_alpha-x64.exe（237.9 MB，19 插件，未签名），SHA256 420098B9556567F25F7DF8300A41CA94AF8C4ED340725248FD029C631297F1B5。全量 Release 0 错误、1138 既有警告；UI 定向测试 120/120，发布脚本回归 52 项通过；程序探针为 2.0.0.2 / alpha / 2026-10-08 / Dark / #FF1E1E1E；完整源码前后快照与重打一致性核验通过，版本文件已更新 fileRevision=2、releaseDate=2026-10-08 并移除旧 channelRevision。只读预检显示下一正常发布为 2.0.0.3_alpha，确认未实际增号。使用说明见 installer/README.md。未安装新包、未执行可选签名/VM 验收、未创建标签或提交源码；基线 d7039607，保留原有工作区变更。

- 2026-10-08 首次安装默认暗色修复：`ThemeManager` 增加统一 `DefaultTheme=Dark`，构造初值和缺失/无效偏好回退改为该值，保留 Light/Dark 枚举数值和有效保存偏好的恢复逻辑；`App.xaml` 在资源末尾加载 DarkTheme，确保启动页及主题管理器初始化前为暗色，后续初始化仍移除初始字典并恢复用户选择。跨模块影响仅涉及启动资源与 SDK 兜底优先级，未改 SDK 色板或工作流执行。扩展无设备版本探针记录默认主题和启动窗口背景，实测 `Dark` / `#FF1E1E1E`。完整隔离 Local 打包流水线自动成功：全量 Release 0 错误、1010 既有警告，UI 定向测试 120/120，SDK 主题资源/契约/设计时契约回归 17/17，设计时 MSBuild 属性正常，安装包 SHA256 校验通过。新包 `dist/local/20261008-140731-d7039607-3d338dc8/SunEyeVision-Setup-2.0.0-local.1-x64.exe`（237.9 MB，19 插件，未签名），SHA256 `2F62372299E66608DAFCB29756E268009763D8004BFE9D5CEF17EF8204C2A10C`；旧 13:29 包不含暗色修复，保留归档。未覆盖现有用户设置、未自动安装或结束用户程序；用户可见首次启动、已有亮色偏好恢复及 VS 可见设计时仍待人工验收。基线 `d7039607`，源码未提交，既有打包与计划改动保留。

- 2026-10-08 打包与版本优化实施（主程序基线 `d7039607`）：发布声明改为 version/channel/channelRevision/fileRevision/releaseDate/schemaVersion；产品/NuGet 使用语义发布版本，Windows 使用四段 fileRevision，CLR 绑定与 ToolContract 保持 2.0 基线。打包只读版本与 CHANGELOG，移除强杀程序、复用陈旧 Release、全 dist 删除和打包后写回计数；新增 OS 独占锁、全新 staging、54 个依赖锁文件、隔离插件构建、自包含 win-x64 publish、原生退出码硬失败、程序无设备版本探针、逐文件哈希、正式签名/安装验收门禁、历史文件版本递增校验及 annotated tag。正式/预览/local AppId 与 ProgID 分离，预览不抢默认文件关联。新增双击 Local/Release 入口与 `installer/README.md`（含版本映射和流程图）。全量隔离 Release 构建 0 错误、1138 既有警告；UI 定向 Release 测试 120/120；脚本回归 36 项通过；VS 设计时 MSBuild 版本属性核对通过。实测修复解决方案级 RID 限制、Inno 数字产品版本限制以及版本字符串尾部填充导致的误判；最后一次自动流水线已通过构建/测试/publish/probe/ISCC，在收尾误判后使用修正后的共享收尾模块复核已有包并完成清单/哈希/移动，未再次全量构建。产物 `dist/local/20261008-132905-d7039607-2bca64e4/SunEyeVision-Setup-2.0.0-local.1-x64.exe`，19 个工具插件，SHA256 `F070656DCA3A84BC055C7774DA7702FE6338104187A891A8B715A0F6A0FCB5DD`；无签名、无正式标签、未安装。代码签名证书和隔离 VM 验收尚不可用，正式发布、实际安装/卸载、主窗口可见运行及 VS 可见设计时待验收。保留既有 plans 和协作记录改动，未提交本轮源码；跨 AI 写入交接前须按项目规则创建说明明确的临时提交。

- 2026-10-08 主程序与通讯管理项目代码同步：同步前确认主程序 `main=039e15bd` 工作树干净、无残留 cherry-pick，通讯项目 `feature/comm=c92d4804` 仅有不影响源码的 `plans/plan.json` 看板注记未提交。主程序此前已包含 OPC UA、统一绑定模型和接收解析链路三个通讯提交；本轮继续同步 `64286423`（接收历史上限 200 条与自动滚动）和 `fe30da15`（通讯发送事件与宿主发送服务），对应主程序提交为 `b86cbd75`、`d7039607`。通讯驱动、通讯管理 UI、通信应用服务、Workflow 通讯模型与通讯测试源码对比无差异；仅保留主程序独有的相机分类和图像源测试。全量 Debug 构建成功，0 错误、1136 警告；通讯／OPC UA／NumericUpDown／TCP Server 定向 Debug 测试 106/106 通过。未启动程序、未连接真实通讯设备，选项卡界面、VS 设计时及真实协议收发仍待人工验收。两工作区的 `plans/plan.json` 会被提交记录同步机制更新，当前计划元数据改动未纳入功能同步。

- 2026-10-01 用户反馈主程序最大化不能适配所有显示器：定位到 `MainWindow.xaml.cs` 的 `WM_GETMINMAXINFO` 使用主屏 `SystemParameters.WorkArea` 再乘当前 DPI，副屏/混合 DPI 会算错边界。改为 Win32 `MonitorFromWindow` + `GetMonitorInfo` 获取窗口所在显示器的工作区，以原生像素设置最大化尺寸及相对于该显示器的偏移；未改其他窗口或内容布局。全量 Debug 构建 0 错误、776 警告；UI Release 构建 0 错误、961 警告。不同屏幕的可见最大化、任务栏位置与 VS 设计时仍待人工验收；如仅有小分辨率内容溢出，需另查主界面列宽约束。保留既有未提交改动；跨 AI 交接前按规则记录基线并创建说明明确的临时提交。

- 2026-09-30 应用户要求重构建 Debug：确认无运行中的 `SunEyeVision.UI` 进程，执行 `dotnet build SunEyeVision.slnx -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -v:q -clp:ErrorsOnly` 成功，0 错误、865 警告；`dev/Debug/SunEyeVision.UI.exe` 与 `dev/Debug/plugins/SunEyeVision.Tool.NPointCalibration/SunEyeVision.Tool.NPointCalibration.dll` 均已生成。未启动 UI，物理点/物理角度标签的可见运行时验收仍待用户确认。保留既有未提交改动；跨 AI 交接前仍需按规则记录基线提交并创建说明明确的临时提交。

- 2026-09-30 应用户要求将 N 点标定触发输入与编辑点表的显示名称对齐 VM：按点改“物理点”，按坐标改“物理坐标 X/Y”，R 角改“物理角度”；界面提示和沟通基线明确此处物理点仍是 R 轴轴心编码器 XY、物理角度仍是机构 R 编码器角，内部字段/求解器未改。全量 Release 构建 0 错误、867 警告；N 点 Release 定向测试 20/20。检测到已有 SunEyeVision.UI 进程且无法读取其可执行路径，为避免打扰运行中的设备会话，未另起 Release 程序，运行时可见 UI 与 VS 设计时仍待验收。保留工作区既有未提交改动；跨 AI 交接前仍需按项目规则记录基线提交并创建说明明确的临时提交。

- 2026-09-30 用户反馈 N 点 UI 看起来几乎没变、与第 5 节方案差距大；核对后承认上一轮主要改文件/验证职责而主布局仍旧。已重排 `NPointCalibrationDebugControl.xaml` 基本/运行参数：触发与手动条件显示、按点/按坐标、点表进度、示教仅启用时显示两项字符、候选文件状态、显式“记录本次”；运行页默认只显示相机模式/自由度/权重函数三下拉和条件权重系数，机构角度/质量阈值及采样环境/同步绑定移至折叠高级组。`NPointCalibrationParameters` 的次数与示教开关改为通知属性，界面状态会随编辑/采点更新。通过离屏 WPF 渲染实际查看基本/运行页并修复“轴心 XY”标签截断；定向 Debug/Release 测试各 20/20，全量 Release 构建 0 错误、867 警告。Debug 插件已通过定向测试编译部署；真机/VS 可见设计时验收仍待用户现场确认。生成截图移至本任务可视化目录，仓库内临时目录已清理；保留原有未提交工作区改动。交给另一 AI 前仍按规则创建说明明确的临时提交。

- 2026-09-30 N 点优化方案实施（在既有未提交 N 点改动上增量修改，未覆盖其他工作区变更）：N 点工具移除内部验证点/阈值/直接发布，训练拟合及现场训练残差门槛通过后只更新 `SolvedUnverified` 候选；新增“N点标定验证”工作流工具、调试配置页、候选加载/摘要复核、独立平移与新 R 角样本残差检查、候选身份与图像域检查、`Verified` 活动文件原子替换和生产读取状态检查；三项 VM 运行参数改下拉，首期相机模式/自由度只允许上视+仿射，三种权重可选；移除 N 点页的无效后处理/运动规划项，采样环境与同步绑定折叠。更新 N 点方案第 10 节。N 点定向 Release/Debug 测试各 20/20；全量 Release 构建 0 错误、867 警告，全量 Debug 构建 0 错误、776 警告；Release 程序启动到可响应的主窗口后停止本轮进程；`git diff --check` 无空白错误。未做 VS 可见设计时、现场 9+3/独立验证或抓取精度验收；工作流运动/到位/同一步原子配对、生产抓取链加载、复拍自检仍待实现。本轮未创建临时提交；交给其他 AI 修改前仍须记录基线提交并按规则交接。

- 2026-09-30 应用户要求继续更新本地 N 点沟通基线（仅文档，未改代码/构建）：独立验证从 N 点工具中移出，改由另一条工作流加载 `SolvedUnverified` 候选文件，以新图像 Mark 点和同次机构 XYR 验证、形成报告，并对同一候选内容摘要原子发布 `Verified` 活动文件；N 点页不再配置验证点/误差阈值或承担发布。方案保留当前代码内置验证的事实并明确待迁移。用户补充 VM 运行参数的相机模式、自由度、权重函数三项均应显示为下拉框：前两项完整展示名称，首期只允许固定上视 + 6 自由度仿射；权重函数三项都可执行。已更新 `docs/04-工具设计/标定工具/2026/09/29/N点标定9加3联合标定与抓取链沟通基线-20260929.md`。交给另一 AI 修改代码前仍需按本文件规则记录基线提交并创建说明明确的临时提交。

- 2026-09-30 用户纠正 N 点一键自动标定架构：一键过程由**工作流节点链**实现，不新增独立的“一键自动标定设置”窗口或应用级标定会话编排器。已据此修订 `docs/04-工具设计/标定工具/2026/09/29/N点标定9加3联合标定与抓取链沟通基线-20260929.md` 第 3、4.3、5.5、5.6、7、8、10 节：图像源/特征节点与接收数据/协议解析节点向 N 点提供同次图像点和机构 XYR；运动目标及到位可由工作流上游通讯/控制节点或外部 PLC 完成；工作流重复执行 9+3 次，N 点每轮最多记录一组并输出进度/结果。步号、帧、读数与触发仲裁仍需已有工作流调度层扩展；当前通讯事件驱动和运动等待链未完成。仅文档修订，未改代码/构建；先前“独立向导/编排器”条目保留为被本条覆盖的历史记录。

- 2026-09-30 应用户要求更新本地 N 点 9+3 统一方案（仅文档，未改代码/构建）：修正 VM“物理坐标系参数”实际用于生成采样目标而非输出后处理的旧错误解释；在 `docs/04-工具设计/标定工具/2026/09/29/N点标定9加3联合标定与抓取链沟通基线-20260929.md` 写入触发获取/手动点表/运行参数的完整 ASCII 条件界面、独立一键自动标定向导、VM 差异及理由。自动会话由执行层串行规划和运动、配对步号/新帧/实际 XYR 后提交样本，N 点工具仅管理点表/联合求解/诊断；默认在平移第 5 点 XY 旋转并复拍自检。12 点训练合格后仅自动更新旁路候选文件，独立验证通过才原子发布活动文件；明确当前代码尚无编排、条件 UI、候选/正式文件分层或生产加载器。更新旧方案第 3、4.3、5、6、7、8、10 节，并保留先前实现记录为历史事实。交给另一 AI 修改前仍需记录基线提交并创建说明明确的临时提交。

- 2026-09-29 应用户要求构建当前 N 点 9+3 实施代码的全量 Debug：确认构建前无运行中的 `SunEyeVision.UI` 进程，执行 `dotnet build SunEyeVision.slnx -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -v:q -clp:ErrorsOnly` 成功，0 错误、992 警告；`dev/Debug/SunEyeVision.UI.exe` 和 `dev/Debug/plugins/SunEyeVision.Tool.NPointCalibration/SunEyeVision.Tool.NPointCalibration.dll` 均存在。Debug N 点测试 20/20 通过。未启动程序、未做真机采点或可见 UI 验收；需从 `dev/Debug/SunEyeVision.UI.exe` 重启后人工验证。

- 2026-09-29 N 点 9+3 优化方案第一阶段实施：直接替换旧两步/圆拟合为 12 点统一 8 参数轴心→Mark 联合方程；求解采用列主元 Householder QR，Huber/Tukey 采用 IRLS，增加退化几何、稳健权重与逐点残差诊断。样本表改为图像 XY、机构轴心 XY、编码器 R 角度及来源/时间信息；触发采集要求帧与机构读数时间窗，支持训练/独立验证点和停用历史；发布标定文件须独立平移+旋转验证点、相机/图像域元数据及误差阈值通过。固定上视、6 自由度仿射以外配置显式拒绝。调试 UI/CSV/结果显示按新契约更新，物理坐标后处理与运动规划分离。N 点定向测试 Release、Debug 各 20/20 通过；全量 Release 构建 0 错误（867 警告），`git diff --check` 通过。Release 程序启动冒烟进程已退出，启动日志到 `CreateWorkflowTab.Register begin`，未观察到 `MainWindow.Show`，**不算完整运行时 UI 验收**。真实机构原子 XYR 回读/静止确认、现场采点、VS 设计时与可见界面仍待验收；生产标定加载/抓取坐标计算/通讯发送未实现，不能宣称整条抓取链完成。详细状态见 `docs/04-工具设计/标定工具/2026/09/29/N点标定9加3联合标定与抓取链沟通基线-20260929.md` 第 10 节。当前工作区改动未提交，交给另一 AI 修改前需先完成交接与临时提交。

- 2026-09-29 N 点 9+3 基线按用户补充的 VM 运行参数附录统一：在 `docs/04-工具设计/标定工具/2026/09/29/N点标定9加3联合标定与抓取链沟通基线-20260929.md` 明确首期仅固定上视相机/6 自由度仿射，最小二乘、Huber、Tukey 三种权重均需真实实现，默认权重系数 20 的项目内无量纲阈值定义与逐点权重诊断，及物理坐标后处理、运动规划与联合求解严格分层。保留数学更正：已知 R 角时基础 8 参数方程线性，鲁棒模式采用 IRLS 而非强制 LM。用户要求先统一方案、暂缓实施，已撤回本轮尚未完成的插件源码尝试；本轮仅修改文档，未构建或运行程序。

- 2026-09-29 N 点 9+3 联合标定最终沟通基线：用户明确点表物理 `Xs,Ys` 是 R 轴轴心机台编码器 XY，`Theta` 是同次 R 编码器角，图像只取同一 Mark 中心 `u,v`；据此新增 `docs/04-工具设计/标定工具/2026/09/29/N点标定9加3联合标定与抓取链沟通基线-20260929.md`，确定 12 点联合线性模型、轴心→Mark 偏心、时序配对、主题化点表、质量门槛、抓取链与 TCP 偏置边界，并在 9/28 旧基线加覆盖提示。研发阶段直接替换旧算法/数据，不做迁移。本轮仅文档，未修改代码或构建；当前实现仍为旧两步算法。

- 2026-09-29 应用户关闭程序后的要求构建巴斯勒属性树修复：确认 UI 进程为 0 后执行 `dotnet build src/UI/SunEyeVision.UI.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -v:q -clp:ErrorsOnly`，成功，0 错误、1048 警告。核对 `dev/Debug/SunEyeVision.UI.exe` 和 UI DLL 时间为 14:31:31、DeviceDriver DLL 为 14:30:57；`git diff --check` 通过。未启动应用、未触碰相机，用户可见中文分类和 VS 设计时仍待验收。

- 2026-09-29 巴斯勒相机属性树中文分类：用户实机确认连接和参数操作正常，但 SDK 类别以英文复数名称显示。UI 补齐巴斯勒常见分类映射，保留未知厂商类别原名；MVS 适配器将原生类别名中的 `/` 转义为全角斜杠，避免 `Digital I/O Controls` 被误拆成两级；同时移除分类映射中重复键。因共享 Release 中间目录写入被拒绝，改用 `build_validation` 独立输出运行 UI 定向测试，11/11 通过；`git diff --check` 通过，临时构建目录已清理。未改相机参数键和值，未接触运行中相机程序；Debug 输出和用户可见界面、VS 设计时仍待验收。保留先前跨厂商触发配置的未提交改动。

- 2026-09-29 跨厂商 MVS 触发配置修复（基线 `12cf0a16`）：信捷相机 `TriggerSource=0/Software` 被旧硬编码误判为 `Line0`。新增 `MvsCaptureConfigurationParser`，MVS 在同一设备操作门内读取枚举原始值及设备符号名，以符号名归一化采集/触发模式和 FrameStart 选择器；未知值拒绝采集并记录 raw/symbolic/归一化诊断，不再用跨厂商固定数字。预览运行中改动 AcquisitionMode/TriggerMode/TriggerSource/TriggerSelector 时先停旧流、写入回读、按新配置重启，失败后保持停止；其他来源改动运行配置时不把旧取流任务误标为新模式。单次工作流软件触发改走已归一化请求的命令路径，不依赖预览取流标记。更新相机开发说明。全量 Release 构建 0 错误（815 警告）、UI Debug 构建 0 错误（907 警告），DeviceDriver Release 非真实硬件测试 11/11、跨厂商解析 Release/Debug 各 6/6、UI 工作流帧测试 Release/Debug 各 2/2 通过，`git diff --check` 无空白错误。未启动额外 Release UI 或争用真机；信捷/海康实时触发、VS 设计时和用户可见 UI 仍待人工验收。Debug 输出已更新，需重启现有应用加载新程序集。

- 2026-09-29 N 点标定 UI 对齐 VM 参数布局：标定点输入改为“按点/按坐标”单选，平移/旋转次数紧随物理角度；字符示教改开关并仅启用时显示输入字符/触发字符/事件序号，移除大段说明；自动生成物理点、相对坐标和文件更新改为开关；X/Y 优先改下拉框；无需绑定的次数、基准/偏移/角度等数据用 SDK NumericUpDown。标定点编辑窗由原生代码拼装 DataGrid 改为独立 XAML 主题化窗口，包含暗色标题/表格/按钮、中文类别、数字单元格编辑，保留 CSV 和确认/取消语义。全量 Release 0 错误（994 警告），N 点 Release/Debug 测试各 9/9 通过（新增窗口加载测试），N 点插件 Debug 构建 0 错误并更新 `dev/Debug/plugins/SunEyeVision.Tool.NPointCalibration/`，`git diff --check` 无空白错误。当前 UI 实例运行中，未另启 Release 程序或完成 VS 设计时/用户可见界面验收；须重启 Debug 实例后人工确认主题切换、数值编辑和布局。

- 2026-09-28 N 点标定工具箱不可见修复：原因是上轮只构建 Release，用户从 `dev/Debug` 启动，而 `dev/Debug/plugins/SunEyeVision.Tool.NPointCalibration/` 原先不存在。已完成全量 Debug 构建（0 错误，1122 警告），按插件目录与同名主 DLL 的发现约定核对 `dev/Debug/plugins/SunEyeVision.Tool.NPointCalibration/SunEyeVision.Tool.NPointCalibration.dll` 已生成；Debug N 点定向测试 8/8 通过。需要重新启动 `dev/Debug/SunEyeVision.UI.exe` 才会重新扫描插件，工具应在“标定”分类显示；可见界面仍待用户验收。未结束用户进程，也未触碰其他在途改动。

- 2026-09-28 N 点标定首版实施（基线 `4a2b8a52`）：新增独立 `SunEyeVision.Tool.NPointCalibration` 插件及三页调试界面、铅笔入口的标定点表编辑/CSV 导入导出、手动/触发采点、平移与旋转样本生命周期、固定相机上视的 HALCON 相似/仿射/投影求解、误差与当前点物理坐标输出、版本化标定文件。工作流节点通过 `IWorkflowResultCommitter` 将一次执行采集的有效样本提交回设计参数，避免运行快照丢失点表；重复触发序号与并发修订冲突会拒绝提交。N 点工具定向测试 8/8、全量 Release 构建 0 错误（1084 警告）、`git diff --check` 通过。当前机器存在运行中的 UI 实例，未另启 Release 程序争用相机；真实相机/通讯事件链路、VS 设计时和可见 UI 待人工验收。移动相机模式、稳健权重拟合和生产级不可变标定版本未实现，界面标注待实现并显式拒绝，不能当成 VM 全功能对齐。保留工作区原有图像源、通讯和计划文件改动。

- 2026-09-28 标定工具链方案沟通基线：根据用户提供的 VM 4.4 本机帮助与既有图像源/插件/工作流架构，新增 `docs/04-工具设计/标定工具/2026/09/28/标定工具链与定位抓取设计基线-20260928.md`。保留 VM 九个独立标定工具入口，以 HALCON 实现、独立标定结果契约、2D 固定平面抓取为首条交付链，并记录采集模式、持久化、UI、验收与待确认工位条件。本轮仅文档，无代码修改或构建；工作区原有改动保留。

- 2026-09-28 图像源本轮实施与验证（主线代码由 Codex 独占写入）：移除“定位到图像预览器”，调试配置分基本/高级页签；来源切换适配本地序列与相机；新增输出格式 Original/MONO8/RGB24、逻辑 SN 初始值与本地末张停止配置；参数通过工作流运行快照覆盖到插件，连续运行按成功输出递增 SN；本地图像改为成功执行后切换，失败不跳图并退避。全量 Release 0 错误（1137 警告），全量 Debug 0 错误（1233 警告），图像源工具定向测试 9/9、UI 持久化/停止定向测试 4/4 通过。Release 隐藏启动冒烟 PID 20148 已结束；日志 `logs/2026/09/28/SunEyeVision_095346.txt` 记录方案加载与 `LoadSolutionToWorkflowTabs begin`，但 15 秒测试期内未记录 `MainWindow.Show completed`，故不宣称主界面显示成功。真实相机、VS 设计时和用户可见运行时验收仍待测试。用户原有 `plans/*` 和 `.codebuddy/*` 改动未触碰。

- 2026-09-28 更新图像源 UI 方案：按用户确认移除目标设计中的“定位到图像预览器”按钮，本地模式仅显示当前图片/序列摘要，文件管理继续由主界面预览器负责。已同步修订 ASCII 示意、交互说明及实施记录；代码中的现有按钮尚未删除，留待后续 UI 实施。本轮只改图像源方案和协作记录，未构建；保留工作区其他改动。

- 2026-09-28 按用户确认更新图像源方案：本地图像的“添加文件/添加文件夹”由现有图像预览器负责，图像源只消费其运行快照，不重复实现入口。已核对预览器当前文件夹操作仅扫描所选文件夹顶层并一次性追加图片，不会持续监听；将“订阅图片文件夹”从图像源待办中移除，未来若有实时文件输入需求再单独设计。本轮仅更新 `docs/04-工具设计/图像源/2026/09/27/图像源工具UI与执行设计方案-20260927-200728.md` 及协作记录，未修改代码或构建；其他现有工作区改动保留。

- 2026-09-28 应用户要求构建同步后的 Debug：`main` 与 `feature/comm` 均在共同基线 `4a2b8a52` 上执行全量 `dotnet build SunEyeVision.slnx -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -v:q -clp:ErrorsOnly`，两边均成功，0 错误、1245 警告；通讯工作区先完成 NuGet 还原。两工作区的 `dev/Debug/SunEyeVision.UI.exe` 与图像源插件 DLL 时间戳均更新。未启动 Debug UI，也未重复运行测试；此前 Release 定向回归结果见下一条。主线现有 `plans/generate_weekly.py`、`plans/sync_plan.ps1` 用户改动未触碰。
### 通讯项目此前记录
- 2026-10-09 心跳设备下拉修复：基线 cef87553901c38f2fe4dcabb2ea05df19dab5ac9，feature/comm，Codex 单写入。真实 WPF 表格复现按钮已选中但 IsTargetOpen 未即时同步，导致 Popup 不打开（目标数据已存在）；按钮/弹层显式 PropertyChanged 更新，并明确 Popup 使用按钮所在行的 DataContext。新增实际窗口/弹层/TreeView 事件回归，覆盖 TCP、Modbus 地址、OPC UA 数据点反复打开及回填共享方案。123/123 串行通讯/OPC UA/相机回归通过；标准 UI Release 0 错误、1049 警告，Debug 0 错误、1047 警告。用户确认关闭程序后 dev/Debug 已更新，git diff --check 通过；未写用户实际设备，VS 设计时与用户实际界面验收待确认。未改协议、调度、SDK、主题或发布规则。交接见 HANDOFF_COMM_HEARTBEAT_PICKER_20261009.md。

- 2026-10-08 发送紧凑布局最终复验：标准 UI Release 构建 0 错误、1049 警告，Debug 构建 0 错误、877 警告；12/12 发送相关测试和 git diff --check 通过，dev/Debug 已更新。

- 2026-10-08 发送页紧凑布局：按用户截图移除与“发送结果”历史重复的单次 PreviewText 只读框；历史/HEX/清空/自动滚动保留。发送测试移到参数列表标题右侧，组装页同样置于组装列表标题右侧，两个页签各只显示对应入口；简短状态提示并入标题栏，详情只保留历史。原结束符配置移到输入配置，仍只对普通文本设备显示，消除列表下方整块测试区。发送命令、类型编码和方案保存行为未改。12/12 发送/发送 UI Release 回归通过，覆盖按钮位置、组装入口、忙碌禁用及不存在重复结果框；Debug UI 构建 0 错误、877 警告，用户关闭程序后 dev/Debug 已更新。标准 UI Release 结果见最终复验；实际布局及 VS 设计时待用户验收。保留其他工作区既有改动。

- 2026-10-08 响应折叠最终复验：标准 UI Release 构建通过（0 错误、936 警告），18/18 回归及 Debug 构建均通过，git diff --check 通过。dev/Debug 已更新，未写入用户实际设备。

- 2026-10-08 响应分组开关显示修订：按用户截图要求，方案加载/流程控制/相机响应关闭时对应配置项和说明 Collapsed，不占布局空间，标题、开关及组间分隔保留；重新开启恢复显示且不清空设备/目标/内容。关闭同步收起设备目标 Popup。新增 STA 断言覆盖三组独立折叠、开关连续操作、弹层关闭和原值保留，相关 Release 回归 18/18 通过。标准 dev/Debug UI 构建通过（0 错误、764 警告），标准 UI Release 结果见本轮复验；未改后台调度/协议/SDK/发布规则。用户实际视觉和 VS 设计时验收仍待确认。

- 2026-10-08 标准 UI Release 最终构建通过：0 错误、1049 警告。全量 Release（0 错误、779 警告）、Debug UI（0 错误、889 警告）及 122/122 串行回归的结果保持；事实基线待确认表已同步，忙碌跳过与 Modbus 编码不再列为实施阻塞项。

- 2026-10-08 触发/响应最终复验：122/122 串行通讯/OPC UA/相机执行回归通过，其中新增自动执行和 UI 专项 18 项。覆盖真实 TCP 无窗口触发、关闭方案取消、Modbus FC16 绑定地址拆分写入、响应顺序、持久化编号、忙时 Warning 且不排队、真实单模块参数快照及缺失上游拒绝、失败不误报完成；WPF 页签动态主题/字段回填/配置共享验证通过。最终全量 Release 构建 0 错误、779 警告；最终 Debug UI 构建 0 错误、889 警告，dev/Debug 已更新。警告数随增量编译范围不同，不宣称清理了既有警告。标准 UI Release 构建结果见本轮最终交接单。git diff --check 通过。实现交接：docs/collaboration/HANDOFF_COMM_AUTOMATION_20261008.md；本轮仅隔离回环/运行时控件验证，未写用户实际设备，完整应用和 VS 设计时人工验收待完成。

- 2026-10-08 触发/响应接入：用户已确认忙时跳过并 Warning、不排队；Modbus 按分隔符拆分、严格按类型编码、失败整条不写。新增触发/响应配置、后台真实样本解析与分派、同设备有序状态响应、稳定对外编号；导航双页签触发表和纵向响应三组已启用。后台复用接收处理器但使用独立无控件状态，配置实时共享方案且统一保存。指定流程/全流程、先加载再提交的方案切换和单模块执行已接入；与手动/连续运行共用非等待租约，单模块使用参数/图像配置快照，拒绝缺失上游结果。相机状态 500ms 观察，初次只建立基线；退出先停止通讯再释放相机。模块动作 SDK 无通用契约，选项禁用；事件到工作流输入/运行参数映射继续暂缓。全量 Release 首轮构建通过（0 错误、889 警告）；串行通讯/OPC UA/相机相关回归首轮 121/121 通过，最终结果见后续记录。并行广范围回归出现 4 个共享 WPF/单例 UI 超时，单独/串行均通过，不宣称并行通过。事实基线第 9 节已更新，禁止再把已确认的忙碌/Modbus策略作为阻塞点。保留 plans/plan.json 及工作区 OpcUaAdapter.cs 的 BOM 改动；本轮不修改 SDK/插件/发布规则。VS 设计时、完整应用和用户设备人工验收待完成。

- 2026-10-08 最终产物确认：生命周期初始化竞态和取消后状态回填修正后，Release 定向回归再次 32/32 通过；标准 Release UI 构建 0 错误、1049 警告，最终 Debug UI 增量构建 0 错误、764 警告，dev/Debug 已更新。前述 1047/990 为较早构建的警告数，随增量编译范围不同，不作为警告减少的结论。git diff --check 通过。实施交接记录：docs/collaboration/HANDOFF_COMM_HEARTBEAT_20261008.md。

- 2026-10-08 心跳首批最终验收：标准 UI Release 构建通过（0 错误、1049 警告），标准 Debug 构建通过（0 错误、1047 警告）。Release 定向测试 32/32 通过，其中心跳专项 9 项，包含隔离 Modbus FC16 写入绑定地址 40 的第二份内容 20。未连接或写入用户运行中的实际设备。用户设备、VS 设计时及明暗主题人工验收仍待进行。

- 2026-10-08 心跳首批实施：基线提交 c92d480480ee84c76aead7a32037d11acea77f8a，分支 feature/comm，执行者 Codex。用户明确数据类型按项目协议能力优化（VM 不支持 OPC UA）。新增心跳纯配置模型及解决方案列表、应用级非重叠周期调度、独立原生发送快照；心跳导航及七列表格、树形设备目标回填、单/多数据输入、原生类型选择、增删启停和共享方案实时同步已接入。开关交互本地恢复 SDK 绑定；设备/地址编辑更新快照；无效行不阻断其他行；关闭窗口不停止，关闭/切换方案先取消旧发送。触发配置和响应配置本批未实现，导航仍禁用；忙碌触发策略及原生数值响应编码仍待用户确认，工作流参数/输入映射继续暂缓。Release 定向测试 32/32 通过，涵盖心跳 UI 和隔离 TCP/Modbus 实际发送、原生发送和 OPC UA/生命周期回归；最终 Release 构建见后续验收记录。Debug UI 构建通过（0 错误、1047 警告），更新 dev/Debug。WPF STA 初始化和布局已测试，VS 设计时、明暗主题视觉及用户设备人工验收待确认。事实基线第 8 节已记录首批运行策略，不能冒充 VM 已确认语义。保留 plans/plan.json 用户既有修改，不涉及 SDK、工具插件、发布规则或工作流调度实现。

- 2026-10-08 心跳多数据布局与周期语义已确认：用户补图显示“发送内容”同一单元格内两个并排输入框，中间短横线不是范围、减法或报文分隔符；示例 0、1 不认定为默认值或类型。用户文字确认单数据逐周期重复发送、多数据逐周期轮流发送两份内容，不是单周期一起发送。已更新事实基线 ASCII、模式说明、定稿范围和待确认清单；首次发送时机、切换模式值保留、原生编码和失败／重连交替策略继续待确认。本轮仅文档，无代码改动或构建。

- 2026-10-08 心跳实际截图补入事实基线：主表列顺序确认“序号／设备／心跳类型／发送内容／时间间隔(ms)／启用关闭／操作”，行内开关和删除按钮、表下新增、单数据一个输入框、1000ms 数值框均已确认。设备地址回填为“2 ModBus通信-1 Address”，与响应页点号路径分别记录；候选图未列当前 Modbus 目标，帮助的去重约束继续沿用但不推断其他地址候选。单／多数据下拉已确认，多数据输入布局及编码类型来源、运行中编辑、重连交替仍待确认。更新 docs/01-方案设计/2026/10/通讯触发响应与心跳事实基线方案-20261008.md，替换旧待确认主图并收敛待补截图清单。本轮仅文档，无代码改动或构建。

- 2026-10-08 触发／响应／心跳沟通基线已保存：新增 docs/01-方案设计/2026/10/通讯触发响应与心跳事实基线方案-20261008.md，并在 VM 参考文档加入入口。记录完整 ASCII 线框及交互：触发双页签表格、四类命令、流程／全流程、字段选择和回填；内容查看中的运行参数／输入绑定按用户要求暂缓；响应同页纵向分组、各组独立目标、设备／地址树形下拉和一体回填；心跳尚无用户实际截图，线框和未知行为明确待确认。后续不得把早期触发左列表、响应多页签、独立地址控件、防抖／排队／启用列／发送一次当作已确认需求。配置共享、方案统一保存、稳定 ID、统一分隔符及表格居中继续沿用。仅文档，无代码改动或构建；下一步补齐其他命令、Modbus 响应附加字段、相机响应及心跳截图与运行语义。

- 2026-10-08 统一分隔符／表格居中最终 UI Release 构建通过（0 错误、1049 个既有警告），git diff --check 通过，dev/Debug 已更新。

- 2026-10-08 统一分隔符目录与表格居中：用户确认无需新控件；新增 CommunicationSeparators 共用只读默认目录与实例选项工厂，接收／发送事件保留独立当前值和自定义选项，默认值不变（接收逗号、发送分号）。新增局部 CommunicationUiResources，三个接收分隔符和两个发送分隔符共用 ComboBox 样式、Value 绑定与显示模板，替换接收比对页独立 TextBox 和硬编码 ComboBoxItem。发送参数／组装表的序号、名称、类型、测试值、结果、状态采用水平／垂直居中样式，控件保持可编辑铺满；接收及设备表格也补齐局部文字／下拉居中，保留 OPC UA 类型错误提示。不修改全局主题、协议编码、保存格式或 SDK。15/15 Release 定向测试通过，含真实主题下单元格对齐、共用选项身份、自定义值隔离、三个接收下拉初始化和方案持久化；标准 dev/Debug 构建 0 错误、764 个既有警告。实际布局及 VS 设计时人工验收待确认，Release 结果见后续记录；保留既有用户修改。

- 2026-10-08 分隔符显示补修最终 UI Release 构建通过：0 错误、1049 个既有警告，标准 dev/Debug 已更新。

- 2026-10-08 分隔符对象文本显示补修：用户实际截图证明仅设置 DisplayMemberPath 在本项目主题模板下仍回退为 record 调试字符串；主题 ContentPresenter 未传递该路径依赖的模板选择器。两处发送分隔符改用显式 DataTemplate/TextBlock.DisplayName，选项 ToString 同时回退为标签，选中值依旧绑定真实 Value。运行时测试现加载真实 CommonStyleResources，验证每个选项文本及选中项可见文本，12/12 发送相关 Release 测试通过。标准 dev/Debug 最终构建 0 错误、764 个既有警告；UI Release 构建结果见后续记录。VS 设计时与用户截图复测待确认；本轮未修改全局主题模板、发送协议或持久化格式。

- 2026-10-08 上述发送主题／分隔符最终 UI Release 构建通过：0 错误、1049 个既有警告；git diff --check 通过，标准 Debug 已更新。

- 2026-10-08 发送事件主题／分隔符修复：用户截图指出组装页签白底且分隔符下拉空白。页签局部 Style 原覆盖主题模板，现采用独立主题资源模板与选中下划线，文字／悬停／选中颜色动态更新，不改全局主题。原可编辑 ComboBox 使用的全局模板没有 PART_EditableTextBox，现两种输出共用带显示名称与真实值的非编辑选项（无、逗号、分号、空格、竖线、CR、LF、CRLF），默认分号可见，已有自定义值作为独立选项保留；选项集合保持稳定，避免自定义值更新时清掉选择，忽略控件切换期间的临时 null。最终 20/20 Release 定向测试通过，覆盖主题资源切换、页签选中、两处下拉同步、换行／空格／空值／自定义分隔符以及事件切换保留；用户已关闭程序，最终标准 dev/Debug 构建 0 错误（764 警告）。实际界面及 VS 设计时人工验收待确认，Release 构建结果见后续记录。保留既有用户修改。

- 2026-10-08 发送事件界面简化：按用户要求将测试值合并到参数列表（序号／名称／类型／测试值／数据结果／状态），移除重复测试参数表、折叠测试区、预览命令和发送页“保存配置”，仅保留实际执行写入的“发送测试”及内容／状态反馈。发送事件增删改继续实时捕获到当前 Solution.CommunicationSendEvents；去除发送配置对设备 HasUnsavedChanges 的误用，不再触发关闭窗口时的 OPC UA 保存／撤销提示，统一在保存解决方案时落盘。设备管理已有保存机制未修改。20/20 Release 定向测试通过，包含运行时 WPF 参数单元格实际编辑、无预览／独立保存按钮、单份参数表、即时共享配置、修改前后文件不变及显式方案保存恢复；UI Release（1049 警告）和标准 dev/Debug（764 警告）构建均 0 错误，git diff --check 通过。实际主题布局、设备发送和 VS 设计时人工验收待用户确认。保留既有 SunEyeVision.sln、plans/plan.json 和运行时文件 BOM 改动。

- 2026-10-08 发送事件实现：按用户提供的 VM 截图启用发送事件导航和列表，加号打开独立“事件管理”窗口，仅提供文本直接输出／组装输出；直接输出为绑定设备、分隔符、参数列表，组装输出采用输入配置／组装配置双页签，表头统一为“类型”。支持右键重命名／删除、参数与输出增删、稳定 GUID 输入引用、方案保存重载、折叠的预览／发送测试区、200 条发送历史及自动滚动。新增 Workflow 持久化定义与纯参数快照组装器、宿主发送服务和调用入口；原始 TCP／UDP／串口使用 UTF-8，Modbus 按地址块配置编码并执行 FC16、超容量整次拒绝，OPC UA 校验原生类型与写权限。字段删除同名重建不自动重绑，组装允许固定常量。Python 脚本按用户要求暂不支持；流程“发送数据”工具模块仍为独立后续接入项，SDK 服务契约注入尚未实现，插件不能依赖 UI 宿主。19/19 定向测试通过（含两种页面运行时初始化、绑定行和页签切换、真实方案序列化、TCP／Modbus 回环与 OPC UA 本地服务器写入）；UI Release 和标准 `dev/Debug` 构建均 0 错误。VS 设计时、实际主题布局和用户设备人工验收待确认。保留了用户已有的 `plans/plan.json` 与 `SunEyeVision.sln` 改动。

- 2026-10-08 接收事件解析结果面板：按用户要求采用与 Modbus 接收记录一致的滚动行为。解析历史原已限制最近 200 条并按最后一条序号递增，本轮补齐面板首次显示、重新展开、文本变化（包含事件切换和十六进制显示切换）的自动滚动，布局完成后定位末尾并合并滚动请求。UI Release 和用户关闭程序后的标准 `dev/Debug` 构建均 0 错误；本轮仅窗口事件与 XAML 修改，实际面板滚动及 VS 设计时验收待确认。用户上一张地址块记录截图已显示滚动条在底部、最新序号 #353，确认地址块自动滚动生效。

- 2026-10-08 Modbus 地址块接收历史：按用户要求将地址块历史上限从 512 改为 200，轮询和手动同步共用截断逻辑，仅保留最近 200 条；记录序号独立递增，删除地址块时清理对应计数。接收数据记录窗口打开及列表更新后，在布局完成时滚到最底部，合并连续更新的滚动请求，关闭窗口解除订阅。地址块定向测试 11/11 通过，UI Release 与标准 `dev/Debug` 构建均 0 错误；VS 设计时与实际窗口滚动的用户人工验收待确认。历史仍为运行时记录，不写入方案文件。

- 2026-10-01 OPC UA `String` 接收事件分隔符修复：用户截图中数据点值 `1,1,1,1,1,`，文本模式分隔符为逗号，但单条输出规则仍显示整串。根因是原生 OPC UA 标量路径对 `String` 强制要求仅一条规则并绕过文本分割。现原生 `String` 在文本模式下直接按现有字符长度／分隔符规则拆分，按已配置输出／比对字段从左到右赋值，保留尾部空字段；无数值强制转换，非 `String` 标量仍按原生类型与单规则处理。更改分隔符／长度／模式或新增字段时用缓存值即时重算，不伪造接收历史。更新接收来源方案。Release 定向测试 47/47，UI Release 与用户关闭程序后的标准 `dev/Debug` 构建均 0 错误；实际 OPC UA `test3` 界面验收与 VS 设计时待确认。

- 2026-10-01 OPC UA `test3` String 数据点读取失败：用户截图中助手 NodeId 为 `ns=2;s=通道 1.设备 1.test3`，本程序类型／值为空。现场日志 `logs/2026/10/01/SunEyeVision_130741.txt` 证实实际请求 `ns=2;s=s=通道 1.设备 1.test3` 并收到 `BadNodeIdUnknown`，根因是把 `s=` 前缀输入到只需标签名称的字段。添加／编辑窗口现接受原始标签名、`s=标签名` 和完整 `ns=数字;s=标签名`，预览并保存规范化 NodeId；若完整 NodeId 的命名空间与单独索引不一致则提示错误，保留完整 NodeId 中标识符字面量 `s=` 的可能性。读取失败时类型列显示“读取失败”，悬停显示错误。Release 定向测试 12/12、UI Release 和标准 `dev/Debug` 构建均通过；已存在的错误 test3 配置需用户双击编辑并保存，实际设备验收及 VS 设计时待确认。

- 2026-10-01 OPC UA 接收结果即时刷新：用户反馈新增输出项默认 `int` 与绑定数据点 `Boolean`／`Double` 类型不符，改正类型后仍要等数据点变化才显示。新增解析／比对项时按绑定点的原生类型初始化；切换绑定点、选中事件或修改字段类型后，使用运行时已缓存的最新点值重新计算结果，不伪造接收历史；断线时清除运行时点值缓存以免重连误用旧值。相关 Release 定向测试 52/52 通过，UI Release 与标准 `dev/Debug` 构建均 0 错误；VS 设计时和用户实际 OPC UA 设备界面验收待确认。

- 2026-10-01 OPC UA Boolean 接收事件：用户截图中设备数据点 `test1` 为 `Boolean=False`，而接收事件类型下拉没有布尔选项。接收事件两个字段类型下拉改为随绑定设备提供选项：OPC UA 增加 `Boolean`、`Double`，普通文本事件不出现 Boolean。OPC UA 数据点更新按设备 ID＋稳定点 ID 路由到接收事件，直接消费原生标量值、质量与时间戳，不经过 UTF-8 分割／强制转换；单点要求一条解析规则，Boolean 支持不比较／等于／不等于，类型或质量不符清空结果并 NG。同步修改帮助提示和目标绑定方案。相关 Debug 定向测试 49/49 通过，标准 `dev/Debug` UI 构建通过（0 错误、1045 警告）；VS 设计时和用户实际 OPC UA 设备验收待确认。OPC UA 数组／结构体、触发／回复执行链仍未接通。

- 2026-10-01 用户验收澄清：先前组装“内容／数据结果”空白的截图当时尚未绑定，并非已绑定后结果丢失。用户后续截图确认绑定 `解析列表.in1/in2` 后数据结果分别为 `1`、`65537`，与 Modbus 解析值一致。撤回本轮因误判而作的 OneWay/失焦改动，保留此前已部署并已验收的组装绑定实现；本轮未重新构建，运行中的程序无须关闭。

- 2026-09-30 组装绑定二次修复：用户再次提供事件持续 `OK`、`out1/out2` 可见绑定“解析列表.in1/in2”而结果仍空的截图，证明前次只同步控件选择事件不足。`ParamBinding` 编辑模式可能将显示文本写入 BindingSource，而运行时只按稳定字段 ID 匹配。组装项现将与当前可用源精确匹配的中文显示路径规范化为 `ReceiveEventInputs.inX` 并恢复 SourceFieldId，且成功组装前对缺失 ID 再解析一次；解绑仍清空身份。新增显示路径、空源但有显示文本的 Modbus 组装回归测试；独立 `build_temp/assembly_binding_fix2` Debug 定向测试 36/36 通过。用户关闭程序后标准 `dev/Debug` UI 构建通过（0 错误、1045 警告）；实际界面复测待确认。

- 2026-09-30 组装绑定数据结果为空：用户补充截图证明 Modbus 接收事件整体 `OK`、解析列表有值，但组装项绑定“解析列表.in1”后仍为空。根因是 `ParamBinding` 控件选中来源只更新控件自身的依赖属性，可见显示文本与行模型中的 BindingSource／稳定 SourceFieldId 可能脱节；组装处理器只按 SourceFieldId 取值。组装页现监听绑定选择及编辑失焦，显式同步路径和显示文本到行模型并解析稳定字段 ID；切换／解除绑定时清理旧身份。新增 Modbus 成功解析后的绑定结果与解绑回归测试，独立 `build_temp/assembly_binding_fix` Debug 定向测试 34/34 通过。用户关闭程序后标准 `dev/Debug` UI 构建通过（0 错误、1045 警告）；实际界面验收待确认。

- 2026-09-30 Modbus 区间状态反向修复：用户截图中实际值 `5`、界面区间 `0–6`，却出现“范围内 NG／范围外 OK”。比较函数对区间语义本身正确；比对列表中的上下限数字控件可能只更新可见数值，规则模型仍保留默认上限 `1`。现为字节比对区间控件增加 PropertyChanged 双向绑定及 `ValueChanged` 显式回写，确保模型、实时状态、方案持久化使用可见区间。新增数值控件绑定回归测试，覆盖 `5` 对 `0–6` 的范围内 OK／范围外 NG；独立 `build_temp/range_fix` 输出的 Debug 定向测试 33/33 通过。用户关闭程序后，标准 `dev/Debug` UI 构建通过（0 错误、1045 警告）；用户实际 Modbus 轮询验收仍待确认。

- 2026-09-30 接收事件比较规则修订：按字段类型区分 `string`（不比较／等于／不等于）和数值范围（不比较／范围内／范围外）；字节匹配数值规则另外支持等于、上升沿、下降沿。修复字节规则下拉显示“等于”但规则模型仍“不比较”导致实际值 5 被误判 OK：下拉选择显式写回模型，比较不满足保留结果并标 NG；沿按 0→非0／非0→0，首次样本仅建立基线。文本协议解析输出项也增加规则编辑、实时状态与方案持久化，文本比对共用新规则；UTF-8 字段仍保持 string，不为范围规则强制转数值。更新 UI 说明和目标绑定文档；Debug 定向测试 33/33 通过，全量 Debug 解决方案构建通过（0 错误、1001 警告），最终标准 Debug UI 构建通过（0 错误、762 警告）。VS 设计时、运行时布局与用户实际设备验收待确认。

- 2026-09-30 Modbus 字节规则再次复核：用户截图显示 UI 区间分别为 `2–3`、`0–3`，但持续轮询历史均为 `2,2`，与 `00 02 00 05` 不符。解析核心的同数据回归测试已证明正确结果为 `5,131077`；单独数字控件默认绑定测试也通过，因此不能再仅把问题归因于该控件本体。已在表格字节起止控件的 `ValueChanged` 中按可见数值显式写回规则模型，绑定显式采用 PropertyChanged 更新，并将 Modbus 解析历史扩展为实际区间、十六进制和十进制值（例如 `2-3[0005]=5`），使下一次界面验收能区分“编辑未写回”与“解析错误”。独立输出目录 Debug 定向测试 22/22 通过；用户关闭程序后标准 `dev/Debug` 构建通过（0 错误、1045 警告），实际轮询验收待确认。

- 2026-09-30 Modbus 规则字节终点显示与实际值不同步：用户把 `0–1` 改为 `0–3`，界面仍显示第一寄存器的十进制 `2`，而 `00 02 00 05` 正确结果应为 `131077`。根因是共用 `NumericUpDown` 内部对 `IntValue`／`Value` 使用 `SetValue`，会替换双向绑定，造成控件显示 `3`、规则模型仍为 `1`。已将控件内部键盘、增减、输入提交和双属性同步改为 `SetCurrentValue`，保留绑定；新增 IntValue、Value 双向绑定 STA 回归测试。独立输出目录 Debug 定向测试 21/21 通过。用户关闭程序后，标准 `dev/Debug` UI 构建通过（0 错误、1045 警告）；实际轮询及 VS 设计时验收待确认。

- 2026-09-30 Modbus 接收事件用户截图复核：规则列空白是因为字节比对样式在“不比较”时同时隐藏了范围控件和文本框；已改为 int/float 默认展示禁用的范围控件、string 默认展示禁用文本框，选“范围”或“等于／不等于”后启用对应输入。用户截图中设备地址持续更新但事件仍“未接收”，检查发现平时启动的 `dev/Debug/SunEyeVision.UI.dll` 为 14:04 旧构建，而新接收事件实现只在隔离目录编译；用户关闭程序后已将最新代码标准 Debug 构建到 `dev/Debug`（15:01:28，0 错误、917 警告）。代码已核对 FC03 轮询批次按设备和地址块 ID 路由；实际重启后的绑定与设备结果仍待用户验收，不能仅凭编译声称已收到事件。

- 2026-09-30 Modbus 接收事件 `int` 解析：按用户确认的“接收事件数据结果”范围，FC03 轮询和手动数据同步均按设备 ID＋地址块 ID 路由；寄存器展开为高低字节，按规则闭区间及顺序拼十六进制，再转十进制 `int`，据等于／不等于／范围／不比较更新结果与 OK/NG。无效字节区间、长度错误和未支持类型清空旧结果并 NG；规则编辑后的即时状态重算使用同一整数比较逻辑，改动区间／顺序／类型先清旧结果。Modbus 原始消息不再走普通 TCP 文本路径。当前 `float`／`string` 字节规则、ASCII 转换和工作流触发／回复未接通。Debug 独立输出目录的接收处理与 Modbus 工作区定向测试 28/28 通过（含比较 NG、四字节换序和规则即时重算）；实际设备及 UI 人工验收待确认。

- 2026-09-30 OPC UA 接收事件数据点下拉实时刷新：原下拉绑定 `Communication.OpcUaDataPoints` 普通 List，新增数据点时不通知 UI；改为绑定设备工作区的 `ObservableCollection<OpcUaPointRowViewModel>`，保留 `Point.Id` 稳定选择值。行视图模型补 `TagName` 变更通知，编辑数据点名称也可刷新下拉。解析配置与比对输入配置两处均已修正。独立输出目录的 Debug 定向测试 9/9 通过，含名称变更通知回归；用户运行中界面实际验收待确认。

- 2026-09-30 OPC UA 接收事件绑定下拉精简：解析配置和比对输入配置两处仅显示数据点名称，移除括号中的 NodeId；选中值仍为数据点稳定 ID，NodeId 仍可在设备管理查看。同步修订目标绑定方案文档。标准 Debug 构建被运行中程序锁定 `dev/Debug/CommonControl.dll`；改用独立 `build_validation` 输出目录构建成功（0 错误、917 个警告）。界面人工验收待确认。

- 2026-09-30 接收来源与目标绑定方案修订：新增《通讯接收来源与目标绑定最终方案-20260930.md》，并同步 VM 参考、字段最终方案及实施计划；明确目标绑定适用于所有需要定位设备内数据的协议，而非仅寄存器协议。接收事件 UI 改为按协议显示第二绑定项：Modbus 地址块、S7 内存地址（临时表达式）、CIP 属性（目标目录尚缺）、OPC UA 数据点（稳定 ID，显示名称和 NodeId）；TCP/UDP/普通串口不显示。切换设备／修改设备协议／删除 OPC UA 点会清除失效引用，Modbus 地址清理仅作用于 Modbus 设备；S7/CIP/OPC UA 当前接收事件运行时尚未接入，不应作为已实现能力验收。UI Debug 最终构建通过（0 错误、1045 个警告）；目标映射、持久化及接收处理相关 Debug 定向测试 24/24 通过；实际界面和设备人工验收待确认。本轮未执行 Release 构建，保留工作区既有改动。

- 2026-09-30 字节匹配－协议比对 UI：新增独立的字节长度比较、ASCII 数据开关、规则匹配逻辑和逐条规则的字节起止位置／类型／顺序／比较值配置；组装配置继续共用现有页。寄存器类设备接收事件补“绑定地址”：Modbus 选择稳定地址块 ID，S7 暂以地址表达式输入（当前无 S7 地址目录）；切换设备或删除地址块时清除失效绑定，并隔离 Modbus 下拉与 S7 输入的双向绑定，避免隐藏控件覆盖地址。配置随事件实时写入方案对象并可序列化。运行时字节匹配尚未实现，显式返回 NG 和未接入原因，避免复用文本解析误报成功。相关 Debug 测试 16/16 通过，最终 UI Debug 构建 0 错误、762 个既有警告；实际界面和设备人工验收待确认。未接入 Modbus/S7 接收事件执行链。

- 2026-09-30 文本接收类型与字符长度修正：依据用户确认和 VM“字符长度比较”截图，两个接收配置页将误标的“字节长度”改为“字符长度”；整条 UTF-8 文本在分隔和字段解析前比较 Unicode 字符数，失败清空结果并标 `NG`。文本模式分隔后的字段保持 `string`，不再把 `"4"` 强转为 `int/float`；配置为数值类型时报类型不匹配，结果为空、状态 `NG`，二进制类型解析不变。更新解析回归测试和参考文档；相关 Debug 测试 16/16 通过，UI Debug 构建 0 错误、1043 个既有警告。用户实际界面人工验收待确认。

- 2026-09-30 接收事件列头校正：解析列表、协议比对输入列表和组装列表的 `DataType` 列头均由误写的“设备”改为“类型”；绑定设备仍在列表上方单独配置。核对 VM 帮助中“解析后数据的类型”及 `ABC,2` 的 int 输出示例，文档区分原始 TCP 文本与解析字段类型；暂不根据 string 成功的截图改变字段类型契约。UI Debug 构建通过（0 错误、760 警告），用户界面人工验收待确认。

- 2026-09-30 用户验收修正：输出／解析列表 item 数不作为字符长度比较依据，也不要求等于报文分隔段数。`CommunicationReceiveProcessor` 改为只在必需字段不足时失败，按列表顺序解析前 N 段并忽略多余段；字符长度开关仍独立检查整条报文（含分隔符）。新增单字段接收 `4,3,4,5,6,7` 和独立长度校验回归测试，同步修订参考、最终方案和实施计划。相关 Debug 测试 16/16 通过，UI Debug 构建 0 错误、1043 警告；用户实际界面验收待确认。

- 2026-09-29 接收运行时阶段 3 第一批：修正字符长度比较为分隔前整报文校验；严格 UTF-8、字段数、类型转换与数值／文本规则校验，失败清除上一轮字段值并记载 NG 原因，成功后一次性更新字段及组装结果；字段增加运行时 `Value`，组装不再覆盖持久化 `Content`。设备快照携带接收结束符，TCP 客户端／服务端（逐客户端缓存）和串口按结束符组帧，未启用时仍按原始读取块交付。新增解析及 TCP 组帧回归测试；通信相关 Debug 测试 34/34 通过，全量 Debug 构建 0 错误、846 警告。待用户在 Debug 程序人工验收；事件发布、工作流触发和无结束符流协议定界未包含本批。

- 2026-09-29 文档基线更新：新增《VisionMaster4.4通信管理参考与差异分析-20260929.md》，核对本机帮助的设备、接收、发送、触发、响应、心跳和示例章节；明确帮助目录不等于软件导航、文本长度是分隔前整报文校验、接收结果原子发布和本项目 `OK/NG`／稳定字段身份决策。同步修订字段最终方案与实施计划；本轮仅文档，无代码修改或构建。

- 2026-09-29 实施第一批：通讯解析/比对事件、解析项、输出项和组装项增加稳定 GUID；AvailableDataSource 增加可选 SourceFieldId；组装绑定自动解析并保存字段身份快照；字段重命名和删除优先按 FieldId 更新/失效绑定；删除后新增 item 使用最小可用显示名称空洞且不复用 GUID。新增 3 个字段身份回归测试通过。UI Release 构建通过（0 错误）；UI 全量测试 62 通过、6 个既有 SolutionLoadRegressionTests 失败，失败与本次改动无关，单独新增测试 3/3 通过。
- 2026-09-29 第一批全量构建：`dotnet build SunEyeVision.slnx -c Release --no-restore` 通过，0 个错误、848 个既有警告。

- 2026-09-29 实施计划：已根据沟通基线制定《通讯解析字段与统一绑定模型实施计划-20260929.md》，按“影响面审计 → 通讯字段稳定 ID → 组装绑定与显式数据源 → 真实解析/持久化 → 脚本所见即所得适配 → 子程序及其他数据源 → 清理兼容逻辑”分阶段实施；当前建议从阶段 0 开始，先确认模型、序列化和运行时入口。

- 2026-09-29 沟通基线：已保存《通讯解析字段与统一绑定模型最终优化方案-20260929.md》，统一稳定 GUID 字段、可修改显示名称、引用快照、显式数据源、脚本/子程序/通讯解析输入输出模型，以及新增、删除、重命名和旧配置迁移规则。补充确认：脚本要求所见即所得，正常情况下脚本生成字段名与显示名称一致；FieldId 只用于稳定绑定，名称修改通过安全重构同步脚本引用；当前为方案基线，后续按实施顺序推进。

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

- 2026-09-29 输入配置重命名绑定修复：补齐接收事件加载时旧数据源的稳定 `SourceFieldId`，新增输入时统一生成带字段 ID 的解析列表数据源；重命名绑定同时按稳定 ID、旧路径前缀和历史名称兼容匹配，并同步更新组装配置中的绑定路径/显示文本，解决输入配置改名后仍显示旧 `解析列表.in1` 的问题。UI Release 构建成功，0 个错误；`CommunicationFieldIdentityTests` 3/3 通过。运行时持久化和脚本字段迁移仍按后续实施计划推进。
- 2026-09-29 通讯事件配置持久化第一批：新增 Workflow 层 `CommunicationEventDefinition` 及输入/输出/组装字段模型，在 `Solution.CommunicationEvents` 保存事件配置；通讯管理器启动时恢复事件、字段稳定 ID 和组装绑定元数据，保存时回写当前配置。新增序列化回归测试 1/1，通过 UI Release 构建和全量 Release 构建，均 0 个错误。运行时 Value/Result/Status 接入、失效绑定提示和脚本适配仍待后续阶段。
- 2026-09-29 通讯接收运行时解析第一批：复用 `CommunicationRuntimeManager.DataReceived`，按接收事件绑定设备过滤数据；文本模式按分隔符或字符长度切分，解析输出/协议比对输入更新 `Result` 和最后一次 `Status`，支持字符串、整数、浮点和等于/不等于/范围规则；组装输出按 `SourceFieldId` 跟随比对输入结果。UI Release 构建通过，0 个错误。字节匹配、脚本事件、跨消息缓冲和真实协议执行链仍待后续阶段。
- 2026-09-29 修复接收事件无反应：设备接收日志正常但事件 `SelectedDeviceId` 为空时，运行时过滤条件不会命中；新建/加载事件在仅有一个通讯设备时自动绑定该设备，当前运行中的空绑定事件收到数据时也自动补齐绑定并标记待保存。UI Release 构建通过，0 个错误；多设备场景仍要求在事件详情中明确选择设备。
- 2026-09-29 接收事件绑定匹配增强：运行时处理除设备 ID 外兼容旧配置中误保存的设备名称，并在收到数据但没有任何接收事件命中时记录设备及当前事件绑定值，便于区分“接收链路正常但事件未绑定”和“解析处理失败”。UI Release 构建通过，0 个错误。
- 2026-09-29 TCP 文本/二进制解析第一批：通讯事件新增 `DataMode`、`ByteOrder`、`ByteOffset` 持久化配置；界面增加文本/二进制、大端/小端和起始偏移设置；字段类型增加 Int16/UInt16/Int32/UInt32/Float32；运行时按固定顺序解码二进制字段，文本模式继续按分隔符/字符长度解析。UI Release 构建和全量 Release 构建均通过，0 个错误；字段身份测试 3/3、持久化测试 1/1 通过。二进制字符串固定长度、字节匹配规则和脚本事件仍待后续实现。
- 2026-09-29 修复单设备历史绑定失效导致解析无反应：设备接收窗口已经证明 `DataReceived` 回调正常，但事件保存的 `SelectedDeviceId` 可能是旧 ID/旧名称；单设备场景现在允许迁移该失效绑定到当前唯一设备，并在收到数据后回写当前 ID。多设备仍严格按用户选择的设备匹配。UI Release 构建通过，0 个错误。
- 2026-09-29 按用户要求撤销单设备自动迁移：接收事件现在只按界面实际保存的 `SelectedDeviceId` 精确匹配当前通讯设备，不再根据设备数量、设备名称或旧绑定猜测；新建事件也保持未绑定状态，必须由用户在“绑定设备”下拉框中选择。未命中时仅记录诊断日志，不修改用户绑定。UI Release 构建通过，0 个错误。
- 2026-09-29 构建配置纠正：用户当前运行和验收的是 Debug，已重新执行 `dotnet build src/UI/SunEyeVision.UI.csproj -c Debug --no-restore -m:1 -p:UseSharedCompilation=false -v:q -clp:ErrorsOnly`，并确认 `dev/Debug/SunEyeVision.UI.exe` 已更新；后续本轮通讯 UI 验收以 Debug 输出为准。
- 2026-09-29 解析结果历史与十六进制显示：接收事件新增运行时解析历史，按序号和时间保存最近 200 条解析记录；“16进制显示”切换历史记录显示原始接收字节的十六进制内容，关闭后显示解析结果；清空按钮同时清除历史。历史不写入方案文件。已按用户要求仅执行 Debug UI 构建，0 个错误。
- 2026-09-29 按用户要求清理历史显示兼容兜底：移除 `MessageText` 单次结果链，解析区域统一使用 `ParseHistoryText`，历史追加、十六进制切换和清空均只更新正式历史模型。后续只验证最新 Debug 程序，不保留旧显示路径。

- 用户日志显示两类独立问题：Modbus 目标端口被拒绝属于运行时设备连接失败；通讯管理器打开失败的 `connectionId` 异常来自 WPF 加载接收事件右键菜单时的样式事件连接。
- 将接收事件的 ContextMenu 从 ListBoxItem 样式 Setter 移到实际 DataTemplate 内的 Grid，右键选中逻辑改为从原始事件源向上查找 ListBoxItem；避免 WPF 样式加载阶段触发 connectionId 异常，同时保留重命名/删除功能。
- UI Debug 构建通过：748 个既有警告，0 个错误。
- 2026-09-29 修复解析历史链路中的残留事件变量引用：`ProcessReceiveEvents` 显式传递接收时间和原始字节，历史记录可连续追加并正确显示时间/序号；移除旧 `MessageText` 后重新执行 Debug UI 构建，0 个错误。当前仅保留 `ParseHistoryText` 正式显示链路。
- 2026-09-29 修复协议比对误报：比较规则统一去除首尾空白；数值“范围”严格按最小值/最大值判断；字符串仅支持等于/不等于；未知规则不再默认判定通过。截图中的实际值 `3`、范围 `0~1` 现在应显示“匹配失败”。Debug UI 构建通过，0 个错误。
- 2026-09-29 优化比较规则交互：数值类型可选“不比较/等于/不等于/范围”，等于/不等于显示单值输入，范围显示最小值/最大值；string 类型自动移除“范围”选项，仅保留“不比较/等于/不等于”，切换到 string 时原“范围”自动改为“等于”。比较状态统一显示 `OK`/`NG`。Debug UI 构建通过，0 个错误。
- 2026-09-29 按最终规则收敛比较选项：数值类型仅支持“不比较/范围”，string 类型仅支持“不比较/等于/不等于”；类型切换遇到无效规则统一回退为“不比较”。Debug UI 构建通过，0 个错误。
- 2026-09-29 接收事件详情布局重构：解析、比对输入、组装配置均改为 Grid 的 Auto/*/Auto 行布局，上方可见配置按实际高度测量，中间 DataGrid 占用剩余空间并自行滚动，底部“＋”独占固定行；删除按消息展开状态硬编码的列表 MinHeight/MaxHeight。字符长度启用与消息展开同时发生时按钮不再被列表裁剪，消息折叠时列表自动扩展。Debug UI 构建通过，0 个错误；窗口实际视觉验收待用户确认。
- 2026-09-29 修正列表填充过度：三个接收事件列表改为按实际内容高度显示，最大高度由可用视口高度扣除“＋”按钮空间决定；少量行时按钮紧随表格，大量行时表格在可用空间内滚动。保留字符长度与消息展开时的动态剩余空间分配。Debug UI 构建通过，0 个错误；窗口实际视觉验收待用户确认。

## 2026-10-09 主界面汇报图片素材
- 用户授权直接从软件抓图。复用历史 PrintWindow 方法，启动当前 dev/Debug/SunEyeVision.UI.exe 与既有 1.solution，截图成功；未执行流程、修改源码、构建或发布。程序 PID 38096 保留运行。
- 原图 artifacts/reports/20261009/main-window-original.png；内置 imagegen 添加五区汇报标注，输出 main-window-annotated.png。界面显示20个工具插件、2台相机、流程尚未运行，未宣称全部验收完成。


- 用户配置工作流并打开主界面后重新抓取 PID2076 的真实窗口，原图 main-workflow-original-v2.png。按用户要求仅主图与功能标注，补相机管理/通信管理/全局变量，最终 main-workflow-ppt-annotated-v2.png；使用内置 imagegen，管理三入口同组框标注，无标题/日期/页脚说明。未操作工作流或源码。
