# 坐标系与角度契约参考（HALCON / WPF / OpenCV）

> 参考类型：坐标系对照与角度口径统一说明
> 适用范围：视觉工具（圆查找 / 直线查找 / Blob 分析 / 后续 OpenCV 接入）的显示与结果角度
> 日期：2026-09-03
> 状态：已定标（直线场景经实测校准；圆/弧场景见 `HalconArcAdapter`）

---

## 1. 一句话结论

**图像像素坐标角（`atan2(dy, dx)`）是全链路显示 + 结果角的唯一契约。**
HALCON 图像坐标（row/col）、WPF 屏幕坐标、OpenCV 图像坐标三者**同构**，角度天然一致、零转换；
只有 HALCON 的**数学几何角 phi**（圆/弧/旋转矩形）是例外，须经 `phi = -WPF屏角` 适配。

---

## 2. 三套坐标/角度体系对照

| 体系 | X 轴 | Y 轴 | 原点 | 角度定义 | 与 WPF atan2 关系 |
|---|---|---|---|---|---|
| **WPF 屏幕坐标** | x 向右 | y **向下** | 左上 | `atan2(dy, dx)`，视觉顺时针为正 | 基准 |
| **HALCON 图像像素坐标**（row/col） | col 向右 | row **向下** | 左上 | 与 WPF 同构 | **一致，无需转换** |
| **OpenCV 图像坐标** | x 向右 | y **向下** | 左上 | `atan2(dy, dx)`，视觉顺时针为正 | **一致，无需转换** |
| **HALCON 数学几何角 phi** | 水平 0° | 数学惯例逆时针为正（视觉上 = WPF 取负） | — | `phi = -WPF屏角` | 需经适配器 |

> 关键事实：HALCON 图像坐标中 **row 向下 = WPF y 向下、col 向右 = WPF x 向右**，
> 二者是同一坐标系的两种命名。HALCON 的 `(row, col)` 直接对应 WPF 的 `(y, x)`。

---

## 3. 分场景角度口径

### 3.1 直线查找（LineDetect）—— 已定标，无适配层

**走图像像素坐标，WPF ↔ HALCON 逐点轴交换即可，方向语义不变。**

传参链路（`LineDetectTool.cs`）：

```
AddMetrologyObjectLineMeasure(
    startP.Y, startP.X,      // WPF (X,Y) → HALCON (row=Y, col=X)
    endP.Y,   endP.X,        // 同上
    ...)
```

- WPF `start→end` 直接映射 HALCON `RowBegin→RowEnd`，**仅轴交换，顺序语义不变**。
- HALCON line measure 的测量扫描方向 = 引导线方向**顺时针转 90°**（屏幕坐标），
  即 WPF 中 `normal = (-dir.Y, dir.X)`（`BuildLineBands` 已在使用）。
- 直线结果角度用 `Math.Atan2(end.Y - start.Y, end.X - start.X)`（度），
  **它就是 HALCON 像素坐标系下的同一角度**——无风险、零转换。

**相关文档**：`直线卡尺主卡尺方向显示方案-20260903`（方向语义定标实验）。

### 3.2 圆/弧查找（CircleDetect）—— 走数学 phi，须经适配器

**HALCON 圆/弧算子（`add_metrology_object_circle_measure`）的角度参数是数学几何角 phi。**

适配规则（`HalconArcAdapter.cs`，commit `d3bdf2f` 定标）：

```
phi = -WPF屏角            // 圆/弧：WPF 连续扫掠角 → HALCON [0, 2π]
整圆: start_phi=0, end_phi=2π, point_order=positive   // 官方文档要求
弧:   start_phi/end_phi 规范到 [0, 2π)，(end-start) mod 2π ≡ ±|AngularSpan|
```

- **禁止**把 WPF 屏角直接传给 HALCON phi（方向会镜像/错 90°）。
- **禁止**整圆传 `start_phi=0, end_phi=0`（零长退化弧，曾致 HALCON `#3263` 拟合失败）。

### 3.3 图像坐标点 / 区域 / 轮廓 —— 通用

HALCON region/XLD/测量点坐标均为 row/col，与 WPF 点同构：
- 显示时 `Point(x=col, y=row)` 直接使用，无需角度换算。
- 位置修正矩阵（`AffineMatrix`）按参考坐标↔当前图像坐标变换，同样适用。

---

## 4. 后续 OpenCV 接入的注意事项

| 风险点 | 说明 | 对策 |
|---|---|---|
| OpenCV 角度基准 | 部分 OpenCV 函数角度定义不同（如 `minAreaRect` 返回 `[-90,0)`、`fitLine` 用向量） | 在**工具边界**做一次显式适配，勿假设与 HALCON 算子同语义 |
| 行/列 vs x/y | OpenCV 惯用 `(x,y)` 但行= y、列= x，与 HALCON row/col 命名不同 | 统一映射：`x↔col`、`y↔row`，在封装层固定 |
| 角度显示 | OpenCV 图像坐标 y 向下，与 WPF 同构 | 显示角仍用 `atan2(dy,dx)`，零转换 |

**建议**：所有算法引擎（HALCON/OpenCV）在 `Run` 边界完成坐标/角度转换，
**输出给 UI/结果树/全局变量的角度一律为 WPF 图像坐标角**，不渗透引擎差异到显示层。

---

## 5. 架构防线（开发时必须遵守）

1. **唯一契约**：显示 + 结果角 = 图像坐标角 `atan2(dy,dx)`（WPF 度）。
2. **引擎边界适配**：仅 HALCON 数学 phi 类算子入/出口经 `HalconArcAdapter`（圆/弧）；
   直线走同构映射，无需适配层（已在代码注释注明）。
3. **角度单位**：结果 `Angle` 一律为**度**；HALCON/OpenCV 内部弧度只在引擎内使用，出口转度。
4. **直线方向歧义**：`atan2` 返回 `(-180°,180°]`，同线反向画会差 180°；
   需要"线角度"（非有向）时归一化到 `[0,180)`。
5. **显示精度**：结果角度展示用 3 位有效数字（`G3`），勿用 `F1` 丢失精度（如 0.239° 显示成 0.2°）。

---

## 6. 关联文件索引

| 文件 | 角色 |
|---|---|
| `src/Plugin.SDK/Models/Geometry/HalconArcAdapter.cs` | 圆/弧 WPF↔HALCON phi 唯一适配出口 |
| `tools/SunEyeVision.Tool.LineDetect/LineDetectTool.cs` | 直线测量（WPF↔HALCON row/col 轴交换） |
| `tools/SunEyeVision.Tool.LineDetect/LineDetectResults.cs` | 直线结果 Angle（图像坐标角，度） |
| `tools/SunEyeVision.Tool.CircleDetect/CircleDetectTool.cs` | 圆测量（phi 适配分派） |
| `docs/01-方案设计/2026/09/03/直线卡尺主卡尺方向显示方案-*.md` | 直线方向语义定标实验 |
