using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
namespace SunEyeVision.UI.Features.WorkflowUI;
/// <summary>标签页仅持有真实流程的投影、视口及呈现状态，编辑历史由方案会话拥有。</summary>
public partial class WorkflowTabViewModel : ObservableObject
{
    private string _id=Guid.NewGuid().ToString("N"),_name="工作流",_currentWorkflowName="工作流";
    private DocumentSession? _canvasSession;
    private WorkflowDefinition? _rootDefinition;
    private bool _rootRetired;
    private bool _isRunning;
    private RunMode _runMode;
    private WorkflowState _state;
    private ScaleTransform _scaleTransform=new(1,1);
    private double _currentScale=1;
    private CanvasType _canvasType=CanvasType.WorkflowCanvas;
    private static readonly ReadOnlyObservableCollection<CanvasNodeProjection> EmptyNodes=new(new ObservableCollection<CanvasNodeProjection>());
    private static readonly ReadOnlyObservableCollection<CanvasConnectionProjection> EmptyConnections=new(new ObservableCollection<CanvasConnectionProjection>());
    public WorkflowTabViewModel() { }
    public string Id { get=>_id;set{if(_canvasSession is not null&&value!=_id)throw new InvalidOperationException("已绑定标签页不能替换流程身份。");if(SetProperty(ref _id,value)){OnPropertyChanged(nameof(RootWorkflowId));OnPropertyChanged(nameof(CurrentWorkflowId));}} }
    public string RootWorkflowId=>Id;
    public string CurrentWorkflowId=>Id;
    public string Name {get=>_name;set{if(_canvasSession is not null){if(value!=_name)RenameRequested?.Invoke(value);return;}if(SetProperty(ref _name,value)){_currentWorkflowName=value;OnPropertyChanged(nameof(EffectiveDisplayName));}}}
    internal WorkflowDefinition? ActualRoot=>_rootDefinition;
    internal bool OwnsRoot(DocumentSession session,WorkflowDefinition root)=>!_rootRetired&&ReferenceEquals(_canvasSession,session)&&ReferenceEquals(_rootDefinition,root);
    internal void RetireRootProjection()=>_rootRetired=true;
    internal bool IsRootProjectionRetired=>_rootRetired;
    public string EffectiveDisplayName=>_currentWorkflowName;
    public bool IsInSubroutine=>false;
    public string? SubroutineIcon=>null;
    public string? CurrentOwnerNodeId=>null;
    public IReadOnlyList<string> NavigationAncestorIds=>Array.Empty<string>();
    public RootWorkspaceSettings RootSettings=>_rootDefinition?.WorkspaceSettings??new();
    public IReadOnlyList<NodeDefinition> RootWorkflowNodes=>_rootDefinition?.Nodes??Array.Empty<NodeDefinition>();
    public ReadOnlyObservableCollection<CanvasNodeProjection> WorkflowNodes=>DocumentCanvas?.Nodes??EmptyNodes;
    public ReadOnlyObservableCollection<CanvasConnectionProjection> WorkflowConnections=>DocumentCanvas?.Connections??EmptyConnections;
    public event EventHandler? WorkflowContentChanging;
    public event EventHandler? WorkflowContentChanged;
    internal void SynchronizeRootName(string name){_name=name;_currentWorkflowName=name;OnPropertyChanged(nameof(Name));OnPropertyChanged(nameof(EffectiveDisplayName));}
        #region 行内重命名

        private bool _isEditingName;
        private string _renameBuffer = string.Empty;
        private long _renameRevision;

        /// <summary>
        /// 根集合事务请求；实际名称只能由正式回执更新。
        /// </summary>
        internal Func<string,bool>? RenameRequested {get;set;}

        /// <summary>
        /// 是否正在编辑名称
        /// </summary>
        public bool IsEditingName
        {
            get => _isEditingName;
            set => SetProperty(ref _isEditingName, value);
        }

        /// <summary>
        /// 编辑中的临时名称缓冲
        /// </summary>
        public string RenameBuffer
        {
            get => _renameBuffer;
            set { if(SetProperty(ref _renameBuffer, value))_renameRevision++; }
        }

