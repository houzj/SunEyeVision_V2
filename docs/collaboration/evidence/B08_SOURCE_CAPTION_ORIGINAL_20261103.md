# 原来源名称与索引后缀：2026-11-03静态核收

本次复用既有VM 4.4反编译材料；未启动VM或SDK，未以截图推断执行/保存语义。用户新截图作为可见差异，原托管代码作为显示规则证据。

| 对象 | 原件位置及规则 | 当前差异与实施边界 |
|---|---|---|
| 普通VmIO来源名 | VM.Core/VM.Core/VmIO.cs:661–676，ModulePathName返回Root.ID + 空格 + Root.UniqueName + 点 + AppLocalizationService.GetLang(UniqueName)；常量与无Root各有独立分支 | 本方宿主候选builder/FormatReference使用中点和斜线组路径。应依据原来源身份及显示声明统一投影，不凭截图把所有嵌套路径去父级；正式MemberFieldPath不能因显示修改而丢失。 |
| 普通参数选择 | Apps.XmlParser.ParamTab/ButtonSelecter.cs:347–391，ClickCommandExecute选VmIO，旧CurValue有左括号则保其后缀，否则ModulePathName追加[]；全局变量管理分支同样处理。无按scalar省略后缀的分支 | ReferenceFieldSession.AcceptedText目前对scalar All省略[]；此为明确显示差异。固定/动态索引、变量及旧文本保留须同一完整C07单元核对。 |
| 图像选择 | Apps.XmlParser.ParamTab/EnumerationG.cs:11–35，CurValueA去掉首个[起后缀再比较赋值/通知 | 图像输入与普通订阅并非相同后缀规则。输入来源可不显示[]；不能让所有框都机械追加。 |

当前源：src/UI/Services/Editing/DeclaredReferenceCandidateBuilder.cs，src/UI/Services/Editing/Nodes/RuntimeNodeConfigurationControl.cs，src/Plugin.Wpf/Adapters/References/ReferenceFieldSession.cs，src/Plugin.Wpf/Fields/DeclaredFieldEditor.ImageSource.cs。

原件文本SHA256（本次实际读取）：
- ButtonSelecter.cs：A04C04DC557328269A06D67C194F6C3A64B68659ACC1F76E4DE778F5DA858931，与已有B08索引证据一致。
- EnumerationG.cs：6665B85DCDBB5727F77CC62FCC398FF49A71ACDB124047CA37AEE7516711C332。
- VmIO.cs：15E1832243422B6BFA2714F300B796D54C365950573639EB1039C5DF58E99CB0；既有20261031报告记录另一份文本SHA53E2...对应VmModule.cs，不混同文件。

绝对来源根：D:/MyWork/REA逆向测试/analysis/decompiled；前三项分别在VM.Core、Apps.XmlParser.CapabilityAudit对应目录。旧索引证据B08_ORIGINAL_INDEX_EVIDENCE_20261031.md保留；显示字符串不是native消费、持久化字节或同索引执行语义的证明。

状态：证据已核，产品未修。当前C05正式同版验收不插入展示改动；图像输入和普通参数订阅的显示/文本编辑在C07完整单元集中调整，相关宿主投影边界已告知工作流，不要求另一工作区停工。