using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Xml.Linq;

var root=args.Length>0?Path.GetFullPath(args[0]):@"D:\Program Files\VisionMaster4.4.0";
var outPath=args.Length>1?Path.GetFullPath(args[1]):"types-static.json";
string[] roots={"Applications","Development","Drivers","MVDAlgorithmSDK"};
var files=roots.SelectMany(r=>Directory.Exists(Path.Combine(root,r))?Directory.EnumerateFiles(Path.Combine(root,r),"*",SearchOption.AllDirectories):Array.Empty<string>()).ToArray();
var rows=new List<AssemblyRow>(); var allTypes=new List<TypeData>(); var typeRefs=new List<TypeRefRow>(); var errors=new List<string>();
foreach(var path in files.Where(p=>p.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))) {
  var rel=Path.GetRelativePath(root,path).Replace('\\','/');
  try {
    using var fs=File.OpenRead(path); using var pe=new PEReader(fs);
    if(!pe.HasMetadata) { rows.Add(new(rel,"","","",0,"native-or-not-cli",null)); continue; }
    var md=pe.GetMetadataReader(); if(!md.IsAssembly) { rows.Add(new(rel,"","", "",0,"module-not-assembly",null)); continue; }
    var ad=md.GetAssemblyDefinition(); var name=md.GetString(ad.Name); var mvid=md.GetGuid(md.GetModuleDefinition().Mvid).ToString();
    foreach(var rh in md.TypeReferences){var tr=md.GetTypeReference(rh);var ns=md.GetString(tr.Namespace);var tn=md.GetString(tr.Name);var full=string.IsNullOrEmpty(ns)?tn:ns+"."+tn;var targetAssembly=tr.ResolutionScope.Kind==HandleKind.AssemblyReference?md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope).Name):name;typeRefs.Add(new TypeRefRow(rel,name,targetAssembly,full));}
    var local=new List<TypeData>();
    foreach(var h in md.TypeDefinitions) {
      var td=md.GetTypeDefinition(h); var ns=md.GetString(td.Namespace); var n=md.GetString(td.Name); if(n=="<Module>") continue;
      var full=string.IsNullOrEmpty(ns)?n:ns+"."+n; string? baseAsm=null; string? baseName=null;
      var bh=td.BaseType;
      if(!bh.IsNil) {
        if(bh.Kind==HandleKind.TypeDefinition) {var bd=md.GetTypeDefinition((TypeDefinitionHandle)bh); var bns=md.GetString(bd.Namespace); var bn=md.GetString(bd.Name); baseName=string.IsNullOrEmpty(bns)?bn:bns+"."+bn; baseAsm=name;}
        else if(bh.Kind==HandleKind.TypeReference) {var tr=md.GetTypeReference((TypeReferenceHandle)bh); var tns=md.GetString(tr.Namespace); var tn=md.GetString(tr.Name); baseName=string.IsNullOrEmpty(tns)?tn:tns+"."+tn; if(tr.ResolutionScope.Kind==HandleKind.AssemblyReference) baseAsm=md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope).Name); else baseAsm=name;}
      }
      var isInterface=(td.Attributes & TypeAttributes.Interface)!=0; var isAbstract=(td.Attributes & TypeAttributes.Abstract)!=0; var vis=(td.Attributes & TypeAttributes.VisibilityMask); var isPublic=vis==TypeAttributes.Public||vis==TypeAttributes.NestedPublic;
      local.Add(new TypeData(rel,name,mvid,full,baseAsm,baseName,isPublic,isAbstract,isInterface));
    }
    allTypes.AddRange(local); rows.Add(new(rel,name,ad.Version.ToString(),mvid,local.Count,"complete",null));
  } catch(Exception ex) {rows.Add(new(rel,"","","",0,"metadata-error",ex.Message));errors.Add(rel+": "+ex.Message);}
}
var map=allTypes.GroupBy(t=>Key(t.Assembly,t.Name),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
var builtins=new HashSet<string>(StringComparer.Ordinal){"System.Windows.Window","System.Windows.Controls.Page","System.Windows.Controls.Control","System.Windows.Controls.UserControl","System.Windows.FrameworkElement","System.Windows.UIElement","System.Windows.Controls.DataTemplateSelector","System.Windows.Controls.StyleSelector","System.MulticastDelegate","System.Delegate"};
string[] Chain(TypeData t){var result=new List<string>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var asm=t.BaseAssembly;var bn=t.BaseName;for(int i=0;i<32&&bn!=null;i++){result.Add(bn);if(builtins.Contains(bn)||!seen.Add(Key(asm??"",bn)))break;if(asm!=null&&map.TryGetValue(Key(asm,bn),out var next)){asm=next.BaseAssembly;bn=next.BaseName;}else break;}return result.ToArray();}
var typeRows=allTypes.Select(t=>{var chain=Chain(t);var cat="other-type";if(t.Interface)cat="interface";else if(chain.Contains("System.Windows.Window"))cat="window";else if(chain.Contains("System.Windows.Controls.Page"))cat="page";else if(chain.Contains("System.Windows.Controls.DataTemplateSelector")||chain.Contains("System.Windows.Controls.StyleSelector"))cat="template-selector";else if(chain.Contains("System.Windows.Controls.Control")||chain.Contains("System.Windows.Controls.UserControl"))cat="control";else if(chain.Contains("System.Windows.FrameworkElement")||chain.Contains("System.Windows.UIElement"))cat="visual-element";else if(Regex.IsMatch(t.Name,"(Data|Model|ViewModel|ViewState|Presenter|Command|Context|Provider|Session|Controller)$"))cat="model-candidate";var id=Hash(Encoding.UTF8.GetBytes(t.Path+"|"+t.Assembly+"|"+t.Mvid+"|"+t.Name));return new TypeRow("type:"+id,t.Name,t.Assembly,t.Path,t.Mvid,t.Public,t.Abstract,t.Interface,t.BaseName,chain,cat,"PE CLI metadata (System.Reflection.Metadata; no assembly loading/execution); MVID "+t.Mvid);}).ToArray();
var totalBytes=files.Sum(p=>new FileInfo(p).Length);
var fileRows=files.Select(p=>{var f=new FileInfo(p);return new FileRow(Path.GetRelativePath(root,p).Replace('\\','/'),f.Length,f.LastWriteTimeUtc.ToString("o"),f.Extension.ToLowerInvariant());}).ToArray();
var rootRows=roots.Select(r=>{var fs=files.Where(p=>Path.GetRelativePath(root,p).StartsWith(r+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)).ToArray();return new RootRow(r,fs.Length,fs.Sum(p=>new FileInfo(p).Length));}).ToArray();
var xamlTemplates=new List<TemplateRow>();
foreach(var path in files.Where(p=>p.EndsWith(".xaml",StringComparison.OrdinalIgnoreCase))){var rel=Path.GetRelativePath(root,path).Replace('\\','/');try{var doc=System.Xml.Linq.XDocument.Load(path);foreach(var node in doc.Descendants().Where(x=>new[]{"DataTemplate","HierarchicalDataTemplate","ControlTemplate","ItemsPanelTemplate","Style"}.Contains(x.Name.LocalName))){var kind=node.Name.LocalName=="HierarchicalDataTemplate"?"DataTemplate":node.Name.LocalName;var key=(string?)node.Attribute(XName.Get("Key","http://schemas.microsoft.com/winfx/2006/xaml"));var target=(string?)node.Attribute("TargetType");var data=(string?)node.Attribute("DataType");var hash=Hash(File.ReadAllBytes(path));var id=Hash(Encoding.UTF8.GetBytes(rel+"|"+kind+"|"+key+"|"+target+"|"+data+"|"+hash));xamlTemplates.Add(new TemplateRow("template:"+id,kind,key??"(implicit/unknown)",target,data,rel,hash,"XML XAML resource; parsed without loading WPF assemblies"));}}catch(Exception ex){errors.Add(rel+": XAML parse: "+ex.Message);}}
var output=new Output(root,roots,files.Length,totalBytes,rootRows,rows.ToArray(),typeRows,typeRefs.Distinct().ToArray(),xamlTemplates.ToArray(),fileRows,errors.ToArray());
File.WriteAllText(outPath,JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=false}),new System.Text.UTF8Encoding(false));
Console.WriteLine(JsonSerializer.Serialize(new{files=files.Length,bytes=totalBytes,pe=rows.Count,managed=rows.Count(x=>x.Status=="complete"),types=typeRows.Length,controls=typeRows.Count(x=>x.Category=="control"),windows=typeRows.Count(x=>x.Category is "window" or "page"),visual=typeRows.Count(x=>x.Category=="visual-element"),templates=typeRows.Count(x=>x.Category=="template-selector"),errors=errors.Count}));
static string Key(string? asm,string name)=>(asm??"")+"|"+name;
static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
class TypeData(string path,string assembly,string mvid,string name,string? baseAssembly,string? baseName,bool isPublic,bool isAbstract,bool isInterface){public string Path=path,Assembly=assembly,Mvid=mvid,Name=name;public string? BaseAssembly=baseAssembly,BaseName=baseName;public bool Public=isPublic,Abstract=isAbstract,Interface=isInterface;}


record AssemblyRow(string Path,string Name,string Version,string Mvid,int TypeCount,string Status,string? Error);
record TypeRow(string Id,string Name,string Assembly,string AssemblyPath,string Mvid,bool Public,bool Abstract,bool Interface,string? DirectBase,string[] BaseChain,string Category,string Evidence);
record RootRow(string Path,int Files,long Bytes);
record FileRow(string Path,long Bytes,string LastWriteUtc,string Extension);
record TypeRefRow(string SourcePath,string SourceAssembly,string TargetAssembly,string TargetType);
record TemplateRow(string Id,string Kind,string Key,string? TargetType,string? DataType,string SourcePath,string SourceSha256,string Evidence);
record Output(string InstallRoot,string[] Roots,int TotalFiles,long TotalBytes,RootRow[] RootSummary,AssemblyRow[] Assemblies,TypeRow[] Types,TypeRefRow[] TypeRefs,TemplateRow[] XamlTemplates,FileRow[] Files,string[] Errors);
