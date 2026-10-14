# 位置修正关闭和绑定候选回归修复交接

- 基线提交：a108da498ae21bda155bbf05806b9368d9fa3fe6。
- 当前分支：feature/comm。
- 当前执行者：Codex单写入，无并行写入、代理提交、推送或跨AI交接。
- 任务目标：按用户要求先修复旧方案回归，再继续B3接入。
- 允许修改目录：D:/MyWork/SunEyeVision_V2-comm；保留用户与前批全部修改，不修改用户方案文件、不操作设备或另一项目。

## 已完成

位置来源原本有AvailableDataSourceConverter支持非空路径，并非完全没有对象／字符串适配。空字符串反向转换及null显示转换回落base，导致新统一校验拦截；补空文本与null映射，非空非法路径仍报错，原有非空来源和索引格式不变。ParamBinding.BindingSource使用SetCurrentValue，保留选择、清除和恢复时的WPF绑定；测试实际验证新来源写回模型及已有索引路径，不仅验证红框消失。

执行和确认仍调用提交校验。标准及自定义窗口关闭提交失败时明确提供继续编辑或关闭、放弃未提交控件编辑的选择，默认继续编辑。关闭放弃会取消待编辑状态、恢复控件显示并抑制Closed／Unloaded再次持久化。提示明确已实时写入参数的值不回滚，不把当前逻辑伪称完整草稿取消；B3尚未接入。

DataSourceQueryService普通入口默认连线上游，B2合法来源计算只用于排除自身、下游和非法候选。已有合法无连线来源保留在“当前绑定来源（无连线）”，不隐藏真实引用；主窗口通过统一绑定描述提供该清单。协议解析、协议组装、发送编辑器显式启用其他合法来源，导航树按“上游节点／其他可绑定节点”区分，上游优先。类型过滤后再建树，不生成没有适配字段的节点组。节点分组包含稳定身份，避免同名合并；导航标题不作为绑定名称或路径。正式输出字段的Group信息补回，保留执行信息等业务分组。

## 已修改文件

- SDK执行参数：AvailableDataSource.cs、ParentNodeInfo.cs、DataSourceQueryService.cs、ArrayTreeFormatter.cs。
- SDK控件：ParamBinding.cs、ParamBindingBase.cs、ToolDebugControlBase.cs、ToolEditorCommit.cs、新增ToolEditorClose.cs。
- SDK窗口：DefaultDebugWindow.xaml.cs、CustomDebugWindow.cs。
- Workflow：OutputBindingChangeService.cs补既有来源清单和契约分组。
- UI宿主：MainWindowViewModel.cs注入已有来源委托；没有启用B3服务。
- 插件：ProtocolParse／ProtocolAssemble／SendData的Editor.xaml仅显式启用其他候选。
- 测试：新增BindingUiRegressionTests.cs六项；B2候选测试改为显式扩展入口，并断言普通入口不扩大。
- 既有主方案、实施计划、CURRENT_TASK和本交接。

## 已执行构建与测试

- 254项专项通过，0失败：UI141、SDK102、脚本9、注册表2。
- 新增六项覆盖空位置来源、已有索引路径、选源双向绑定保留、无效非空路径拒绝、关闭的继续／放弃路径、正常关闭不提示、候选类型与分组以及既有无连线来源保留。部分测试在同一用例内覆盖多个断言场景。
- 证据artifacts/validation/binding-ui-regression-20261014中的ui-regression.trx、sdk-regression.trx、script-regression.trx、registry-regression.trx。
- 标准全量Release：dotnet build SunEyeVision.slnx -c Release --no-restore，0错误1025警告。
- 标准UI Release：dotnet build src/UI/SunEyeVision.UI.csproj -c Release --no-restore，0错误1064警告。
- 常用Release已构建更新，最终实际启动探针退出0、24工具，CircleDetect及四通信、Script、Subroutine正常；证据release-toolbox.json。探针不初始化硬件通信。
- DesignTimeBuild的TargetPath正确，不等于真实VS设计器验收；git diff --check通过。

## 人工验收结果与边界

尚未运行用户那份实际旧方案，也未手动操作Visual Studio设计器或高DPI界面。没有据此宣布旧方案已验收；用户需检查原圆查找的位置来源显示／选源、原方案执行、无效编辑关闭的退出选择，以及普通和通信工具的候选树。

关闭放弃不回滚已实时写入模型的内容；错误配置不因此获得执行资格。没有全局忽略校验、替换原来源或自动选择新节点，也没有恢复样本锁定或旧报文有效期。

## 下一步

优先收集旧方案回归验收并处理残留问题，再接入B3节点配置提交服务的Default／Custom窗口生命周期、各执行入口及统一撤销历史。此前核心和宿主网关仍为未启用入口的实现，不将本批关闭修复当作B3全部完成。文档维护技能已同步实际顺序与边界。
