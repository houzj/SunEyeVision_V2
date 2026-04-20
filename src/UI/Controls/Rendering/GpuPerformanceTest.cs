using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace SunEyeVision.UI.Controls.Rendering
{
    /// <summary>
    /// CPU vs GPU性能对比测试工具
    /// 测试三种加载方式的性能差异
    /// </summary>
    public class GpuPerformanceTest
    {
        /// <summary>
        /// 性能测试结果
        /// </summary>
        public class TestResult
        {
            public string TestName { get; set; } = "";
            public double TotalTimeMs { get; set; }
            public double AvgTimeMs { get; set; }
            public double Speedup { get; set; }
            public bool IsBest { get; set; }
        }

        /// <summary>
        /// 运行完整的性能对比测试
        /// </summary>
        public static void RunComparisonTest(string testImagePath, int testSize = 80, int iterations = 100)
        {
            if (!File.Exists(testImagePath))
            {
return;
            }
            // 预热
TestPureCPU(testImagePath, testSize, 10, silent: true);
            TestWPFDefault(testImagePath, testSize, 10, silent: true);
            
            using var gpuLoader = new DirectXGpuThumbnailLoader();
            gpuLoader.Initialize();
            if (gpuLoader.IsGpuAvailable)
            {
                TestDirectXGPU(testImagePath, testSize, 10, gpuLoader, silent: true);
            }
// 正式测试
var results = new List<TestResult>();

            // 测试1: 纯CPU（System.Drawing）
var cpuResult = TestPureCPU(testImagePath, testSize, iterations, silent: false);
            results.Add(cpuResult);

            // 测试2: WPF默认（WPF的GPU加速）
var wpfResult = TestWPFDefault(testImagePath, testSize, iterations, silent: false);
            results.Add(wpfResult);

            // 测试3: DirectX GPU加速
            if (gpuLoader.IsGpuAvailable)
            {
var gpuResult = TestDirectXGPU(testImagePath, testSize, iterations, gpuLoader, silent: false);
                results.Add(gpuResult);
            }

            // 打印汇总
            PrintSummary(results, iterations);
        }

        /// <summary>
        /// 测试1: 纯CPU模式
        /// </summary>
        private static TestResult TestPureCPU(string filePath, int size, int count, bool silent = false)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                LoadWithPureCPU(filePath, size);
            }
            sw.Stop();

            if (!silent)
            {
            }

            return new TestResult
            {
                TestName = "纯CPU (System.Drawing)",
                TotalTimeMs = sw.ElapsedMilliseconds,
                AvgTimeMs = sw.ElapsedMilliseconds / (double)count,
                IsBest = false
            };
        }

        /// <summary>
        /// 纯CPU加载方式
        /// </summary>
        private static BitmapImage LoadWithPureCPU(string filePath, int size)
        {
            using var bitmap = new Bitmap(filePath);
            int width = size;
            int height = (int)(bitmap.Height * ((double)size / bitmap.Width));

            // CPU缩放
            using var scaled = new Bitmap(width, height);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(bitmap, 0, 0, width, height);
            }

            // 转换为WPF
            var ms = new MemoryStream();
            scaled.Save(ms, ImageFormat.Png);
            ms.Position = 0;

            var wpfBitmap = new BitmapImage();
            wpfBitmap.BeginInit();
            wpfBitmap.CacheOption = BitmapCacheOption.OnLoad;
            wpfBitmap.StreamSource = ms;
            wpfBitmap.EndInit();
            wpfBitmap.Freeze();

            return wpfBitmap;
        }

        /// <summary>
        /// 测试2: WPF默认模式
        /// </summary>
        private static TestResult TestWPFDefault(string filePath, int size, int count, bool silent = false)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                LoadWithWPFDefault(filePath, size);
            }
            sw.Stop();

            if (!silent)
            {
            }

            return new TestResult
            {
                TestName = "WPF默认",
                TotalTimeMs = sw.ElapsedMilliseconds,
                AvgTimeMs = sw.ElapsedMilliseconds / (double)count,
                IsBest = false
            };
        }

        /// <summary>
        /// WPF默认加载方式
        /// </summary>
        private static BitmapImage LoadWithWPFDefault(string filePath, int size)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(filePath);
            bitmap.DecodePixelWidth = size;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// 测试3: DirectX GPU加速模式
        /// </summary>
        private static TestResult TestDirectXGPU(string filePath, int size, int count, DirectXGpuThumbnailLoader gpuLoader, bool silent = false)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                gpuLoader.LoadThumbnail(filePath, size);
            }
            sw.Stop();

            if (!silent)
            {
            }

            return new TestResult
            {
                TestName = "DirectX GPU",
                TotalTimeMs = sw.ElapsedMilliseconds,
                AvgTimeMs = sw.ElapsedMilliseconds / (double)count,
                IsBest = false
            };
        }

        /// <summary>
        /// 打印测试结果汇总
        /// </summary>
        private static void PrintSummary(List<TestResult> results, int iterations)
        {
// 找出最快的
            TestResult? fastest = null;
            foreach (var result in results)
            {
                if (fastest == null || result.AvgTimeMs < fastest.AvgTimeMs)
                {
                    fastest = result;
                }
            }
            
            if (fastest == null) return;
            
            fastest.IsBest = true;

            // 计算加速比
            foreach (var result in results)
            {
                result.Speedup = result.AvgTimeMs > 0 ? result.AvgTimeMs / fastest.AvgTimeMs : 1;
            }

            // 打印结果表格
foreach (var result in results)
            {
                string mark = result.IsBest ? "最快" : "";
                string name = result.TestName.PadRight(30);
                string total = result.TotalTimeMs.ToString().PadLeft(12);
                string avg = result.AvgTimeMs.ToString("F3").PadLeft(12);
                string speedup = result.IsBest ? "1.00x" : $"{result.Speedup:F2}x";
            }
        }

        /// <summary>
        /// 快速测试单张图像的加载性能
        /// </summary>
        public static void QuickTest(string testImagePath, int testSize = 80)
        {
            if (!File.Exists(testImagePath))
            {
return;
            }
// 纯CPU
            var sw = Stopwatch.StartNew();
            var cpuBitmap = LoadWithPureCPU(testImagePath, testSize);
            sw.Stop();
// WPF默认
            sw.Restart();
            var wpfBitmap = LoadWithWPFDefault(testImagePath, testSize);
            sw.Stop();
// DirectX GPU
            using var gpuLoader = new DirectXGpuThumbnailLoader();
            gpuLoader.Initialize();
            if (gpuLoader.IsGpuAvailable)
            {
                sw.Restart();
                var gpuBitmap = gpuLoader.LoadThumbnail(testImagePath, testSize);
                sw.Stop();
}
            else
            {
}
}
    }
}
