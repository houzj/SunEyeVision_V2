using System;

namespace SunEyeVision.Plugin.SDK.Core;

/// <summary>
/// 插件声明的宿主契约版本。
/// 该元数据描述接口语义，不承担 CLR 程序集绑定身份。
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ToolContractVersionAttribute : Attribute
{
    // MSBuild AssemblyAttribute 通过字符串参数生成 AssemblyInfo；保留字符串重载，
    // 让构建注入与源码中的整数特性用法都产生同一份元数据。
    public ToolContractVersionAttribute(string major, string minor)
        : this(int.Parse(major), int.Parse(minor))
    {
    }

    public ToolContractVersionAttribute(int major, int minor)
    {
        if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
        if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));

        Major = major;
        Minor = minor;
    }

    public int Major { get; }

    public int Minor { get; }
}
