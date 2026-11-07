# FT-M03-CLR-OPERATION-PROPAGATION-01 有限修复

- 基线：07b0413fc95cc88ae76c263e7437da054fc442be。原a45封存不改。
- 缺陷：直接SDK常量参数定义已保存Conversion，但ParameterResolver在实际CLR转换/形状适配时丢失策略或通过默认Automatic提前拒绝。不是Workflow typed Plan正例失败。
- 修改：ParameterResolver从binding.Conversion贯穿标量、标量提升、单项解包及所有数组元素；BindingContractValidator.Accept显式接受同策略，仍委托唯一ParameterBindingService编译器；实际转换仍唯一NumericStorageRules/BindingClrAdapter checked实现。未知枚举在入口及纯转换器拒绝；自动规则不放宽、单项只允许长度1、数组全量成功才提交。
- 文件：NumericStorage.cs、ParameterResolver.cs、BindingContractValidator.cs/Canonical.cs、ExplicitConstantConversionTests.cs、本交接。不依赖未提交Blob/资源/主窗代码；必要未提交依赖0。
- 新开发验证：result-bridge-01/sdk-build07.log真实0错365警告13.69秒；explicit-conversion02.trx实际8通过0失败0skip。涵盖actual async Constant double12→actual int（persisted long hint）、Automatic拒绝、fraction/overflow拒绝、数组全量/失败零body与ActiveRuns0、原配置不变、scalar promotion/singleton nullable unwrap、未知枚举same-type拒绝、空double[]自动拒绝/显式成功。此前explicit-conversion01真实6通过2正例失败保留，针对其旧Automatic形状门修后才跑02。
- 随后同策略Validation入口加可选参数供Workflow readiness使用，缺省仍Automatic，源检查待隔离fresh构建一并核。相关Workflow调用处尚随Blob主线WIP，不是direct SDK修复必需依赖，后续主线全量会带入；不得冒它已在本有限提交。
- 独测：原a45 defect-explicit-conversion-propagation.json五正例FAIL保留。协调按本提交fresh独立worktree构建完整SDK测试闭包/新bin及源封存，交独测只回受影响五正例和必要负控，不复制主树WIP、不单换DLL、不重复原19已过。
- 主线继续Blob正式声明/资源reader/公共Presenter承载，最终整方案仍未完成。
