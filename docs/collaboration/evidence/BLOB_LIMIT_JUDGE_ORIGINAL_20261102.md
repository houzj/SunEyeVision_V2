# VM 4.4 Blob 结果判定链静态核查（2026-11-02）

## 覆盖和结论

本次静态获取原 Blob 模块 `LimitJudge@18001f720` 与三个比较 helper 原指令，复用 Process、ProcessAlg、ProcessOutput、XML、SDK 头和结果输出报告。未运行 VM/SDK；此处不将静态路径当作现场行为，不推断内核筛选、排序、限数的未取体顺序。原 `IMVSBlobFindModu.dll` SHA-256 `4DE695D8C3051045C9010739686F94C32D66A36EE486207FE749FB183FFF4C9A`。

## 调用、零目标与状态

原 `Process@18001fa80` 在图像类型等条件满足后调用 `ProcessAlg@18001fec0`；只有返回 0 才调用 `LimitJudge`，LimitJudge 返回 0 才调用 `ProcessOutput@180020310`。前一阶段失败会直接跳过后续阶段，非零返回值与判定为 NG 不等同。`ProcessAlg` 成功读取结果对象的数量 n 后令实例 `+0x15da0 = (n > 0)`，输出结构的 `+0 = n`、`+4 = 该初始值`。`LimitJudge` 不改输出结构 +4，而使用并更新实例 `+0x15da0`；`ProcessOutput` 将实例 +0x15da0 作为 `ModuStatus` 发布，并在 n>0 时发布实例逐项数组 `BlobStatus`。它还用输出结构 +4 作为若干发布调用的参数，因此不能假定该结构内初始标志等同最终 ModuStatus。

`SetParam@180022500` 将 `OKWhenNumIsZero` 存入实例字节 `+0x15ec0`；资源初始声明 False。原指令 `18001f758–18001f769`：仅当该字节恰好为 1 时执行 `status = (n == 0)`；否则沿用 `ProcessAlg` 的 `(n > 0)`。此后两个总体比较和每个逐体比较均以按位 AND 收缩 status。故对本函数正常路径及有限的已解析条件：

| 开关 | n=0 时比较前 | n>0 时比较前 | 后续 |
| --- | --- | --- | --- |
| 未等于 1 | 0 | 1 | AND 两个总体条件及所有逐体条件 |
| 等于 1 | 1 | 0 | AND 两个总体条件及所有逐体条件 |

这个 n>0 且开关为 1 时变为 0 的行为反直觉，但由 `CMP [result],0; SETZ AL; MOV [this+15da0],EAX` 原指令直接支持；不因参数名称擅自改为 `status OR (n==0)`。此为静态代码，实际配置、外部覆盖或 DLL 身份尚未经动态核验。零对象时逐体循环不执行，两个总体限制仍参与；空集不是一概通过或失败。

## 15 组限制：2 总体 + 13 逐体

SetParam 的字段偏移和 LimitJudge 的 getter 位移交叉对应；下表名称来自同版 XML/SetParam，getter 语义在同版 SDK vtable 布局前提下成立。所有 helper 仅当 enable **恰等于 1** 时检查；未开启返回 1。总体结果及每个对象的多个字段均按 AND 聚合，不是 OR 或“至少一个对象合格”。每个对象状态保存到 `this+0x15d98[i]`，随后 status &= 该状态。

| 层级 | 字段（enable 所在对象偏移） | 读值及比较 |
| --- | --- | --- |
| 总体 | BlobNum `+15da8` | ProcessAlg 返回结果 n，整数 helper `180019f20` |
| 总体 | BlobTotalArea `+15db8` | 结果对象 `+0x28` 总面积 getter，转 double，`180019f40` |
| 逐体 | BlobArea `+15dd0` | 对象 `+0x80` 面积 float 转 double，`180019f40` |
| 逐体 | CentroidX/Y `+15de8/+15df4` | 对象 `+0x70` 质心 X/Y，`180019ee0` |
| 逐体 | CenterX/Y `+15e00/+15e0c` | 对象 `+0x60` 的 box 对象 `+0x90/+0xa0`，`180019ee0` |
| 逐体 | Angle `+15e18` | box `+0xd0`，`180019ee0` |
| 逐体 | BoxHeight/BoxWidth `+15e24/+15e30` | box `+0xc0/+0xb0`，`180019ee0` |
| 逐体 | Perimeter `+15e40`、LongAxis `+15e58`、ShortAxis `+15e70` | 对象 `+0x20/+0x38/+0x30`，float 转 double，`180019f40` |
| 逐体 | Circularity `+15e88`、Rectangularity `+15e94` | 对象 `+0x28/+0x48`，`180019ee0` |

