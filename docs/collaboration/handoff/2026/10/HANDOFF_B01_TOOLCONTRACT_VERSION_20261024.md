# AI 协作交接单：FT-B01/B03有限版本合同候选

- 基线提交：97f1326c42dbfe75fb516731be9df0ec76ed2da7；独立detached worktree b01-toolcontract-version。
- 当前执行者：Codex；本tree唯一writer，未push/merge。
- 任务目标：ToolContract3.0唯一常量，同源WriteCodeFragment元数据；与Foundation4.0独立。
- 允许/实际产品修改：ToolContract.cs、Directory.Build.targets、ToolContractVersionContractTests.cs，仅3文件。
- 已完成：Major3/Minor0；IsLiteral生成引用同源const；旧2.1/未来minor/异major/缺metadata精确拒绝和诊断测试。
- 构建与测试：owned离线还原；定向ToolContractVersionContractTests7PASS/0FAIL/0SKIP，原与final TRX保留；真实ImageFilter build03成功0错；实际生成C#引用const；实际DLL一份[3,0]metadata与host3.0一致，未构造/执行工具。
- 原失败：build01验证侧HALCON路径缺失；build02验证侧target-time Reference改写错误；两日志均保留。仅修owned验证配置，build03成功。
- 未完成：全工具重编、真实PluginManager旧2.1/缺attribute拒绝且ctor/execute0与3.0注册、全量Release/真实部署运行、最终资源依赖闭包。由主实施者统一最终同包验收，不以此7项替代。
- 人工验收：未执行GUI/VS/用户手验。
- 禁止改动：未改loader/ALC/载体/编译器/产品版本/CLR AssemblyVersion/Foundation；不修改主checkout。
- 输入与产物：本tree artifacts/validation/b01-toolcontract/receipt.json，含3源码快照、生成源码、实际SDK/tool/test DLL及TRX精确SHA。
- 构建身份：构建HEAD为97f+上述3文件dirty，不能冒称产物内stamp为后续提交。
- 下一步：唯一主实施者评审有限commit并合入；在其新owned最终包重编所有工具、完成真实loader负控与Release运行。
