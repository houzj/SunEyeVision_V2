# A11 原B01/B03发布准入与注册事务源码接缝

- 基线提交：3fccb9a746cc286bb4fc06897530205f68a62813（前置原WIP保存点eb8416e0）。
- 当前分支：main；执行者：Codex，唯一共享树产品写者。
- 任务目标：原批准B01/B03的同发布权威校验、整DLL注册原子性、serializer代际刷新、完整工具集合发布门。载体/ALC设计继续等待主控，不在此片。
- 允许修改目录：原Core、Plugin.Infrastructure、UI探针、installer/构建规则及定向测试；无关锁文件/文档dirty不纳入。
- 已完成：整DLL构造/metadata/参数准备在锁外，Tool→Parameter→Bridge同持锁引用发布；duplicate/stale/失败不覆盖旧工具。临时IDisposable实例释放，读catalog不在锁内反射；初始化不在参数gate日志。四serializer facade随generation刷新。
- 唯一权威：build产生app/release-payload.json schema1，所有文件SHA、managed完整身份与直接SDK Tool声明；同payload对象复用outermanifest。签名/清理后seal，ISCC前后drift检查；缺坏清单failclosed，无runtime扫描自批准。
- FD按CLR真实Core/WPF来源与正式deps证据；SCD按SDK RuntimePackAsset与publish最终winner精确source/identity/SHA。Resources卫星按SDK catalog exact path纳入。Default已选host允许同token/culture且实际版本>=请求，实际host仍须等于批准身份/SHA；本地shared候选须和实际host完整身份及SHA相同。BAML主身份仍严格版本。SDK runtimepack副本只在SDK/NuGet conflict处理后从插件copylocal按source剔除，不按System前缀，不改真实私有NuGet闭包。
- 已执行构建与测试：mutable owned根artifacts/validation/tool-framework-contract/a11-registration-01。FD full11 0错1284警告，toolbox03 PID48684自然0/22；实际runtime03 PID59420自然0，Script/Roslyn执行marker、ProtocolParse BAML/错误99桥拒绝、throwlogger/22，payload17826C3F…3118E逐SHA不变；shared02实际Default绑定及local冲突2PASS。四facade generation01 1PASS；root-separator01 1PASS；原atomic02 6PASS/private01双顺序2PASS。新authority13编译0错264警告12.71s，受影响authority03自然0/11PASS0skip（四SCD损坏负控+FD框架local SHA负控及六原子注册回归）。原metadata解析publisherderived负控见专用fixture。
- SCD原full03/publish04/seal05通过，toolbox06正确拒绝runtimepack9与host10冲突；scd02新输出full03 0错1269警告139.77s，首按来源过滤时机过早令NuGet8 loser复现，前检保留，未运行第二个冒通过probe。已改为conflict后过滤，真实Script copylocal32且Immutable不在列表；须新干净输出完整构建/publish/实际运行并固定，不把此前mutable DLL说成当前源码闭包。
- 未完成：A11最终精确source/bin不可变固定、FD/SCD实际新身份复验、独立验收及其矩阵；此提交只是明确可复核源码边界，不是整B01/B03/47项完成。16–17源码/18–20 fixed原预测保留。
- 人工验收结果：本片无用户/VS人工结论。最终同一Release集中一次清单。
- 已知风险或禁止改动：noncollectible ALC不能回滚任意插件构造外部IO/静态线程；不承诺卸载。不覆盖旧FAIL、旧C67/几何/数值/空合同包，不重已闭验证、不动未知dirty。
- 下一步：完成新干净SCD规则实测，当前源码精确固定送独测；随后审合已父核并直交独测的Editor c795+dd6d及Shape候选6ef，按原资产合同继续。Editor原E06/E07真实保存FAIL保持开放。
- 精确归档构建补充：development seal显式接收构建SourceRevisionId；嵌套归档不会把父checkout后继HEAD误记为本源码来源。SourceRevisionId空时保留普通checkout Git获取。此后继只改seal来源传递，最终固定将以其精确commit重编；原29e边界保留。
