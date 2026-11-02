# C17 原版 OpenCvSharp 几何转换 CIL

本次离线解码原 VM 4.4 `Applications/3rdLib/OpenCv/OpenCvSharp.dll`，SHA-256 `16AE93D84A694B1099C33AA97F248FCFBDC05FE4D552C5FD67F6F9579AD15F9B`，MVID `3cd2bd95-9250-4f68-90aa-cab227a0fa5a`；工具 dotnet-ildasm 0.12.2（.NET major roll-forward），并用 `System.Reflection.Metadata.PEReader` 直接读取 enum 常量。没有加载、执行原程序集或运行 VM/SDK。完整反汇编为 `C17-opencv-original.il.txt`，SHA-256 `B38B053CC5D96FE6DDEAA33B207115E30389A92ACCC7AFF6E61842B49E76BF92`；调用者 `Apps.Data.dll` 的反汇编为 `C17-appsdata-original.il.txt`。行号为本地产物行号，不是原源代码行号。

## 构造与调用者实际选型

| 原成员 | 原 CIL | 可实施结论 |
|---|---|---|
| `Point(double,double)`，RVA `0x1C069`，IL 文件:64102 | 对 X/Y 各 `conv.i4`，直接写 int32 字段 | 有限且转换结果可表示时向零截断，如 1.9→1，-1.9→-1；不是四舍五入，不是 checked `conv.ovf.i4`。NaN、无穷、越界的精确结果不从此 IL 指定为固定整数或饱和；应分开保护/验证运行时。 |
| `Size(double,double)`，RVA `0x1F29F`，:70376 | 对宽高各 `conv.i4` | 同上，宽高均向零截断。 |
| `Point2f(float,float)`，RVA `0x1C43D`，:64726 | 原样写 single 字段 | 不在该构造器内转整；调用者从 double 转入时先有 float 精度损失。 |
| `Size2f(float,float)`，RVA `0x1F3F6`，:70611 | 原样写 single 字段 | 保留传入 float 小数。 |
| **`Size2f(double,double)`**，RVA `0x1F406`，:70624 | 每个参数先 `conv.i4`，再 `conv.r4` 写 single | 有限可表示值先向零取整再转 float；不能把它与 float 重载混同。 |

关键调用者已核原 `Apps.Data.Common.ImageSaveOpenCvHelper.DrawRect` 的 CIL `C17-appsdata-original.il.txt:22875-22970`：中心 X/Y `conv.r4` → `Point2f(float,float)`；**宽、高取接口的 double → `Size2f(double,double)`**；角度 `conv.r4`；随后 `RotatedRect.Points()`，每个 float 顶点转 double 后调用 `Point(double,double)` 再交 `Cv2.Line`。因此实际宽高小数在求四角前被截断、四角坐标在画线前再向零截断。这一点与直接使用新版 float 构造求角不同。

## RotatedRect.Points

原 `C17-opencv-original.il.txt:68503-68686`：构造器只保存 Center、Size、single Angle；`Points()` 是**托管 CIL**，无 native 调用。它用 double 计算 `θ=Angle*π/180`，对 `Math.Cos(θ)`、`Math.Sin(θ)` 各先 `conv.r4`，再以 single 乘 0.5，设 `a=cos/2,b=sin/2`。接下来按 single 字段/结果计算：

```
p0 = (cx - b*h - a*w, cy + a*h - b*w)
p1 = (cx + b*h - a*w, cy - a*h - b*w)
p2 = (2*cx - p0.x,     2*cy - p0.y)
p3 = (2*cx - p1.x,     2*cy - p1.y)
```

角度为度，正角使局部向右方向旋向图像坐标向下，视觉上为顺时针；仍应保留原单精度转换与运算顺序，不能假设所有中间运算使用 double 后最终一次取整即可像素一致。源码证据证明该消费者如何处理角度，不证明 Blob 算法如何产生 Angle。

## 直接读取的枚举

复验脚本 `tests/NativeEvidence/C17-opencv-enum-metadata.ps1` 输出 `C17-opencv-enum-evidence-20261103.json`，SHA-256 `E7EA872F47F4782E258D7A64C87E8FC20DA446FCFDEBC8E3FF9DF9804620B0C4`；只读 PE 常量表：`LineTypes.Link8=8`、`Link4=4`、`AntiAlias=16`；`ColorConversionCodes.BGR2RGB=4`、`RGB2BGR=4`、`GRAY2BGR=8`、`GRAY2RGB=8`。这些是原构建的枚举数值，不独立证明 native 操作和新版行为逐像素一致。

迁移时建议在导出几何适配层显式复现本构建的重载和转换序列；对 NaN/越界采取独立、明确标记的保护，位置在 float→int 适配边界。图形光栅、字体、编码等原生行为与版本差异仍需另行证据，本次不称完整导出像素同态。
