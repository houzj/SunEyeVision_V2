using System.Collections.ObjectModel;
using System.Reflection;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.DataAdapters;
using SunEyeVision.Plugin.Wpf.Native.Numeric.ParameterPresentation.ParamTabControl;
internal static class S2DocumentEditingCases
{
    private static int passed;
    [STAThread] static int Main()
    {
        try
        {
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            app.Resources.MergedDictionaries.Add(EditorResources.Create());
            using var converter=new PointListRightFixdMultiplicationConverter();
            var source=new ObservableCollection<Point>{new(.1,.2),new(.1,.2),new(.4,.5)};
            var pixels=(ObservableCollection<Point>)converter.Convert(new object[]{source,200d,100d},typeof(object),null!,CultureInfo.InvariantCulture);
            Case("有序坐标Replace只修改重复点中的指定槽，X/Y分别按非方形宽高归一化",()=>
            {
                pixels[1]=new(70,80);Require(source[0]==new Point(.1,.2)&&source[1]==new Point(.35,.8),"重复点槽或宽高错误");
            });
            Case("尺寸重投影不反写规范点，保持原输出集合及有序坐标",()=>
            {
                var before=source.ToArray();var writes=0;source.CollectionChanged+=(_,_)=>writes++;
                var next=converter.Convert(new object[]{source,400d,50d},typeof(object),null!,CultureInfo.InvariantCulture);
                Require(ReferenceEquals(next,pixels)&&before.SequenceEqual(source)&&writes==0&&pixels[1]==new Point(140,40),"尺寸变化污染来源");
            });
            var second=new ObservableCollection<Point>{new(.2,.3),new(.5,.6),new(.8,.9),new(.3,.4)};
            ObservableCollection<Point>? current=null;
            Case("换集合解除旧源及旧输出事件，增删重排Reset完整有序投影",()=>
            {
                current=(ObservableCollection<Point>)converter.Convert(new object[]{second,200d,100d},typeof(object),null!,CultureInfo.InvariantCulture);
                var before=second.ToArray();source[0]=new(.7,.8);pixels[0]=new(1,1);Require(before.SequenceEqual(second),"旧事件污染新源");
                second.Insert(1,new(.9,.1));second.Move(0,3);second.RemoveAt(2);
                Require(current.SequenceEqual(second.Select(p=>new Point(p.X*200,p.Y*100))),"拓扑事件丢槽");
                second.Clear();Require(current.Count==0,"空集合未重投影");second.Add(new(.3,.7));
            });
            Case("转换通知内换根立即投影新源，旧遍历不继续覆盖新集合",()=>
            {
                var third=new ObservableCollection<Point>{new(.6,.2),new(.4,.8)};bool swapped=false;
                current!.CollectionChanged+=(_,_)=>{if(swapped)return;swapped=true;current=(ObservableCollection<Point>)converter.Convert(new object[]{third,300d,50d},typeof(object),null!,CultureInfo.InvariantCulture);};
                second[0]=new(.2,.9);
                Require(swapped&&current.Count==2&&current[0]==new Point(180,10)&&third[0]==new Point(.6,.2),"换根仍用旧集合或旧尺寸");
            });
            Case("投影通知内同源改尺寸与顶点时，合并请求并收敛到最新输入",()=>
            {
                using var projection=new PointListRightFixdMultiplicationConverter();
                var vertices=new ObservableCollection<Point>{new(.1,.2),new(.3,.4),new(.5,.6)};
                var output=(ObservableCollection<Point>)projection.Convert(new object[]{vertices,100d,100d},typeof(object),null!,CultureInfo.InvariantCulture);
                var changed=false;
                output.CollectionChanged+=(_,_)=>
                {
                    if(changed)return;changed=true;
                    projection.Convert(new object[]{vertices,400d,50d},typeof(object),null!,CultureInfo.InvariantCulture);
                    vertices[2]=new(.8,.9);
                };
                projection.Convert(new object[]{vertices,200d,100d},typeof(object),null!,CultureInfo.InvariantCulture);
                Require(changed&&output.SequenceEqual(vertices.Select(p=>new Point(p.X*400,p.Y*50))),"重入请求丢失或旧轮继续覆盖");
            });
            Case("反写通知中同源重排立即废弃其余旧槽，并恢复完整最新顺序",()=>
            {
                using var projection=new PointListRightFixdMultiplicationConverter();
                var vertices=new ReentrantPoints{new(.1,.2),new(.3,.4),new(.5,.6)};
                var output=(ObservableCollection<Point>)projection.Convert(new object[]{vertices,100d,100d},typeof(object),null!,CultureInfo.InvariantCulture);
                var writes=0;vertices.AfterSet=()=>{writes++;if(writes==1)vertices.Move(0,2);};
                var notify=typeof(ObservableCollection<Point>).GetMethod("OnCollectionChanged",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(System.Collections.Specialized.NotifyCollectionChangedEventArgs)},null)!;
                notify.Invoke(output,new object[]{new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Replace,new List<Point>{new(70,80),new(90,95)},new List<Point>{output[0],output[1]},0)});
                Require(writes==1&&vertices[0]==new Point(.3,.4)&&vertices[2]==new Point(.7,.8),"拓扑变化后仍向旧槽写入");
                Require(output.SequenceEqual(vertices.Select(p=>new Point(p.X*100,p.Y*100))),"重排后显示未恢复");
            });
            Case("投影通知内释放立即退出，旧输出后续编辑不再反写来源",()=>
            {
                var projection=new PointListRightFixdMultiplicationConverter();
                var vertices=new ObservableCollection<Point>{new(.1,.2),new(.3,.4)};
                var output=(ObservableCollection<Point>)projection.Convert(new object[]{vertices,100d,100d},typeof(object),null!,CultureInfo.InvariantCulture);
                output.CollectionChanged+=(_,_)=>projection.Dispose();
                projection.Convert(new object[]{vertices,200d,100d},typeof(object),null!,CultureInfo.InvariantCulture);
                output[0]=new(99,99);Require(vertices[0]==new Point(.1,.2),"释放后旧集合仍可写源");
            });
            Case("非法尺寸与释放解除来源；关闭后的转换明确拒绝",()=>
            {
                Require(ReferenceEquals(converter.Convert(new object[]{second,double.NaN,100d},typeof(object),null!,CultureInfo.InvariantCulture),DependencyProperty.UnsetValue),"非有限尺寸被接受");
                converter.Dispose();bool rejected=false;try{converter.Convert(new object[]{second,100d,100d},typeof(object),null!,CultureInfo.InvariantCulture);}catch(ObjectDisposedException){rejected=true;}
                Require(rejected,"释放后继续投影");
            });
            Case("原Point真实ToBtn/T1绑定及轴投影；暂存租约不靠Hash回写集合",()=>
            {
                var view=new SliderArrangePoint{Width=150,Minimum=-30000,Maximum=30000,PointSource=new(123.125,45.625)};
                var points=new ObservableCollection<Point>{new(.1,.2),new(.1,.2)};view.OriPoints=points;
                var window=new Window{Content=view,Width=220,Height=100,Left=-10000,ShowActivated=false,ShowInTaskbar=false};
                try
                {
                    window.Show();Pump();
                    var leaf=(NumericControl)typeof(SliderArrangePoint).GetMethod("EnsurePointInput",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(view,null)!;
                    Require(view.FindName("CBox") is null&&view.FindName("ToBtn") is ToggleButton,"错误使用Float的CBox入口");
                    var binding=BindingOperations.GetBinding(leaf,NumericControl.TextProperty);
                    Require(ReferenceEquals(binding?.Source,view)&&binding!.Path.Path=="StartValue"&&binding.StringFormat=="0.00","原Point数值绑定或格式丢失");
                    Require(ReferenceEquals(typeof(SliderArrangeBase).GetField("T1",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(view),leaf),"原基类未登记Point叶");
                    view.IsX=false;Require(view.StartValue==45.625,"Y轴仍读X");
                    var staged=new List<double>();Func<double,bool> stage=value=>{staged.Add(value);return true;};
                    using var lease=(IDisposable)typeof(SliderArrangePoint).GetMethod("BindStage",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(view,new object[]{stage})!;
                    view.StartValue=90;Require(staged.SequenceEqual(new[]{90d})&&points.All(p=>p==new Point(.1,.2)),"租约未暂存或直接写Hash点");
                    view.PointSource=new(200,60);Require(staged.Count==1&&view.StartValue==60,"外部投影被当成编辑");
                    lease.Dispose();view.StartValue=70;Require(staged.Count==1,"旧租约释放后仍写入");
                }
                finally{window.Close();Pump();}
            });
            Case("原Popup开启时才持有Owner，换目标和关闭完全退订且不重设Auto尺寸",()=>
            {
                var a=new Button{Content="a"};var b=new Button{Content="b"};
                var first=new Window{Content=a,Width=100,Height=80,Left=-10000,ShowActivated=false};var secondWindow=new Window{Content=b,Width=100,Height=80,Left=-10000,ShowActivated=false};
                var popup=new PopupWithTopmost{PlacementTarget=a,PopupPlacementTarget=a,Child=new Border{Width=80,Height=30},AllowsTransparency=true};
                object? Owner()=>typeof(PopupWithTopmost).GetField("_owner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(popup);
                try
                {
                    first.Show();secondWindow.Show();Pump();Require(Owner() is null,"关闭时仍订阅Owner");
                    popup.IsOpen=true;Pump();Require(ReferenceEquals(Owner(),first)&&double.IsNaN(popup.Width)&&double.IsNaN(popup.Height),"开启Owner或Auto尺寸错误");
                    popup.PopupPlacementTarget=b;Require(ReferenceEquals(Owner(),secondWindow),"未改订阅新Owner");first.Close();Pump();Require(popup.IsOpen,"旧Owner关闭影响新弹层");
                    secondWindow.Close();Pump();Require(!popup.IsOpen&&Owner() is null,"Owner关闭后未释放弹层");
                }
                finally{popup.IsOpen=false;first.Close();secondWindow.Close();}
            });
            Console.WriteLine($"Polygon原依赖检查：{passed}组通过；尚不代表Polygon参数/图像/宿主一次接纳完整组合或VM/VS人工验收。");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error.InnerException??error);return 1;}
    }
    private sealed class ReentrantPoints:ObservableCollection<Point>
    {
        internal Action? AfterSet;
        protected override void SetItem(int index,Point point){base.SetItem(index,point);AfterSet?.Invoke();}
    }
    private static void Case(string name,Action action){action();passed++;Console.WriteLine("通过："+name);}
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
}
