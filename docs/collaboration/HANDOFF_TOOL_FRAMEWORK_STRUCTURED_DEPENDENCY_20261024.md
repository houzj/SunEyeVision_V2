# 首依赖内部交接：FT-A01/A02/A03/M03

- 基线提交：6c2def23ce062d2a7802ed8edddd40de113ccfa3；期间外部出现b2214579，来源待核，本实施者未执行该提交，不回滚。
- 当前执行者：Codex，唯一产品代码写入及后续git操作者。
- 任务目标：原48批准项依赖，继续整包；本交付仅可执行基础合同子集。
- 允许修改目录：现有Plugin.Contracts及其foundation消费者/验证Host。
- 已修改文件：TypeRef/Values/ToolContracts/GeometryValues/StructuredValues；foundation消费者合同4.0声明；Host声明嵌套图像所有权；Verification真实结构/几何/资源用例；相应README。
- 已完成：单一声明类型、计数、结构深冻结、七类几何纯数据、逻辑坐标框架严格校验、共享真实PointPairMath纯几何Session调用、嵌套Image retain/raw拒绝/释放。
- 未完成：完整BindingPlan、Model/Contour资源、每轮真实图像来源关联、实际代表工具迁移、整包性能和验收。
- 已执行构建与测试：Release Foundation --no-restore，5.00秒，0警告0错；现有Verification新增真实调用用例，共34PASS/0FAIL/0BLOCKED。
- 固定收件：artifacts/validation/tool-framework-contract/structured-dependency-02/source、bin、input-manifest.json、binary-manifest.json、receipt.json、local-report.json、build.log。输入前后漂移0。
- 人工验收结果：尚未执行，最终同一Release五方向集中一次，不向用户散发局部清单。
- 已知风险或禁止改动：无关locks原有dirty非本轮restore；不暂存不回滚。程序集管理继续等主控。内部33样例不等生产整合同通过。
- 下一步：独测从固定副本验本子集；实施继续同原依赖的绑定/资源及Host链，不停交接。

- 独审修复：普通编辑配置拒绝可达borrowed Image常量，真实释放负控通过，绑定路径保留；正式AssetRef/version/owner配置正路仍待实施，不将此拒绝当作完成ShapeMatch。
- 几何覆盖仅七种纯数据合同，非七类ROI交互通过，不包含新增Ellipse交互。