        /// <summary>
        /// 开始重命名（复制当前显示名称到缓冲）
        /// 子程序下钻时显示子程序名，根层次显示 Tab.Name
        /// </summary>
        public void BeginRename()
        {
            RenameBuffer = EffectiveDisplayName;
            IsEditingName = true;
        }

        /// <summary>
        /// 确认重命名 — 请求当前实际根的集合事务。
        /// 成功才结束编辑；失败及回执期间的新原文本继续保留。
        /// </summary>
        public bool ConfirmRename()
        {
            var text = RenameBuffer;
            var revision = _renameRevision;
            if(RenameRequested?.Invoke(text)!=true)return false;
            // A receipt may arrive after a view has received another raw edit.
            if(revision==_renameRevision){RenameBuffer=Name;IsEditingName=false;}
            return true;
        }

        /// <summary>
        /// 取消重命名
        /// </summary>
        public void CancelRename()
        {
            IsEditingName = false;
        }

        #endregion

        /// <summary>
        /// 设置当前显示的工作流名称（由外部在导航后调用）
        /// </summary>
        public void SetCurrentWorkflowName(string name)
        {
            _currentWorkflowName = name;
            OnPropertyChanged(nameof(EffectiveDisplayName));
        }


        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    State = value ? WorkflowState.Running : WorkflowState.Stopped;
                    OnPropertyChanged(nameof(ContinuousRunButtonText));
                }
            }
        }

        /// <summary>
        /// 运行模式
        /// </summary>
        public RunMode RunMode
        {
            get => _runMode;
            set => SetProperty(ref _runMode, value);
        }

        /// <summary>
        /// 工作流状态
        /// </summary>
        public WorkflowState State
        {
            get => _state;
            set{if(SetProperty(ref _state,value)){OnPropertyChanged(nameof(StateText));OnPropertyChanged(nameof(StateColor));}}
        }


    public Point ViewportCanvasOrigin{get;private set;}
    public bool HasSavedViewport{get;private set;}
    public void CaptureViewport(double horizontalOffset,double verticalOffset)
    {
        if(!double.IsFinite(horizontalOffset)||!double.IsFinite(verticalOffset)||!double.IsFinite(CurrentScale)||CurrentScale<=0)return;
        ViewportCanvasOrigin=new(Math.Max(0,horizontalOffset)/CurrentScale,Math.Max(0,verticalOffset)/CurrentScale);HasSavedViewport=true;
    }
        public ScaleTransform ScaleTransform
        {
            get => _scaleTransform;
            set => SetProperty(ref _scaleTransform, value);
        }

        /// <summary>
        /// 当前缩放比例（默认1.0即100%）
        /// </summary>
        public double CurrentScale
        {
            get => _currentScale;
            set => SetProperty(ref _currentScale, value);
        }

        /// <summary>
        /// 画布类型
        /// </summary>
        public CanvasType CanvasType
        {
            get => _canvasType;
            set => SetProperty(ref _canvasType, value);
        }

        /// <summary>
        /// 单次运行按钮文本
        /// </summary>
        public string SingleRunButtonText => "▶ 运行";

        /// <summary>
        /// 连续运行按钮文本
        /// </summary>
        public string ContinuousRunButtonText => IsRunning ? "⏹ 停止" : "⏵ 连续";

        /// <summary>
        /// 是否可关闭
        /// </summary>
        public bool IsCloseable => true;

        /// <summary>
        /// 获取状态显示文本
        /// </summary>
        public string StateText
        {
            get
            {
                return State switch
                {
                    WorkflowState.Stopped => "已停止",
                    WorkflowState.Running => "运行中",
                    WorkflowState.Paused => "已暂停",
                    WorkflowState.Error => "错误",
                    _ => "未知"
                };
            }
        }

        /// <summary>
        /// 获取状态颜色
        /// </summary>
        public string StateColor
        {
            get
            {
                return State switch
                {
                    WorkflowState.Stopped => "#999999",
                    WorkflowState.Running => "#00CC99",
                    WorkflowState.Paused => "#FF9900",
                    WorkflowState.Error => "#FF4444",
                    _ => "#999999"
                };
            }
        }


    public void RefreshProperty(string propertyName)=>OnPropertyChanged(propertyName);
    public string[] GetNavigationPath()=>new[]{Name};
}
public enum WorkflowState{Stopped,Running,Paused,Error}
