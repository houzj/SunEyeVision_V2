# Blob 扇环继承“内径/外径”与半径换算静态补证

## 结论

本次追到的 VM 4.4 **继承输入叶 → 24 字节扇环记录 → 扇环对象虚表参数**路径中，内、外值没有发现除以 2 或乘以 0.5；可见的传递是按原 float 数值复制。SDK 对扇环 SetInnerRadius/SetOuterRadius 的参数描述为“内半径/外半径”，因此在同版 ABI 对应前提下，应以输入值作为半径处理，不应仅因简体中文标签“内径/外径”而自动除二。这不证明所有生产者、坐标修正或后续算法内部都不进行缩放，亦未进行原机数值差分。

## 标签与输入

`Applications/Module(sp)/Group.xml:1989–2047`：`ROIAnnulusInnerRadius` 的 DisplayName 为 `CircleInnerRadius`，操作目标是 `roiAnnulusInnerRadius`；`ROIAnnulusOuterRadius` 的 DisplayName 和目标均为 `roiAnnulusOuterRadius`。这两个 `ButtonSelecter` 选择 float 来源并通过 `SetFilterSourceOperation` 绑定，不是给数值做乘除的数字输入转换器。组合来源路径 `Region2 → SetCombinationSourceOperation(InputROIAnnulus)` 最终亦映射同名叶。

同版 `Applications/Lang/zh-cn.xaml:7787–7788,9272` 把以上 key 显示为“内径/外径”；`en-us.xaml:7672,9135` 分别显示“Outer Radius”和“Inner Radius”。中文标签本身不足以证明数学上是直径。Blob `IMVSBlobFindModu.xml:55–65` 的两个输入叶均为 float，名称是 InnerRadius/OuterRadius。

## 原生数值传递

1. Blob 模块 `18001bf70` 的每项读取以 `VM_M_GetFloat(...,"roiAnnulusInnerRadius",i,&record+8,...)` 和 `...OuterRadius...&record+12...` 取得叶值；内半径读取非成功时该值清零，外半径非成功时返回错误。随后角度分支可处理角度范围；没有对两个已成功读取的半径值做÷2。检查依据 `annulus/0x18001bf70-18001bf70.{c,asm.txt}`；编译器使用 24 字节栈记录，`18001c70e` 调 `180019ad0`。
2. `180019ad0` 将该 24 字节记录作为三个 8 字节片段写入向量；无浮点计算（`copy/0x180019ad0-180019ad0.{c,asm.txt}`）。上层 `18000e810` 在扇环结果状态 2 时调用 `180005a70`，后者通过 `180005db0` 将每项三个 8 字节片段复制入输入结构（`copy/` 与 `copy-leaf/` 对应反编译和原指令）；未见半径值变换。
3. 原 `180010a70` 在类型 3 或 0x80 扇环分支遍历 24 字节记录，创建 `CreateAnnularSectorInstance`。原指令 `180011071–180011091` 复制记录，`1800110cb` 读取偏移 +12 的浮点，`1800110f2/1103` 读取 +8 的浮点，之后分别通过对象虚表 `+0xa8/+0xb8` 调用；比较及调用先后分支会变化，但传入指令是 `MOVSS/MOVAPS`，此段没有除 2。用同版 SDK `MVDShapeCpp.h:754–835` 的虚方法顺序解释，`+0xa8/+0xb8` 分别对应 SetInnerRadius/SetOuterRadius；这是基于头文件与实际对象 ABI 一致的对应，未在本批独立展开 MVDShapeCpp 对象虚表。
4. `CIMVSBlobFindModu::ProcessAlg@18001fec0` 对只有一个形状且没有屏蔽形状的条件分支，把形状对象经算法工具虚函数传入；其他分支走 `GenerateMaskImage`。若启用位置修正，形状还可能经过 PositionFix 变换；是否缩放、缩放公式及各种复合形状路径不属于本次已闭合的“原始输入是否除二”结论。

## 未确认与实现建议

未观察原机；未经验证上游节点输出本身是否是半径、`MVDShapeCpp.dll` 运行实例 ABI 与方法体、任意位置修正和光栅化内部的全部换算。故给工具端的可操作建议是：在 **VM 扇环输入适配层**将订阅来的 `roiAnnulusInnerRadius/OuterRadius` 按半径原值传递，并将中文界面标签与数值合同分开；外部自研界面若确实提供直径输入，可在该界面边界显式实现直径→半径转换，但那是自研 UI 约定，不标为 VM 行为。若后续证据改变合同，只需替换扇环输入适配器。

## 证据身份（SHA-256）

- 原 `IMVSBlobFindModu.dll` `4DE695D8C3051045C9010739686F94C32D66A36EE486207FE749FB183FFF4C9A`。
- 原 `zh-cn.xaml` `8DBABDC771FC81174D02C40E37843EF4CCCE97133F41988FF976A5BD556B8B7E`；`en-us.xaml` `3E6897213458FA5F1DF6DD8A535CD238E6A4CA171ED048A8640D17CF81E7CA89`；`MVDShapeCpp.h` `2418F6398742A388E1D0295A951A90EE2EAA6E5F0CF634DF5756EAE767013D2E`。
- `annulus/0x18001bf70-18001bf70.asm.txt` `964E733EAB135796C4AB5EDC5D26DD2FD2866AE5E91E90C89241AA36364D9A10`。
- `copy/0x180019ad0-180019ad0.asm.txt` `C71566DE0983A71688EA0B71782365184345F22825147EED18B6A013714CF961`；`copy/0x180005a70-180005a70.asm.txt` `42D2C704481300519FE4400405A1D0E41652253F12492B6E571EA1BD11DD05A6`；`copy-leaf/0x180005db0-180005db0.asm.txt` `B985AC55C25F9235930F0C91E0B030BBE67D0F83FB0AD2F415B182B07452DAF3`。
- `analysis/full-audit/b08-execution-inject-20261030/blob-pre/0x180010a70-180010a70.asm.txt` `E5FA6328251DC865D0A575F77E95D50625E85BCDC3925061D759B9203A766751`。
