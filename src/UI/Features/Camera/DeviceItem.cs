using System;

namespace SunEyeVision.UI.Models
{
    /// <summary>
    /// 设备面板模型
    /// </summary>
    public class DeviceItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsConnected { get; set; }
        public string Status { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }

        public DeviceItem(string id, string name, string type)
        {
            Id = id;
            Name = name;
            Type = type;
            Status = "连";
        }
    }
}
