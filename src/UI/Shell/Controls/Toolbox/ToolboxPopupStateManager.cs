using System;

namespace SunEyeVision.UI.Shell.Controls.Toolbox
{
    /// <summary>
    /// Popup状态机管理器 - 管理Toolbox Popup的生命周期和状态转换
    /// </summary>
    public class ToolboxPopupStateManager
    {
        public enum PopupState
        {
            Idle,           // 空闲状态
            Hovering,       // 悬停中
            Opened,         // 已打开
            Dragging,       // 拖拽中
            Closing         // 关闭中
        }

        private PopupState _currentState = PopupState.Idle;

        public event EventHandler<PopupState>? StateChanged;

        public PopupState CurrentState
        {
            get => _currentState;
            private set
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    StateChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// 是否允许打开Popup
        /// </summary>
        public bool CanOpen()
        {
            return CurrentState == PopupState.Idle || CurrentState == PopupState.Hovering;
        }

        /// <summary>
        /// 是否允许关闭Popup
        /// </summary>
        public bool CanClose()
        {
            return CurrentState == PopupState.Opened || CurrentState == PopupState.Hovering;
        }

        /// <summary>
        /// 转换到悬停状态
        /// </summary>
        public void ToHovering()
        {
            if (CanOpen())
            {
                CurrentState = PopupState.Hovering;
            }
        }

        /// <summary>
        /// 转换到打开状态
        /// </summary>
        public void ToOpened()
        {
            CurrentState = PopupState.Opened;
        }

        /// <summary>
        /// 转换到拖拽状态
        /// </summary>
        public void ToDragging()
        {
            CurrentState = PopupState.Dragging;
        }

        /// <summary>
        /// 转换到关闭状态
        /// </summary>
        public void ToClosing()
        {
            if (CanClose())
            {
                CurrentState = PopupState.Closing;
            }
        }

        /// <summary>
        /// 转换到空闲状态
        /// </summary>
        public void ToIdle()
        {
            CurrentState = PopupState.Idle;
        }

        /// <summary>
        /// 重置状态到初始状态
        /// </summary>
        public void Reset()
        {
            CurrentState = PopupState.Idle;
        }

        /// <summary>
        /// 从拖拽状态恢复
        /// </summary>
        public void RecoverFromDragging()
        {
            // 拖拽结束后，如果是打开状态，保持打开；否则进入空闲状态
            if (CurrentState == PopupState.Dragging)
            {
                CurrentState = PopupState.Opened;
            }
        }
    }
}