有限数值的端点：整数 helper 原指令排除 `< low` 和 `> high`，即闭区间 `[low,high]`。float helper 比较 `[low−FLT_EPSILON, high+FLT_EPSILON]`，原 PE `0x18002f9ac` float32 为 `1.1920928955078125e-7`；double helper 比较 `[low−DBL_EPSILON, high+DBL_EPSILON]`，`0x18002fb78` float64 为 `2.220446049250313e-16`。要保留浮点计算精度、舍入及汇编 `COMISS/COMISD` 的无序语义，不能把该描述推广为 NaN 的普通闭区间逻辑。整数、float、double helper 均对 enable 非 1 直接返回 1。

## 输出集合及错误

`LimitJudge` 从 `this+0x15eb8` 已运行的 Blob 工具获取同一结果对象，取 n 后逐索引 `GetBlobInfo(i)`；它不在已取函数体内删除、排序、截断该结果集合。`ProcessOutput` 再取结果 getter 发布 BlobNum、TotalArea，n>0 时发布 n 个 `BlobStatus` 并在多个输出分支同样按索引取 Blob 信息。因此判定消费的是算法对象已经返回的集合，判定失败状态不等于函数错误或把该 Blob 从结果中筛掉。结果内部的面积/特征初筛、排序、FindNum 截断彼此先后、并列排序稳定性、总面积对应哪一个筛选阶段尚未闭合：仅从 ProcessAlg→LimitJudge 的函数先后，不能猜算法内部顺序。

`LimitJudge` 空参数返回 `0xe0000001`；逐项状态缓冲为空返回 `0xe0000002`；n>20000 返回 `0xe0000003`；结果对象空返回 `0xe0000315`。这些非零错误会让 Process 跳过 ProcessOutput。相反，规则判定产生 0 状态不使 LimitJudge 自动返回非零：它仍可进入 ProcessOutput 并发布状态及数据。`ProcessOutput` 的发布不是事务：先发 ModuStatus，再发 BlobNum、TotalArea 等，某一步失败可提前返回，故不能从错误推断所有旧输出都被清空，或所有结果都原子更新。最终框架运行状态和旧结果保留还需其上层调用/回调证据。

## 几何含义

默认结果列表 `Angle` 映射 `MinBoudingRect.Angle`，不是独立的 `BlobAngle`。LimitJudge 的 Angle 判定也来自 `GetBoxInfo` 所得最小外接矩形角度；Centroid、外接矩形中心是不同 getter。LongAxis/ShortAxis 直接取 Blob 结果 getter，头文件称最小外接矩形的长、短轴；模块转发中没看到乘二/除二，但 getter 内部是否返回全长或半长、退化目标及方向归一化仍未知。原 SDK 矩形头对角度列出 `(-180,180]` 与顺时针正；不能据此断言所有原生算法数值或目标退化情形都已验证。完整展示与汇编追踪见 `D:/MyWork/REA逆向测试/docs/joint-audit/B08结果列表角度与长短轴映射-20261031.md`。

## 审计证据

本目录 `native/0x18001f720-18001f720.{c,asm.txt}`：指令 SHA-256 `191DAC8C3BF2B62D8BDD0BD05255A7645C1BD42334E0E40424F981BABB19674F`。

- `helpers/0x180019f20-180019f20.asm.txt` `637541FEB283E21BC3FCF7857BFCAA75786387C4AAFE574B3612C82B57AB8DBA`。
- `helpers/0x180019f40-180019f40.asm.txt` `3117C14DBB5D791B02B1D4E13D3C8AD531353B328D45DA25651A3FE02947725A`。
- `helpers/0x180019ee0-180019ee0.asm.txt` `557795CB1204D28579F206261CF81A0DEAB75608426CEDB7B926267A4F954E89`。
- Process、ProcessAlg、ProcessOutput 与 SetParam 的原反编译/汇编分别位于 `D:/MyWork/REA逆向测试/analysis/full-audit/b08-blob-low-20261030/output/0x18001fa80-18001fa80.*`、`0x18001fec0-18001fec0.*`、`0x180022500-180022500.*` 和 `analysis/full-audit/b08-geometry-output-20261031/0x180020310-180020310.*`。
