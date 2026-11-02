// Original SDK observation helper only. Not product code and never starts the SDK.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

internal static class B08SourceQualificationCapture
{
    static object Property(object value,string name) { return value.GetType().GetProperty(name).GetValue(value,null); }
    static object Field(object value,string name) { return value.GetType().GetField(name).GetValue(value); }
    static int Count(object value,string name) { int count=Convert.ToInt32(Field(value,name));if(count<0||count>8192)throw new InvalidOperationException("Invalid native count: "+name);return count; }
    static string Text(object value,string name)
    {
        var bytes=(byte[])Field(value,name);int length=Array.IndexOf(bytes,(byte)0);
        if(length<0)length=bytes.Length;return new UTF8Encoding(false,true).GetString(bytes,0,length);
    }
    static object Manager()
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("VM.PlatformSDKCS.ServerSDKManager",false)).First(t=>t!=null);
        return Field(type.GetProperty("serverSDKManager").GetValue(null,null),"mModuleManager");
    }
    static object[] NativeItems(IntPtr pointer,int count,Type type,int depth)
    {
        if(depth>16||count<0||count>8192||(count>0&&pointer==IntPtr.Zero))throw new InvalidOperationException("Invalid native output tree");
        int size=Marshal.SizeOf(type);var result=new List<object>();
        for(int index=0;index<count;index++)
        {
            object item=Marshal.PtrToStructure(IntPtr.Add(pointer,checked(index*size)),type);
            int kind=Convert.ToInt32(Field(item,"nDataType"));int children=Count(item,"nNum");
            result.Add(new {name=Text(item,"szParamName"),type=Text(item,"szParamType"),extra=Text(item,"szExtraData"),nDataType=kind,nNum=children,
                children=kind==2?NativeItems((IntPtr)Field(item,"pstCombInfo"),children,type,depth+1):new object[0]});
        }
        return result.ToArray();
    }
    static object RawOutputs(object manager,object module)
    {
        object list=manager.GetType().GetMethod("GetModuOutputIOInfo").Invoke(manager,new[]{Property(module,"ID")});
        if(list==null)throw new InvalidOperationException("SDK returned null metadata, not an empty successful catalogue");
        try
        {
            var itemType=list.GetType().DeclaringType.GetNestedType("IMVS_MODULE_IO_INFO");
            int count=Count(list,"nNum");
            return new {nIoClass=Field(list,"nIoClass"),nNum=count,items=NativeItems((IntPtr)Field(list,"pstModuIoInfo"),count,itemType,0)};
        }
        finally { GC.KeepAlive(list); } // Original SDK wrapper owns/finalizes the native allocations.
    }
    static object ManagedItem(object item,string parent,int depth)
    {
        if(depth>16)throw new InvalidOperationException("Managed output tree exceeds observation bound");
        var name=Convert.ToString(Property(item,"Name"));var path=parent.Length==0?name:parent+"/"+name;
        var children=((IEnumerable)Property(item,"Children")).Cast<object>().ToArray();
        if(children.Length>8192)throw new InvalidOperationException("Managed children exceed observation bound");
        return new {path,type=Property(item,"TypeName"),leaf=Property(item,"IsLeafNode"),
            canSubscribe=Property(item,"CanSubscribeA"),visible=Property(item,"Visible"),isPrivate=Property(item,"IsPrivate"),
            customVisible=Property(item,"CustomVisibleA"),resultVisible=Property(item,"VisibleInResultTree"),
            children=children.Select(child=>ManagedItem(child,path,depth+1)).ToArray()};
    }
    static object Connections(object manager,object[] modules)
    {
        var method=manager.GetType().GetMethod("GetMultiModuleConnect");var type=method.GetParameters()[0].ParameterType;
        var query=Activator.CreateInstance(type);var ids=new uint[2064];
        for(int index=0;index<modules.Length;index++)ids[index]=Convert.ToUInt32(Property(modules[index],"ID"));
        type.GetField("nNum").SetValue(query,modules.Length);type.GetField("anModuleId").SetValue(query,ids);
        type.GetField("nReserved").SetValue(query,new uint[4]);
        var returned=method.Invoke(manager,new[]{query});int count=Count(returned,"nNum");
        var rows=(Array)Field(returned,"pstModuleConnectInfo");
        if(rows==null&&count!=0||rows!=null&&rows.Length<count)throw new InvalidOperationException("Incomplete connection response");
        return rows==null?new object[0]:rows.Cast<object>().Take(count).Select(row=>new {
            id=Field(row,"nModuleId"),next=((uint[])Field(row,"anNextModuleId")).Take(Count(row,"nNextModuNum")).ToArray(),
            previous=((uint[])Field(row,"anPreModuleId")).Take(Count(row,"nPreModuNum")).ToArray()}).Cast<object>().ToArray();
    }
    static bool Allow<T>(T value) { return true; }
    static object TreeItem(object item,int depth)
    {
        if(depth>16)throw new InvalidOperationException("Original source tree exceeds observation bound");
        var type=item.GetType();var entries=type.GetProperty("EnumEntrysA");
        if(entries!=null)
        {
            var children=((IEnumerable)entries.GetValue(item,null)).Cast<object>().ToArray();
            if(children.Length>8192)throw new InvalidOperationException("Original source tree has too many children");
            var target=type.GetProperty("HolderTarget").GetValue(item,null);
            var label=type.GetProperty("DisplayNameA");
            return new {kind=type.FullName,label=label==null?null:label.GetValue(item,null),targetType=target==null?null:target.GetType().FullName,
                targetId=target==null||target.GetType().GetProperty("ID")==null?null:Property(target,"ID"),
                children=children.Select(child=>TreeItem(child,depth+1)).ToArray()};
        }
        return new {kind=type.FullName,name=Property(item,"Name"),type=Property(item,"TypeName"),
            rootId=Property(Property(item,"Root"),"ID"),canSubscribe=Property(item,"CanSubscribeA"),visible=Property(item,"Visible"),isPrivate=Property(item,"IsPrivate")};
    }
    static object SourceTrees(object target)
    {
        var ownerType=target.GetType().Assembly.GetType("VM.Core.VmModule",true);
        var extension=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Apps.XmlParser.ParamTab.VmModuleExtern",false)).First(t=>t!=null);
        var method=extension.GetMethods().Single(m=>m.Name=="GetFrontFilterTree"&&m.GetParameters().Length==11&&m.GetParameters()[0].ParameterType==ownerType);
        var predicate=Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(ownerType,typeof(bool)),
            typeof(B08SourceQualificationCapture).GetMethod("Allow",BindingFlags.NonPublic|BindingFlags.Static).MakeGenericMethod(ownerType));
        return new[]{"int","float"}.Select(kind=>new {type=kind,
            roots=((IEnumerable)method.Invoke(null,new object[]{target,predicate,"Output",new[]{kind},false,false,true,true,"",true,null}))
                .Cast<object>().Select(item=>TreeItem(item,0)).ToArray()}).ToArray();
    }
    internal static object Capture(string phase,params object[] modules)
    {
        if(modules.Length==0||modules.Length>2064)throw new ArgumentException("Explicit owned modules are required");
        var manager=Manager();
        return new {phase,utc=DateTime.UtcNow.ToString("o"),connections=Connections(manager,modules),ordinaryNumericSourceTrees=SourceTrees(modules.Last()),
            modules=modules.Select(module=>new {id=Property(module,"ID"),name=Property(module,"FullName"),raw=RawOutputs(manager,module),
                managed=((IEnumerable)Property(module,"Outputs")).Cast<object>().Select(item=>ManagedItem(item,"",0)).ToArray(),
                previous=((IEnumerable)module.GetType().GetMethod("GetPreModules").Invoke(module,null)).Cast<object>().Select(item=>Property(item,"ID")).ToArray()}).ToArray()};
    }
}
