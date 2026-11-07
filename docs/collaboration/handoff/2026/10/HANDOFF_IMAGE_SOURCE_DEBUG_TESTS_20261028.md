# 图像源调试与正式源输入补充回归交接（2026-10-28）

- 独立工作树workflow-overview，codex/image-source-tests；前批e7b87a6a已由root集中合入511eda48。本批仅新增ImageSourceDebugInvocationTests与专用交接，同步ImageSourceCases选择清单。不写产品/共享项目/旧夹具，不构建私有Release。
- 依据root未提交冻结生产链：RuntimeDebugInvocation.Capture(resourcePaths/cameraService)→PrepareDebug(私有Runtime配置/资源地址/SN)→同一ImageSourceInvocationInputs.CaptureAsync→实际ImageLoad Runtime；正式WorkflowRunRequest.sourceInputs使用同一helper。所有测试经真实Runtime注册、实际PNG/HalImage、正式CameraFrame与服务合同，不复制取图算法/Runtime/结果模型。

## 实质回归矩阵

1. 本地debug冻结私有草稿、基目录A与SN；捕获后修改草稿/正式历史配置/baseB，仍读A像素和原SN，不回写正式历史/OutputCache/LastResult。
2. 空本地列表、运行选择排除当前起点、无基目录三项拒绝；即使存在defaultImage也不回退，且不调用上游结果reader。
3. 直接WorkflowExecutionEngine使用sourceInputs成功，本地像素/实际Domain与debug一致；请求字典外改不反改冻结输入，正式与debug分别保真实Run/Frame身份。
4. 直接引擎缺sourceInputs，以及尝试NodeParameterOverrides代注入地址/SN均拒绝，不借默认图伪造成功。
5. camera debug核冻结设备/超时/CorrelationId、帧号/时间戳/SN和实际typed像素快照，正式配置后改不影响本次；acquired CameraFrame在结果返回前恰一次释放，输出独立可读。
6. 错设备、非typed图像、无设备、服务异常四项拒绝，按真实归属释放，无成功生产发布；另核capture前取消不调用服务，等待中取消晚到frame被释放。
7. 直接正式执行借入cameraFrames路径仍由宿主持有帧，run Dispose不释放借入帧，也不额外调用服务；宿主最后Dispose才释放。
8. 调试快照仅执行一次；开始前Dispose拒绝执行，执行中Dispose/第二次Execute拒绝且原任务排空，结束ActiveRuns=0；结果关闭后已借pixels/domain reader仍可读，不回写正式结果。

测试分为8个Fact与2个Theory（3+4行），实际15个展开用例；ImageSourceCases新增整个测试类，原非法候选7行加本批拒绝7行完整展开为14行，记录真实总数量，不套原45成绩。

## 接缝及验证

- UI测试项目须能引用现有HalAdapter公开HalImage（如现传递项目不足，由root集中补引用）；NSubstitute已是现有依赖。没有工具CLR模型依赖或新测试项目变动。
- 新测试依赖root本批尚未回填到本独立分支的sourceInputs DTO、Capture两个可选参数；不能单独旧基线编译，更不能为测试保兼容接口。
- 所有数据写S2_CASE_ROOT；未设置时写当前正式HostDebugRoot/test-data子目录，使用少量PNG并finally清理，不复制程序/算法库。
- 当前仅Roslyn ParseText纯语法与git diff检查，没有语义编译、运行、硬件、VS或人工证据。可控相机服务+实际typed图像验证宿主合同和所有权，不代表相机硬件验收。
- root集中合入生产与测试后，按唯一主仓同源正式Release重新构建和运行，保留所有失败；前批成绩不能套本批。
