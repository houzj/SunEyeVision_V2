# CvEngine 数组语义对齐方案（最终版）

## 0. 决策记录

| # | 决策 | 来源 |
|---|------|------|
| 1 | `ReduceDomain` 对齐 Halcon：不改变图像尺寸，仅掩码 | 用户纠正 |
| 2 | `CropDomain` 对应 Halcon crop_domain：裁剪到 BBox，改变图像尺寸 | 新增 |
| 3 | BlobAnalysis 流程步骤2 用 CropDomain（性能优化） | 用户确认 |
| 4 | CvRegion/CvImage 自带数组语义（方案A），不需要 HObject/HTuple 基类 | 用户选择 |
| 5 | 不考虑向后兼容，直接删除旧代码 | rule-008 |

## 1. 问题

Halcon 的 HObject 天然支持 tuple（对象数组）语义：
- `area_center(Regions, Area, Row, Column)` — 输入 N 个区域，输出 N 个面积/行/列
- `connection(Region, ConnectedRegions)` — 输出 N 个连通域
- `threshold(Image, Region, MinGray, MaxGray)` — 支持多区间阈值
- `reduce_domain(Image(-array), Region, ImageReduced(-array))` — 支持图像数组

当前 CvRegion 内部用 `_mask + _labels/_stats/_centroids + _count` 存储，
CC 缓存机制已经实现了"多区域零开销直读"，但存在以下问题：

1. **每个 item 没有独立 Mask** — 只有合并掩码 + labels 图，SelectObj/Split 每次都要 Compare
2. **算子输入不支持数组** — 如 `Intensity(CvImage, CvRegion?)` 只能处理合并后的单个区域
3. **CvImage 完全无数组语义** — 无法表达 Halcon Image(-array)
4. **ReduceDomain/CropDomain 混淆** — 已在本次对话中修正

## 2. 方案：HObject 式数组语义（方案A）

### 2.1 核心思路

**CvRegion/CvImage 自带数组语义**（类似 Halcon HObject），内部用 `List<TItem>` 存储，
`Count` 属性即数组长度，算子签名不变。**不需要 HObject/HTuple 基类**（C# 类型系统更优）。

| Halcon | CvEngine |
|--------|----------|
| `HObject` (tuple of regions) | `CvRegion` (List of RegionItem) |
| `CountObj(HObject)` | `CvRegion.Count` |
| `SelectObj(HObject, Index)` | `CvOp.SelectObj(region, index, out single)` |
| `ConcatObj(Obj1, Obj2)` | `CvOp.Concat(region1, region2, out result)` |

### 2.2 CvRegion 重构

#### 当前实现（v1）

```csharp
public class CvRegion : IDisposable
{
    internal Mat? _mask;        // 合并二值掩码
    internal Mat? _labels;      // 标签图（仅多区域时存在）
    internal Mat? _stats;       // CC 统计
    internal Mat? _centroids;   // CC 质心
    internal int _count;        // 子区域数量
    internal Size _imageSize;   // 图像尺寸
}
```

问题：没有独立 item Mask，每次 SelectObj/Split 都要 `Cv2.Compare(labels, i, ...)`。

#### 新实现（v2）

```csharp
public class CvRegion : IDisposable
{
    // ===== 核心存储 =====
    internal List<RegionItem> _items;       // 子区域列表（对齐 HObject tuple）
    internal Size _imageSize;               // 共享图像尺寸

    // ===== CC 缓存（可选，由 Connection 设置）=====
    internal Mat? _ccLabels;                // 标签图（CV_32SC1）
    internal Mat? _ccStats;                 // 连通域统计
    internal Mat? _ccCentroids;             // 连通域质心

    // ===== 合并掩码缓存（懒加载）=====
    private Mat? _combinedMaskCache;
}

internal class RegionItem : IDisposable
{
    internal Mat? Mask;                     // 单个子区域掩码（CV_8UC1）
}
```

#### 关键设计：Connection 产出的 item Mask 采用懒加载

Connection 产出 N 个 RegionItem 时，每个 item **不立即创建 Mask**，只记录 labelIndex。
Mask 在首次访问时从 `_ccLabels` 中提取，提取后缓存。

理由：
1. 很多场景只需 stats 数据（面积/质心/BBox/矩形度），不需要独立 Mask
2. 避免一次性创建 N 个 Mask 的内存峰值
3. 只有 SelectObj/Split/GetContour 等需要独立 Mask 时才提取

```csharp
internal class RegionItem : IDisposable
{
    internal Mat? Mask;                     // 独立掩码（懒加载，首次访问时创建）

    // 懒加载参数（仅 Connection 产出时设置）
    internal Mat? _ccLabels;                // 共享的标签图引用（不拥有）
    internal int _labelIndex;               // 标签值（1..N）
    internal Size _imageSize;               // 图像尺寸

    /// <summary>获取或创建独立掩码（懒加载）</summary>
    internal Mat? GetMask()
    {
        if (Mask != null) return Mask;
        if (_ccLabels == null || _ccLabels.Empty()) return null;

        Mask = new Mat(_imageSize, MatType.CV_8UC1, Scalar.Black);
        using var tmp = new Mat();
        Cv2.Compare(_ccLabels, _labelIndex, tmp, CmpType.EQ);
        tmp.CopyTo(new Mat(Mask, Cv2.BoundingRect(tmp)));
        return Mask;
    }
}
```

