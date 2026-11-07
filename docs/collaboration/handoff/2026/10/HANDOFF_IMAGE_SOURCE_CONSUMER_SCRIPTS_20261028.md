# 图像源消费者正式回归脚本迁移交接（2026-10-28）

- 独立工作树workflow-overview、分支codex/image-source-tests，父85caa1f3。仅修改授权四个verification脚本及本单，不改产品、测试项目或共享夹具，不构建程序。
- 开始前只读比对D:/MyWork/SunEyeVision_V2同名文件：S4两脚本字节SHA相同；O06/S3原始SHA不同仅CRLF/LF，规范化文本逐字一致。完成前再对主仓与85caa1f3原基线核对，四份仍无语义增量，没有覆盖主线C13新断言。

## 集中迁移

1. O06RoiBindingCases：ImageLoad节点实际创建RuntimeConfiguration；通过正式ImageSourcePolicy字段投影装载绝对文件引用、起点及SN，退出CreateParameters/反射FilePath/SourceKind。直接执行与保存重开执行都构造WorkflowRunRequest.sourceInputs，地址由冻结资源resolver解析；SDK消费端引用改output.image。保矩形、圆、参数广播/索引、缺失拒绝、错物理帧、显示字段误导、非活动分支、屏蔽/检测、run身份、保存重开及CircleDetect原断言。
2. S3RuntimeWorkflowCases：原SDK源替为正式Runtime生产者，从其实际Schema取output.image端口并建立现正式OutputReference。Run助手按本次flow真实源设置产生ImageSourceRunInput，不借NodeParameterOverrides为源注入。原Blob设置覆盖测试仍使用其原覆盖入口、所有面积/资源/异帧/保存等断言保留。
3. S4FullChainCases：真实工具箱Drop额外核图像源Runtime；主界面源条导入走一次AddImages(selectFirstAdded:true)，核其正式Runtime引用与起点；迁ImageInfo到唯一UI.Shared类型。实际UI调度仍由生产MainWindow生成sourceInputs，不在脚本旁路调度。原Blob上下游/自动绑定/历史/正式结果完整范围不减。
4. S4RuntimeEditorCases：新源节点添加前以实际Runtime策略写入本地引用/起点/选择/SN，退出SDK FilePath反射。实际主面板切源核恢复同一Runtime列表，避免再次AddImage制造重复引用。Blob窗口、原字段数量/可见来源、私有草稿/自引用、捕获同步重绑、保存关闭等全部保留。

## 范围与验证

- O06本体仍是历史SDK Blob/ROI链，本批只迁图像源，不擅自替换Blob或降低其断言；若现主线已退出该Blob合同，该脚本仍需主线决定对应ROI链职责，不能用源迁移宣称它已运行通过。
- 四脚本搜索已无旧图像源CreateParameters、FilePath/SourceKind反射、OutputImage端口或Panels.ImageInfo。不是全项目旧SDK退出证明。
- 实际sourceInputs DTO和新Runtime来自root集中候选；当前独立分支旧产品源码不能单独编译这些消费者，不增兼容入口。
- 纯静态Roslyn ParseText四文件0语法错误，git diff --check通过；没有语义编译、正式Release运行或人工证据。请root整批集成后在唯一主仓同源Release按原各轨全范围验证，保存所有失败，不套旧成绩。
