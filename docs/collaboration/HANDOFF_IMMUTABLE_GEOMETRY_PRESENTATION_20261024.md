# 不可变几何结果显示有限接线

- 基线：f1feb5b02a84387d6404318051e64d1f30c1be04；唯一实施者Codex。
- 范围：原J02/J03/M01/M03，Contracts只读值reader、纯展示模型及现有Presenter，无载体/资源持有机制修改。
- 实现：只读reader仅对正式Geometry声明返回闭合不可变GeometryValue；Scalar/资源/容器不能通过此API取出。模型读取该不可变值；现有表格显示七类几何的坐标、坐标Frame、长度单位，角度明确rad。Polygon只读Count形成有界摘要，不遍历/复制顶点，不以字符串作绑定权威。
- 验证：geometry-presentation-01，UI-build01实际0错1192警告37.29s；新增2项真实Presenter测试PASS/0skip，七类值在de-DE下坐标格式仍Invariant、单位明确；100000顶点展示分配<256KiB且reader返回同一不可变Geometry实例；错误Scalar声明取Geometry拒绝。与既有资源不持有规则保持一致。
- 未闭环：Polygon顶点逐页展开、资源元数据显示、树图选择、七类交互/性能、完整固定后继包及独验仍原范围；本片非几何交互或三投影整项完成。
- 旧C67 immutable包不回写；后继新root继续，最终Release/人工VS统一一次。