**Threshold 等单区域产出的 CvRegion**：直接创建 1 个带 Mask 的 RegionItem，无 CC 缓存。

#### 合并掩码（懒加载）

```csharp
internal Mat? GetCombinedMask()
{
    if (_combinedMaskCache != null) return _combinedMaskCache;
    if (_items.Count == 0) return null;

    _combinedMaskCache = new Mat(_imageSize, MatType.CV_8UC1, Scalar.Black);
    foreach (var item in _items)
    {
        var mask = item.GetMask();  // 触发懒加载
        if (mask != null && !mask.Empty())
            Cv2.BitwiseOr(_combinedMaskCache, mask, _combinedMaskCache);
    }
    return _combinedMaskCache;
}
```

#### 公共属性

| 成员 | 实现 | Halcon 对应 |
|------|------|-------------|
| `Count` | `_items.Count` | `CountObj()` |
| `IsEmpty` | `_items.Count == 0` | `\|Obj\| == 0` |
| `ImageSize` | `_imageSize` | — |
| `Mask` | `GetCombinedMask()` | 整体区域 |
| `this[int index]` | `SelectObj` 语义 | `SelectObj()` |

#### 工厂方法

| 方法 | 说明 | Halcon 对应 |
|------|------|-------------|
| `GenEmptyObj()` | 创建空 tuple | `gen_empty_obj()` |
| `FromMask(Mat)` | 单元素 tuple（1 个带 Mask 的 item） | 从掩码创建 |
| `GenRectangle1(...)` | 单元素 tuple | `gen_rectangle1()` |
| `CreateFromRegions(...)` | 单元素 tuple（合并 UI 区域） | 从 UI 创建 |

### 2.3 CvImage 重构

当前 CvImage 是纯单图像包装，无数组语义。

```csharp
public class CvImage : IDisposable
{
    internal List<ImageItem> _items;
    internal bool _ownsImage;

    public int Count => _items.Count;
    public bool Empty => _items.Count == 0;

    // 兼容：单图像快捷访问
    public Mat Image => _items.Count > 0 ? _items[0].Image : throw new InvalidOperationException("空图像");
    public Size Size => Image.Size();
}

internal class ImageItem : IDisposable
{
    internal Mat Image;
}
```

**注意**：CvImage 数组语义优先级低于 CvRegion。当前工具只用单图像，
Phase 3 再实现。Phase 1/2 期间 CvImage 保持单图像，`Count` 始终 = 1。

### 2.4 CvContour — 不变

CvContour 当前已独立于 CvRegion，GetContour 输出 `CvContour[]`，
本身已具备数组语义（数组元素是独立 CvContour）。无需改动。

### 2.5 算子行为变化

**签名不变，行为对齐 Halcon tuple 处理：**

| 算子 | 当前行为 | 新行为（对齐 Halcon） |
|------|---------|----------------------|
| `ReduceDomain(image, region, out reduced)` | 处理单个 image | 处理 image 中每个 item（Count=1 时行为不变） |
| `CropDomain(image, region, out cropped, out bbox)` | 处理单个 image | 处理 image 中每个 item |
| `Threshold(image, region, ..., out result)` | 输出 1 个 Region（Count=1） | 输出 1 个 Region（Count=1，1 个 RegionItem） |
| `Connection(region, out connected)` | 输出含 N 个子区域（labels 缓存） | 输出含 N 个 RegionItem（懒加载 Mask + CC 缓存） |
| `AreaCenter(region, out areaCenters)` | 读 _stats 缓存 | 读 _ccStats 缓存（逻辑不变） |
| `SmallestRectangle1(region, out ...)` | 读 _stats 缓存 | 读 _ccStats 缓存（逻辑不变） |
| `Rectangularity(region, out ...)` | 读 _stats 缓存 | 读 _ccStats 缓存（逻辑不变） |
| `SelectObj(region, index, out single)` | Compare(labels, index+1, ...) | `region[index]` 取 item，构造单元素 CvRegion |
| `Split(region, out regions)` | Compare 逐标签提取 | 逐 item GetMask()，构造独立 CvRegion |
| `GetContour(region, out contours)` | Compare 逐标签 FindContours | 逐 item GetMask() + FindContours |
| `Intensity(image, region, out ...)` | 处理合并掩码 | 逐 item 处理（Count=1 时不变） |
| `SelectShape(region, out selected, ...)` | 过滤后重建 labels | 过滤 items，保留 CC 缓存 |

### 2.6 ReduceDomain vs CropDomain（已实现）

| 算子 | 对齐 Halcon | 行为 | 签名 |
|------|-------------|------|------|
| `ReduceDomain` | `reduce_domain` | 不改变图像尺寸，掩码限制域 | `(CvImage, CvRegion?, out CvImage)` |
| `CropDomain` | `crop_domain` | 裁剪到 BBox，改变图像尺寸 | `(CvImage, CvRegion?, out CvImage, out Rect)` |

