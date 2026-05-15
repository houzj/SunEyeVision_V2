using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// 区域特征类型 - 对应 Halcon region_features 的特征名列表
    /// </summary>
    /// <remarks>
    /// 使用示例（对齐 Halcon）:
    ///   region_features(SingleRegion, ['area', 'perimeter', 'circularity'], [A, P, C])
    ///   ↓
    ///   CvOp.RegionFeatures(singleRegion,
    ///       RegionFeature.Area, RegionFeature.Perimeter, RegionFeature.Circularity,
    ///       out var values)
    /// 
    /// 公式说明：
    ///   Circularity = Compactness = P²/(4πA) （Halcon 定义，值越大越不圆）
    ///   Rectangularity = A / minEnclosingRectArea（最小外接矩形面积比）
    ///   Convexity = A / convexHullArea（凸包面积比）
    /// </remarks>
    public enum RegionFeature
    {
        // ===== 基础几何（对应 Halcon area_center）=====

        Area,           // 面积（像素数）
        Row,            // 质心行坐标 (cy)
        Column,         // 质心列坐标 (cx)

        // ===== 周长与轮廓 =====

        Perimeter,          // 区域周长（外轮廓 ArcLength，closed=true）
        ContLength,         // 轮廓长度（所有内外轮廓之和，RetrievalModes.List）

        // ===== 形状特征 =====

        Circularity,        // 圆度 F = P²/(4πA) — Halcon 定义（越大越偏离圆形）
        Compactness,        // 紧凑度 F = P²/(4πA) — 与圆度公式相同
        Rectangularity,     // 矩形度 = A / minEnclosingRectArea（最小外接矩形）

        // ===== 凸性 =====

        Convexity,          // 凸性 = A / convexHullArea（区域面积/凸包面积）

        // ===== 外接矩形1 - axis-aligned bounding box（smallest_rectangle1）=====

        BoundingBoxRow,         // 左上角行坐标 Row1
        BoundingBoxColumn,      // 左上角列坐标 Column1
        BoundingBoxWidth,       // 宽度
        BoundingBoxHeight,      // 高度

        // ===== 外接矩形2 - 最小面积旋转矩形（smallest_rectangle2）=====

        MinRect2CenterRow,      // 中心点行坐标
        MinRect2CenterColumn,   // 中心点列坐标
        MinRect2Phi,            // 方向角度（弧度）
        MinRect2Length1,        // 第一边长度（长边）
        MinRect2Length2,        // 第二边长度（短边）

        // ===== 椭圆轴（elliptic_axis）=====

        EllipticAxisRa,     // 长轴半径 Ra
        EllipticAxisRb,     // 短轴半径 Rb
        EllipticAxisPhi,    // 主轴方向 Phi（弧度）

        // ===== 外接圆（smallest_circle / min_enclosing_circle）=====

        CircleRadius,       // 最小外接圆半径
        CircleRow,          // 外接圆圆心行
        CircleColumn,       // 外接圆圆心列
    }

    /// <summary>
    /// 最小梯度阈值模式 — 匹配时响应图低梯度像素过滤策略
    /// Auto:         每层 Triangle Threshold 自适应
    /// Manual:       固定用户指定值
    /// FromTemplate: 使用模板创建时的 Contrast/3
    /// </summary>
    public enum MinContrastMode { Auto, Manual, FromTemplate }

    /// <summary>
    /// 视觉算子集合 - 所有图像/区域操作的唯一入口
    /// </summary>
    /// <remarks>
    /// 设计原则：
    ///   - 类型定义（CvImage/CvRegion）与算法实现完全解耦
    ///   - CvRegion 是纯数据载体，不含任何算法逻辑
    ///   - 所有操作通过本类静态方法执行
    /// 
    /// 签名约定（严格对齐 Halcon）：
    ///   所有算子统一使用 void 返回 + out 参数输出结果。
    ///   Halcon: connection(Region : : : ConnectedRegions)
    ///   C#:    Connection(region, out var connectedRegions)
    /// 
    /// 算子分组：
    ///   📦 图像转换  Rgb1ToGray, ReduceDomain, CropDomain
    ///   ✂️ 阈值分割  Threshold, AdaptiveThreshold, ThresholdRange
    ///   📊 统计     Intensity(单区域), Intensity(多区域)
    ///   🖼️ 显示     ComposeDisplay
    ///   🔬 区域算子  Connection, AreaCenter, SelectShape, Split, SelectObj
    ///                SmallestRectangle1, Rectangularity, GetContour
    ///   📐 轮廓算子  Circularity(XLD), ContLength
    ///   🔧 集合运算  Union1, Difference, Intersection, Complement, Concat
    ///   ⭕ 形态学    DilationCircle, ErosionCircle, DilationRectangle1, ErosionRectangle1
    ///               OpeningCircle, ClosingCircle
    ///   📋 其他    CopyObj
    /// </remarks>
    public static partial class CvOp
    {
    }
}
