using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.UI.Extensions;
using SunEyeVision.UI.Models;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Performance
{
    /// <summary>
    /// 工作流执行协调器 - 整合所有优化组件
    /// 提供图像切换优化的完整解决方案
    /// </summary>
    public class WorkflowExecutionOrchestrator : IDisposable
    {
        private readonly WorkflowEnginePool _enginePool;
        private readonly PreExecutionCache _cache;
        private readonly ImageConverterPool _imageConverterPool;
        private readonly BatchUIUpdater _batchUpdater;
        private readonly NodeResultManager _nodeResultManager;
        private bool _disposed;

        /// <summary>
        /// 创建工作流执行协调器
        /// </summary>
        /// <param name="nodeResultManager">节点结果管理器</param>
        /// <param name="maxCacheSize">最大缓存项数</param>
        /// <param name="maxMemoryMB">最大缓存内存（MB）</param>
        public WorkflowExecutionOrchestrator(
            NodeResultManager nodeResultManager,
            int maxCacheSize = 50,
            int maxMemoryMB = 100)
        {
            _nodeResultManager = nodeResultManager ?? throw new ArgumentNullException(nameof(nodeResultManager));

            _enginePool = new WorkflowEnginePool(maxPoolSize: 3);
            _cache = new PreExecutionCache(maxCacheSize, maxMemoryMB);
            _imageConverterPool = new ImageConverterPool(maxCacheSize: 10);
            _batchUpdater = new BatchUIUpdater(updateInterval: 16, maxBatchSize: 10);
        }

        /// <summary>
        /// 执行工作流并显示结果（带缓存优化）
        /// </summary>
        /// <param name="node">工作流节点（UI模型）</param>
        /// <param name="imageSourceId">图像源ID</param>
        /// <param name="inputImage">输入图像（HImage）</param>
        /// <param name="workflow">工作流实例</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>执行结果</returns>
        public async Task<CachedExecutionResult?> ExecuteAndDisplayAsync(
            UI.Models.WorkflowNode node,
            string imageSourceId,
            HImage inputImage,
            SunEyeVision.Workflow.Workflow workflow,
            CancellationToken cancellationToken = default)
        {
            if (node == null || inputImage == null || !inputImage.IsInitialized())
                return null;

            // 1. 检查缓存
            if (_cache.TryGet(node.Id, imageSourceId, out var cachedResult))
            {
                // 缓存命中，直接显示
                _batchUpdater.Enqueue(() =>
                {
                    DisplayCachedResult(node, cachedResult!);
                });

                return cachedResult;
            }

            // 2. 缓存未命中，执行工作流
            var engine = _enginePool.Rent();

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // 执行节点
                var result = await Task.Run(() =>
                {
                    return ExecuteNode(engine, node, inputImage, workflow, cancellationToken);
                }, cancellationToken);

                stopwatch.Stop();

                if (result == null || !result.IsSuccess)
                {
                    return null;
                }

                // 3. 获取输出图像
                var outputMat = GetOutputHImage(result);
                if (outputMat == null || !outputMat.IsInitialized())
                {
                    return null;
                }

                // 4. 转换为BitmapSource
                var processedImage = _imageConverterPool.ConvertToBitmapSource(outputMat);
                var originalImage = _imageConverterPool.ConvertToBitmapSource(inputImage);

                // 5. 添加到缓存
                _cache.Add(node.Id, imageSourceId, processedImage, originalImage, stopwatch.ElapsedMilliseconds);

                // 6. 创建缓存结果
                cachedResult = new CachedExecutionResult
                {
                    NodeId = node.Id,
                    ImageSourceId = imageSourceId,
                    ProcessedImage = processedImage,
                    OriginalImage = originalImage,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };

                // 7. 批量更新UI
                _batchUpdater.Enqueue(() =>
                {
                    DisplayCachedResult(node, cachedResult);
                });

                return cachedResult;
            }
            finally
            {
                _enginePool.Return(engine);
            }
        }

        /// <summary>
        /// 预执行工作流（后台预加载）
        /// </summary>
        /// <param name="node">工作流节点（UI模型）</param>
        /// <param name="imageSourceId">图像源ID</param>
        /// <param name="inputMat">输入图像</param>
        /// <param name="workflow">工作流实例</param>
        public void PreExecute(
            UI.Models.WorkflowNode node,
            string imageSourceId,
            HImage inputMat,
            SunEyeVision.Workflow.Workflow workflow)
        {
            if (node == null || inputMat == null || !inputMat.IsInitialized())
                return;

            // 检查缓存是否已存在
            if (_cache.TryGet(node.Id, imageSourceId, out _))
                return;

            // 后台执行（不更新UI）
            Task.Run(() =>
            {
                try
                {
                    var engine = _enginePool.Rent();

                    try
                    {
                        var result = ExecuteNode(engine, node, inputMat, workflow, CancellationToken.None);

                        if (result != null && result.IsSuccess)
                        {
                            var outputMat = GetOutputHImage(result);
                            if (outputMat != null && outputMat.IsInitialized())
                            {
                                var processedImage = _imageConverterPool.ConvertToBitmapSource(outputMat);
                                var originalImage = _imageConverterPool.ConvertToBitmapSource(inputMat);

                                _cache.Add(node.Id, imageSourceId, processedImage, originalImage);
                            }
                        }
                    }
                    finally
                    {
                        _enginePool.Return(engine);
                    }
                }
                catch (Exception ex)
                {
}
            });
        }

        /// <summary>
        /// 快速切换到已缓存的节点结果
        /// </summary>
        /// <param name="node">节点（UI模型）</param>
        /// <param name="imageSourceId">图像源ID</param>
        /// <returns>是否成功切换</returns>
        public bool QuickSwitchNodeAsync(UI.Models.WorkflowNode node, string imageSourceId)
        {
            if (_cache.TryGet(node.Id, imageSourceId, out var cachedResult))
            {
                _batchUpdater.Enqueue(() =>
                {
                    DisplayCachedResult(node, cachedResult!);
                });

                return true;
            }

            return false;
        }

        /// <summary>
        /// 执行节点
        /// </summary>
        private ToolResults? ExecuteNode(
            WorkflowEngine engine,
            UI.Models.WorkflowNode node,
            HImage inputMat,
            SunEyeVision.Workflow.Workflow workflow,
            CancellationToken cancellationToken)
        {
            try
            {
                // 简化实现：直接执行工作流（实际项目中需要根据具体实现调整）
                // 这里返回null表示功能待实现
                // TODO: 根据实际的工作流引擎实现此方法
// 占位返回
                return null;
            }
            catch (Exception ex)
            {
return null;
            }
        }

        /// <summary>
        /// 从结果中获取输出Mat
        /// </summary>
        private HImage? GetOutputHImage(ToolResults result)
        {
            try
            {
                var outputImageProp = result.GetType().GetProperty("OutputImage");
                if (outputImageProp != null)
                {
                    var value = outputImageProp.GetValue(result);
                    if (value is HImage image)
                    {
                        return image;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// 显示缓存结果
        /// </summary>
        private void DisplayCachedResult(UI.Models.WorkflowNode node, CachedExecutionResult cachedResult)
        {
            _nodeResultManager.UpdateNodeImageFromCache(node, cachedResult);
        }
        /// 获取缓存统计信息
        /// </summary>
        public (int Count, long MemoryBytes, double MemoryMB) GetCacheStatistics()
        {
            return _cache.GetStatistics();
        }

        /// <summary>
        /// 清空缓存
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// 刷新所有待处理的UI更新
        /// </summary>
        public void FlushPendingUpdates()
        {
            _batchUpdater.Flush();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _enginePool.Dispose();
            _cache.Dispose();
            _imageConverterPool.Dispose();
            _batchUpdater.Dispose();
        }
    }
}
