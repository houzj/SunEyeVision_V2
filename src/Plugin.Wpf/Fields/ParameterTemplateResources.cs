using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Fields;

/// <summary>
/// 工具贡献的参数行模板字典（对齐原厂 <c>ParamUserControl.xaml.cs:59-72</c> 的
/// <c>MergedDictionaries.Add</c>）。
/// </summary>
/// <remarks>
/// 与原厂的两处必然差异：
/// ① 原厂工具不卸载，本方插件是隔离加载且可卸载的，因此登记必须带属主、并支持移除；
/// ② 原厂靠 WPF 自身的“先查本体、再倒序查合并字典”实现“平台键优先于工具键”，
///    这里把该顺序显式写出来，保证同名键仍是平台胜出（工具只能**新增** key，不能覆盖）。
/// </remarks>
public static class ParameterTemplateResources
{
    /// <summary>工具约定路径（原厂同名约定：<c>{Assembly};component/DataTemplate/ParamUserControlDataTemplate.xaml</c>）。</summary>
    public const string ConventionPath = "DataTemplate/ParamUserControlDataTemplate.xaml";

    private static readonly List<Registration> Entries = new();

    private sealed record Registration(string Owner, ResourceDictionary Dictionary);

    /// <summary>当前登记的全部属主（测试与诊断用）。</summary>
    public static IReadOnlyList<string> Owners
    {
        get
        {
            lock (Entries) return Entries.Select(entry => entry.Owner).ToArray();
        }
    }

    /// <summary>登记一份工具模板字典；同一属主重复登记按覆盖处理（对应原厂“每程序集只合一次”）。</summary>
    public static void Register(string owner, ResourceDictionary dictionary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentNullException.ThrowIfNull(dictionary);
        lock (Entries)
        {
            Entries.RemoveAll(entry => string.Equals(entry.Owner, owner, StringComparison.Ordinal));
            Entries.Add(new Registration(owner, dictionary));
        }
    }

    /// <summary>移除工具模板字典（插件卸载路径）。返回是否确有移除。</summary>
    public static bool Unregister(string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        lock (Entries)
        {
            var removed = Entries.RemoveAll(entry => string.Equals(entry.Owner, owner, StringComparison.Ordinal));
            return removed != 0;
        }
    }

    /// <summary>
    /// 按登记逆序查找模板键（平台资源树由调用方先行查询，因此这里只覆盖“工具新增”的部分）。
    /// </summary>
    public static DataTemplate? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Registration[] snapshot;
        lock (Entries) snapshot = Entries.ToArray();
        for (var index = snapshot.Length - 1; index >= 0; index--)
            if (snapshot[index].Dictionary[key] is DataTemplate template) return template;
        return null;
    }

    /// <summary>该键是否已被某个工具登记（诊断消息用，不参与可见性判断）。</summary>
    public static bool IsRegistered(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Registration[] snapshot;
        lock (Entries) snapshot = Entries.ToArray();
        return snapshot.Any(entry => entry.Dictionary.Contains(key));
    }

    /// <summary>
    /// 按约定路径加载并登记工具程序集自带的模板字典；程序集没有该资源时返回 false（不是错误：
    /// 多数工具不贡献自定义模板）。
    /// </summary>
    public static bool RegisterFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var owner = assembly.GetName().Name ?? assembly.FullName ?? throw new ArgumentException(
            "程序集缺少可用于登记的标识。", nameof(assembly));
        lock (Entries)
            if (Entries.Any(entry => string.Equals(entry.Owner, owner, StringComparison.Ordinal)))
                return true;
        var uri = new Uri("/" + owner + ";component/" + ConventionPath, UriKind.Relative);
        ResourceDictionary dictionary;
        try
        {
            dictionary = (ResourceDictionary)Application.LoadComponent(uri);
        }
        catch (Exception)
        {
            // 资源不存在或格式不可用：视为“该工具不贡献模板”，由调用方的键校验继续兜底。
            return false;
        }
        Register(owner, dictionary);
        return true;
    }

    /// <summary>清空全部登记（测试隔离用）。</summary>
    internal static void ClearAll()
    {
        lock (Entries) Entries.Clear();
    }
}
