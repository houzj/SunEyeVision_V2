# Blob 多 ROI 继承标志初始化与消费（2026-11-01）

## 结论

已定位原版 Blob 的实际初始化覆盖：基础初始化将对象 `+0x2f` 置 0，Blob 自己的 `ResetMember` 随后将其置 1。因此，对通过原 `CreateModule` 创建、后续该字节未被改写的实例，矩形和扇环继承取值 helper 均收到非零多项标志，可按已有数量/64 上限分支遍历多个元素；四种界面继承最终都使用这两种目标叶读取路径。实际得到多项仍需要有效来源、每叶数量及索引满足条件，静态代码不能证明任意用户方案一定取得多项。

## 初始化及可达边

1. Blob 原模块 `IMVSBlobFindModu.dll` SHA256 `4DE695D8C3051045C9010739686F94C32D66A36EE486207FE749FB183FFF4C9A`。`CreateModule@180023b20` 的原指令 `180023b4d` 先调用 `1800064a0`，设置 Blob 虚表后 `180023b83` 调 `CIMVSBlobFindModu::ResetMember@180022100`，然后调用 Init。独立公开构造 `18001e230` 同样先基础构造再调用 Blob ResetMember。
2. `1800064a0@180006510` 调基础 `180015000`；`18001500d MOV dword ptr [RCX+0x2c],0x10100` 依 x64 小端写入 `00 01 01 00`，其中 `+0x2f=0`。Blob `ResetMember@180022241 MOV word ptr [RCX+0x2f],0x101` 写 `01 01`，所以该实例后续覆盖为 `+0x2f=1`。安装示例 VM431 `VmAlgModuBase.h` 命名 `m_bInheritMultipleROI`、基础 ResetMember 将它置 false，支持字段解释；实际地址和值以该版本 Blob 指令为准，不能仅按示例源码替代 ABI 证据。
3. 原二进制 Ghidra 已分析指令列表的直接偏移扫描 `roi-flag-scan.tsv` 包含两次 `18000e810` 的 `+0x2f` 读取、上述两次覆盖写，以及 +0x2c/+0x2d/+0x2e 的其他访问。该扫描仅匹配现有反汇编的显式 `[base+0x2c..0x2f]`；不能排除指针别名、宽范围 memcpy/外部写、运行时补丁或尚未分析代码。Blob ResetMember 也被析构、DeInit 路径调用，不能把销毁中的值当成活动实例的用户操作。
4. `Group.xml` 的值 0/1 将组合/五叶绑定到 InputROI，值 2/3 将组合/六叶绑定到 InputROIAnnulus；`18000e810` 在相应图形 ROI 路径分别向 `18001c790`、`18001bf70` 传同一 `+0x2f` 值。选择器空索引可编码为 `All`，单元素或变量索引仍受相应订阅和值来源制约。故多项是**有源码依据的条件可达路径**，但未执行原 VM，无法证实具体某个 UI 方案/所有生产者都能返回超过一项或未中途重置。

## 多形后续：一次掩膜工具运行

`180010a70` 按输入扇环/矩形数组生成和复用形状对象列表；`CIMVSBlobFindModu::ProcessAlg@18001fec0` 当检测形状列表恰为一个、屏蔽列表为空时，将唯一形状传到算法工具 `+0x20`（对应头文件的 SetROI）；其他图形分支将整个检测形状列表及屏蔽列表传至本对象虚表 `+0x48`。

实际 Blob 虚表 `0x1800302c0+0x48 = 180009c80`（既有 `analysis/full-audit/b08-blob-low-20261030/output/vtable.tsv`）。该函数创建 `PreproMaskTool`，按是否需重建执行形状/图像尺寸比较，重建时对检测列表逐个调用虚表 `+0x28`，原指令 `18000a32a MOV R8B,1`；对屏蔽列表逐个调用同一虚函数，`18000a359 XOR R8D,R8D`。同版 `PreproMaskCpp.h` 虚方法顺序对应 `AddRegion(shape, bool bRegionInterest)`。之后设置输入图像、调用一次工具 `Run`，取得一个输出图像；ProcessAlg 将其交算法工具的区域图像路径，之后一次调用 Blob Run。即这条路径是**多个形状合成到一张掩膜后执行一次 Blob**，不是已证的每个 ROI 各跑一遍 Blob。可依据同版接口将布尔值 1/0 理解为检测区/屏蔽区标记；mask 工具内部逐像素并集、重叠、边界、屏蔽优先级和异常细节未在本次核实，不能以名称直接断言。

未运行 SDK，未证明所有函数指针与 DLL 版本的动态装载身份，也未验证实际列表长度、越界或 UI 多选交互结果。输入继承各叶不必来自同源同帧；其最小数量、64 和失败细则见本目录 `Blob-ROI-inheritance.md`。

## 证据文件（本目录）

- `flag-write/0x180015000-180015000.asm.txt` SHA256 `F7C1FD3DE034585542CFEFDD4D84997C7776586B00D20EB1A4C6DAA4AD238A57`。
- `blob-reset/0x1800064a0-1800064a0.asm.txt` `131A769CA232EBBEF0B290152B13310223054DFEE5DD9060CD9818B128AA74FD`；`blob-reset/0x180022100-180022100.asm.txt` `F529CE9C744F00FFCE4907D9A64C3BA444235EC3A8968063348F6BE7C522F9A5`。
- `blob-reset-xrefs.tsv` `C4CDF77A7A18B9EA6D7E34A66A05F6362A876B973AFC50F6C7868E3449A2E842`；`roi-flag-scan.tsv` `537448A342F6F14A829D79E3838763E053D078E2514D96A6A3371FC0C3FA38C4`（扫描脚本 `ScanRoiFlag.java`）。
- `mask-consumer/0x180009c80-180009c80.asm.txt` `EF780E55B28D9B885234339B5A02B2CECD64977C5BE1A36B6AB5E2367751D47D`。
- 原 `PreproMaskCpp.h` `913EDCA2A6E8FEB2F81D8C8026F5D2439FF42CF1B6E32110DC5D8D062BD47F23`；Blob AlgorithmTab XML `34A0CC5423A3D66B92DC272E0F50B013310E5180E801C648005383C3ADDDFDEC`。
- 原 CreateModule 指令 `D:/MyWork/REA逆向测试/analysis/full-audit/b08-blob-low-20261030/output/constructor/0x180023b20-180023b20.asm.txt` `4DA1169F6F3F20B87666004B393DA04648535F49831484F0C9A32CF503C0F337`；ProcessAlg 指令 `analysis/full-audit/b08-blob-low-20261030/output/0x18001fec0-18001fec0.asm.txt` `AA892CE1D12B0A132DC0EE5D4651FCD8EC9685839AD8F1012DD9CDC4A029F684`。
