using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;

namespace SunEyeVision.CvEngine
{
    public static partial class CvOp
    {
        #region 🔧 集合运算

        /// <summary>区域并集 - 对应 Halcon union1(Regions : : : RegionUnion)</summary>
        public static void Union1(CvRegion region, out CvRegion unionRegion)
        {
            if (region.IsEmpty)
            {
                unionRegion = CvRegion.GenEmptyObj();
                return;
            }
            var combinedMask = region.Mask;
            unionRegion = new CvRegion(combinedMask?.Clone(), region.ImageSize);
        }

        /// <summary>区域差集 - 对应 Halcon difference(Sub, Pattern : : : ResultDifference)</summary>
        public static void Difference(CvRegion sub, CvRegion pattern, out CvRegion resultDifference)
        {
            if (sub.IsEmpty)
            {
                resultDifference = CvRegion.GenEmptyObj();
                return;
            }
            if (pattern.IsEmpty)
            {
                resultDifference = new CvRegion(sub.Mask?.Clone(), sub.ImageSize);
                return;
            }

            var subMask = sub.Mask!;
            var patternMask = pattern.Mask!;

            using var notPattern = new Mat();
            Cv2.BitwiseNot(patternMask, notPattern);

            var resultMask = new Mat();
            Cv2.BitwiseAnd(subMask, notPattern, resultMask);
            resultDifference = new CvRegion(resultMask, sub.ImageSize);
        }

        /// <summary>区域交集 - 对应 Halcon intersection(Region1, Region2 : : : ResultIntersection)</summary>
        public static void Intersection(CvRegion region1, CvRegion region2, out CvRegion resultIntersection)
        {
            if (region1.IsEmpty || region2.IsEmpty)
            {
                resultIntersection = CvRegion.GenEmptyObj();
                return;
            }

            var resultMask = new Mat();
            Cv2.BitwiseAnd(region1.Mask!, region2.Mask!, resultMask);
            resultIntersection = new CvRegion(resultMask, region1.ImageSize);
        }

        /// <summary>区域补集 - 对应 Halcon complement(Region : : : ResultComplement)</summary>
        public static void Complement(CvRegion region, out CvRegion resultComplement)
        {
            if (region.IsEmpty)
            {
                resultComplement = CvRegion.GenEmptyObj();
                return;
            }

            var resultMask = new Mat();
            Cv2.BitwiseNot(region.Mask!, resultMask);
            resultComplement = new CvRegion(resultMask, region.ImageSize);
        }

        /// <summary>拼接两个区域 - 对应 Halcon concat_obj(Objects1, Objects2 : : : ConcatObjects)</summary>
        public static void Concat(CvRegion region, CvRegion other, out CvRegion concatResult)
        {
            if (region.IsEmpty && other.IsEmpty)
            {
                concatResult = CvRegion.GenEmptyObj();
                return;
            }
            if (other.IsEmpty)
            {
                concatResult = region.CopyObj();
                return;
            }
            if (region.IsEmpty)
            {
                concatResult = other.CopyObj();
                return;
            }

            var resultMask = new Mat();
            Cv2.BitwiseOr(region.Mask!, other.Mask!, resultMask);

            var items = new List<RegionItem>(region.Count + other.Count);
            foreach (var item in region._items)
            {
                var m = item.GetMask();
                items.Add(m != null && !m.Empty() ? new RegionItem(m.Clone()) : new RegionItem(new Mat()));
            }
            foreach (var item in other._items)
            {
                var m = item.GetMask();
                items.Add(m != null && !m.Empty() ? new RegionItem(m.Clone()) : new RegionItem(new Mat()));
            }

            concatResult = new CvRegion(items, region.ImageSize);
        }

        #endregion
    }
}