## 3. 实施计划

### Phase 1：CvRegion 重构（核心）

**删除旧代码，直接替换：**

1. 新增 `RegionItem` 内部类（懒加载 Mask）
2. CvRegion 内部存储改为 `List<RegionItem>` + CC 缓存
3. 删除旧字段：`_mask`, `_labels`, `_stats`, `_centroids`, `_count`
4. 新增 `GetCombinedMask()` 懒加载
5. 适配工厂方法：`GenEmptyObj`, `FromMask`, `GenRectangle1`, `CreateFromRegions`
6. 适配 `CopyObj`, `Dispose`

### Phase 2：CvOp 算子适配

1. **Connection** — 产出 N 个 RegionItem（懒加载 Mask）+ CC 缓存
2. **AreaCenter / SmallestRectangle1 / Rectangularity** — 读 `_ccStats`/`_ccCentroids`（逻辑微调）
3. **GetContour** — 逐 item `GetMask()` + FindContours
4. **Split** — 逐 item `GetMask()`，构造独立 CvRegion（out CvRegion[] 不变）
5. **SelectObj** — 从 `_items[index]` 取 item
6. **SelectShape** — 过滤 `_items`，保留 CC 缓存
7. **Concat** — 合并两个 `_items` 列表
8. **Union1 / Difference / Intersection / Complement** — 基于合并掩码计算，输出单 item
9. **形态学算子** — 基于合并掩码计算，输出单 item
10. **Intensity** — 逐 item 统计（对齐 Halcon intensity 对 Regions 数组的处理）

### Phase 3：CvImage 数组语义

1. 新增 `ImageItem` 内部类
2. CvImage 改为 `List<ImageItem>`
3. ReduceDomain / CropDomain 支持图像数组
4. Rgb1ToGray 支持图像数组

### Phase 4：高级特性

1. ~~Threshold 多区间阈值（MinGray/MaxGray 数组）~~ → `ThresholdRange` 新算子（Halcon 风格 InRange + 多区间）
2. ~~形态学算子数组处理~~ → 保持合并掩码处理（膨胀分配律 D(A∪B)=D(A)∪D(B)，单 item 输出即可；腐蚀需要 Split 后逐 item）
3. ~~更多 Halcon 算子对齐~~ → Intensity 多区域重载 + Rgb1ToGray/ReduceDomain/CropDomain 图像数组支持

## 4. 兼容性分析

### 对现有工具的影响

| 工具 | 影响 | 适配量 |
|------|------|--------|
| BlobAnalysisTool | 小 — CvRegion 外部 API 不变（Count, IsEmpty, Mask 属性） | 几乎无 |
| ThresholdTool | 几乎无 — 只用 CvRegion.FromMask + CvOp.Threshold | 无 |

### API 兼容

- `CvRegion.Count` — 语义不变（从 `_count` 变为 `_items.Count`）
- `CvRegion.IsEmpty` — 语义不变
- `CvRegion.Mask` — 改为懒加载合并掩码，行为不变
- `CvRegion.ImageSize` — 不变
- `CvRegion.PixelCount` — 基于合并掩码计算，不变
- 算子签名 — **全部不变**

### 内部 API 变化

| 旧 | 新 | 影响 |
|----|-----|------|
| `region._mask` | `region.GetCombinedMask()` | CvOp 内部 |
| `region._labels` | `region._ccLabels` | CvOp 内部 |
| `region._stats` | `region._ccStats` | CvOp 内部 |
| `region._centroids` | `region._ccCentroids` | CvOp 内部 |
| `region._count` | `region._items.Count` | CvOp 内部 |
| `region.HasLabels` | `region._ccLabels != null` | CvOp 内部 |

以上变化仅在 CvEngine 内部（CvOp + CvRegion），对外部工具无影响。

## 5. 风险与缓解

| 风险 | 缓解 |
|------|------|
| 内存增加（N 个独立 Mat vs 1 个 labels） | Connection 产出时 item Mask 懒加载，不需要时不创建 |
| 合并掩码懒加载开销 | 缓存机制：首次计算后缓存，Dispose 时释放 |
| GetMask 懒加载竞态 | CvRegion 非线程安全（与 Halcon HObject 一致），单线程使用 |
| RegionItem 懒加载依赖 _ccLabels 生命周期 | _ccLabels 由 CvRegion 拥有，item 只是引用，Dispose 顺序保证 |

## 6. 验证清单

- [x] Phase 1：CvRegion 重构后，BlobAnalysisTool + ThresholdTool 构建通过
- [x] Phase 1：CvRegion.Count / IsEmpty / Mask / ImageSize / PixelCount 行为不变
- [x] Phase 2：Connection 产出 N 个 item，AreaCenter/SmallestRectangle1/Rectangularity 零 CC 开销
- [x] Phase 2：SelectObj/Split/GetContour 正确提取独立 Mask
- [x] Phase 2：BlobAnalysis 完整流程跑通，性能不回退
- [x] Phase 3：CvImage 数组语义（List<ImageItem> + Count/Image/Size/Empty 兼容属性）
- [x] Phase 4：高级特性
