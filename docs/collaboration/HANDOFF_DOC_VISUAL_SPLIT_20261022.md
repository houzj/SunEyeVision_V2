# 文档分页与图形化交付

- 基线提交：3a5dc6dd54f372699a450ad7862b52c961cd2797；原有产品dirty保留，非稳定产品验收基线。
- 当前分支：main
- 当前执行者：Codex，文档唯一维护者。
- 任务目标：剩余能力/优化/审计拆分，既有32专题图形化，按界面找文档，中文专用目录，关联名可读。
- 允许修改目录：架构对照文档目录、对应artifacts/validation生成/校验脚本、文档专用协作记录。
- 已修改文件：自研架构文档维护/分类正文与公共资源；日期根的78个兼容入口；新旧两份生成清单、中文目录布局清单与路径资源；对应生成器/校验器；DOC_SYNC_STATUS。精确文件见document-library-layout.json。
- 已完成：151个新/升级页面，3界面场景18热点，49能力、12优化包/41固定整改、20工作包、13UI、42原证据、162工具目录与218菜单出现项原口径保留。执行/状态/时序/数据/Owner/9工程依赖图；12拟议合同及24未运行用例保持。关联名以主题为主，编号降为次要检索。
- 未完成：产品实施/24运行验收未执行；源模型漂移只登记不语义升级；逆向文档拆分要求已送达，其执行结果不由本任务宣称。
- 已执行构建与测试：不构建产品。check-visual-document-library：151页、390语义路由、3191本地资源/链接、898锚点、453宽度检查、18热点、键盘/历史/查询/放大/打印，零脚本错误与远程请求。check-document-library-compatibility：78旧独立入口，主图到工具查询与ROI查询通过。secure-export原有加密回归通过，分类ZIP202条目齐全。无关HTML字节保留与不收集测试通过。
- 人工验收结果：用户产品运行/设计器验收未进行。Codex已目视桌面/手机/Owner/依赖/时序和打印截图；修正了重复标题、证据/目标标签、筛选hidden样式、旧平铺路径和打印对比度。
- 已知风险或禁止改动：原版/独立实现/拟议/未知严格区分；不改src/tools/产品测试/项目发布规则/CURRENT_TASK/逆向工作树；不新增后台轮询或原42语义进度。
- 下一步：用户从新index按界面查阅；后续只按明确交付增量维护。新的页经受管理清单归档，不扫描任意同日HTML。
- 生成链：build-new-entry-report → 原专题生成/导航 → build-architecture-doc-site → build-visual-document-library → document-library-layout.pack → build-secure-export。只改本批文档时可直接node artifacts/validation/build-architecture-doc-site.cjs，再调用导出生成器；无需重生成产品或全站分析。
- 交接：需求文件REVERSE_DOC_SPLIT_REQUIREMENTS_20261022.md已按用户授权发送“架构逆向与实现对照文档”，含关联命名补充；未写其工作区或向其它聊天派发。
