# D07 公共视口接入合同检查点

- 执行者：root；唯一公共库写者。宿主线在独立工作树整体接入，不复制原控件或交互代码。
- 本文件冻结接入签名，不代表完整实现或正式Release验收完成。库层005零错误、候选原结果窗口27/0；仍需集中规定UI/full、新生产回归及真实VS/VM/人工。

## 唯一原视口与帧

命名空间 `SunEyeVision.Plugin.Wpf.Presenters.Display`：

```csharp
NativeResultViewport(NativeResultRasterLimits? limits = null,
                     NativeResultShapeLimits? shapeLimits = null);
static NativeResultViewport Attach(FrameworkElement originalView,
    NativeResultRasterLimits? limits = null, NativeResultShapeLimits? shapeLimits = null);
FrameworkElement View { get; }
ImageLayout? Layout { get; }
NativeResultFrameReceipt? CurrentFrame { get; }
bool BindImage(IReadOnlyImage image, Func<bool> isCurrent);
byte[]? ReadPixel(int x, int y);
void RefreshCurrentness();
void Clear();
void Dispose();
```

`NativeResultFrameReceipt(Guid OwnerId, long Epoch, ImageFrameAddress Frame, CoordinateFrame Coordinates)` 是本实例安装回执。读取属性本身不是当前性证明；各操作都会复核 source/publication closure、owner、epoch。不要给旧回执重新贴当前Frame。

每侧Shell实际原件仅一个owner；Attach拒第二owner及ImageDataSource/ShapeList/ROIList现存绑定。Shell生产整批删除InputImage/OutputImage DP及其绑定，提供只读 `FrameworkElement InputViewport/OutputViewport` 挂点；该专属组合改动由宿主线负责。公共owner不调SDK、工作流、算法或查latest。

## 独立显示子层

```csharp
RegionLayer CreateRegionLayer(int zIndex = 0);
GeometryLayer CreateGeometryLayer(int order = 0);
GeometryLayer CreateSelectionLayer(int order = 100);
TextLayer CreateTextLayer(int order = 10);
```

- RegionLayer：`bool Present(receipt, long revision, IReadOnlyRegion region, Color color, Func<bool> isCurrent)`；`Clear/Dispose` 仅撤本栅格层。实际RLE完整读取、预算整批拒绝；zIndex只在原ImageLayers范围，不跨原Shape/ROI页面改层级。
- GeometryLayer：`bool Present(receipt, long revision, IReadOnlyList<NativeGeometryOverlay> values, Func<bool> isCurrent)`。
- `NativeGeometryOverlay(ImageFrameAddress SourceFrame, GeometryValue Geometry, Color Stroke, double Thickness=1, double Opacity=1)`；物理Frame及Geometry.Frame都严格匹配回执。仅像素坐标，不推断模型变换。点、线、圆、旋转矩形、椭圆、多边形及整圆Arc有原入口；非整圆Arc精确原入口尚缺，明确拒绝，不近似。
- TextLayer：`bool Present(receipt, long revision, IReadOnlyList<NativeTextOverlay> values, Func<bool> isCurrent)`；`NativeTextOverlay(string Content, Point Position, int FontSize, Color Color, double Opacity)` 是宿主已解析的该帧像素文字，无表达式执行。
- 几何、文字及选择层 `Clear/Dispose` 仅撤本层。选择复用同一几何管线。revision在各层单独递增；换帧清旧值及revision，层对象可以使用新receipt重新呈现。规范资源不变，显示Domain仅修改私有alpha；ReadPixel返回原格式及alpha，保行padding语义。
- 宿主publication失效时调用RefreshCurrentness或显式清其层；不以定时轮询/页面刷新查询最新结果。背景来源publication和结果publication是不同租约，切背景不擅自退休结果表publication。

## 同帧 ROI 编辑

```csharp
INativeRoiEditLease BeginRoiEditing(NativeResultFrameReceipt receipt,
    NativeRoiSession session, Func<bool> isCurrent);

public interface INativeRoiEditLease : IDisposable {
    bool IsCurrent { get; }
    bool HasPendingGesture { get; }
    void CancelGesture();
}
```

Session来自 `SunEyeVision.Plugin.Wpf.Adapters.Regions`。宿主必须用**当前节点实际冻结输入图像**、相同物理Frame的RoiEditStamp和正式私有草稿/history接纳创建，持源借阅直到scope退场。不得拿任选输出/背景、Bitmap标签、尺寸或列表索引猜配对。

`RoiDeclarations.ImageFrame=image:input` 是ROI参数声明的逻辑输入槽位；Session构造固定实际图像Frame/Domain坐标/尺寸，Capture核每份Stamp.Frame及scope/revision，原ROI记录按该图像像素值归一化再由原页恢复位置。此明确输入实例绑定不要求Domain ID改名。Begin仍核实际Session.Coordinates、Frame、Stamp.Frame、尺寸、owner/epoch/current；这一规则不能推广为任意GeometryValue跨坐标映射。

Attachment只借已安装图像，不Acquire第二像素lease，不Dispose Session；原绘制/选择/菜单/四种手柄/取消实现移至内部NativeRoiAttachment，两个独立ROI/文字包装器也使用相同owner。ROI退场不能清背景、结果、文字或选择层。

`HasPendingGesture` 核真实绘制请求/手柄状态及待定投影；Apply/Save须先核IsCurrent，再在未完成手势时拒绝提交。Esc、切页、关闭使用CancelGesture；最终Dispose释放事件及借用scope。宿主不得另写鼠标捕获或暴露原Canvas。

当前原编辑仅矩形、完整圆环、扇形、多边形。circle/point/line/arc等未接原编辑入口的项、非整圆Arc显示及Group不能冒称已迁完；保完整草稿，显式未完成能力，不用旧SDK画布后备。此处不缩减原阶段一总清单。

## 后续集中批

当前候选无live构建/测试。先保存公共候选、合宿主固定D06修复批，主仓唯一UI/full及正式EXE，再新GlobalBinding/ImageSources/原视口与既有ROI/文字关联回归；旧候选成绩不套新产物。随后宿主整片接原Shell、来源选择、结果/调试/历史、全屏、ROI工厂及旧主SDK视觉消费者退出。用户Blob参数XAML不改不暂存，Blob业务第二阶段仍冻结。
