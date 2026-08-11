using System.Collections.Generic;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shell;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Shell
{
    /// <summary>
    /// 面板管理器
    /// 管理所有扩展面板
    /// </summary>
    public class PanelManager
    {
        private static readonly PanelManager _instance = new PanelManager();
        public static PanelManager Instance => _instance;

        private readonly Dictionary<string, IPanelExtension> _panels = new Dictionary<string, IPanelExtension>();

        private PanelManager() { }

        /// <summary>
        /// 注册面板
        /// </summary>
        /// <param name="panel">面板扩展</param>
        public void RegisterPanel(IPanelExtension panel)
        {
            if (_panels.ContainsKey(panel.PanelId))
            {
                return;
            }

            _panels[panel.PanelId] = panel;
        }

        /// <summary>
        /// 获取面板
        /// </summary>
        /// <param name="panelId">面板ID</param>
        /// <returns>面板扩展</returns>
        public IPanelExtension GetPanel(string panelId)
        {
            _panels.TryGetValue(panelId, out var panel);
            return panel;
        }

        /// <summary>
        /// 获取所有面板
        /// </summary>
        /// <returns>面板列表</returns>
        public IEnumerable<IPanelExtension> GetAllPanels()
        {
            return _panels.Values;
        }

        /// <summary>
        /// 注销面板
        /// </summary>
        /// <param name="panelId">面板ID</param>
        public void UnregisterPanel(string panelId)
        {
            _panels.Remove(panelId);
        }
    }
}
