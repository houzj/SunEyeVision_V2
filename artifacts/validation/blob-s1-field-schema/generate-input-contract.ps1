$ErrorActionPreference='Stop'
$specPath='D:/MyWork/SunEyeVision_V2/artifacts/analysis/vm-blob-control-architecture-review-20261024/interface-deepening/implementation/blob-field-map-final-v2.json'
if((Get-FileHash -LiteralPath $specPath).Hash -ne '94DA12876AB19360F681378B1FFAB2D2139330EDF1701121CCFBADC752A8ADD3'){throw 'Approved field map changed'}
$map=Get-Content -LiteralPath $specPath -Raw|ConvertFrom-Json
function Q([string]$value){ConvertTo-Json -InputObject $value -Compress}
function Modes($values){if(@($values).Count -eq 0){return 'FieldEditModes.None'};return (@($values)|ForEach-Object {'FieldEditModes.'+$_}) -join '|'}
function Spec([string]$type,[string]$logical){
 switch($type){
 'bool' {return @{type='(TypeRef)DataType.Boolean';editor='Scalar';storage='null';choices=@()}}
 'int' {return @{type='(TypeRef)DataType.Integer';editor='Scalar';storage='NumericStorage.Int32';choices=@()}}
 'double' {return @{type='(TypeRef)DataType.Number';editor='Scalar';storage='NumericStorage.Float64';choices=@()}}
 'Color' {return @{type='PresentationDeclarations.Rgba';editor='Record';storage='null';choices=@()}}
 'LineDashStyle' {return @{type='(TypeRef)DataType.Text';editor='Choice';storage='null';choices=@('Solid','Dash','Dot','DashDot')}}
 'ImageSourceReference?' {if($logical -eq 'input.image'){return @{type='TypeRef.ResourceOf(ResourceKind.Image)';editor='Image';storage='null';choices=@()}};return @{type='SpatialDeclarations.Affine2D';editor='Record';storage='null';choices=@()}}
 default {$enum=$map.enumChoices.PSObject.Properties[$type];if($null -eq $enum){throw ('Unmapped CLR type: '+$type)};return @{type='(TypeRef)DataType.Text';editor='Choice';storage='null';choices=@($enum.Value)}}
 }
}
$entries=[System.Collections.Generic.List[object]]::new()
foreach($field in $map.fields){
 if($field.logicalKey -in 'input.mask','input.inspection','presentation'){continue}
 $entries.Add(@{id=$field.fieldIdCandidate;logical=$field.logicalKey;path=$field.currentClrProperty;type=$field.currentClrType;modes=@($field.modes);read=$field.currentClrProperty;default='null'})
}
foreach($group in $map.regionModeGroups){foreach($field in $group.members){
 $suffix=$field.logicalKey.Split('.')[-1]
 $type=switch($suffix){'mode' {'region-mode'} 'local' {'roi'} 'geometrySource' {'roi'} 'regionSource' {'region'} default {throw 'Unknown region role'}}
 $default=if($suffix -eq 'mode'){'new ConstantSetting(nameof(RegionFieldMode.LocalGeometry))'}else{'null'}
 $entries.Add(@{id=$field.fieldIdCandidate;logical=$field.logicalKey;path=$field.mapping;type=$type;modes=@($field.modes);read=$null;default=$default})
}}
foreach($field in $map.presentationLeaves){
 $read=$field.currentClrPath -replace '\[Key=([^\]]+)\]', '.Single(item=>item.Key=="$1")'
 $entries.Add(@{id=$field.fieldIdCandidate;logical=$field.logicalKey;path=$field.currentClrPath;type=$field.currentClrType;modes=@($field.modes);read=$read;default='null'})
}
if($entries.Count -ne 117 -or @($entries.id|Select-Object -Unique).Count -ne 117){throw 'Concrete input IDs/count mismatch'}
$builder=[System.Text.StringBuilder]::new()
[void]$builder.AppendLine(@'
// Explicit approved IDs generated from blob-field-map-final-v2 SHA94DA1287. No runtime name hashing or reflection.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.BlobAnalysis;

public sealed class BlobInputField
{
    private readonly Func<BlobAnalysisParameters,object?>? _readStoredLiteral;
    private readonly ParameterSetting? _declaredDefault;
    internal BlobInputField(string logicalKey,string legacyPath,Port port,FieldPresentation presentation,
        Func<BlobAnalysisParameters,object?>? readStoredLiteral,ParameterSetting? declaredDefault)
        =>(LogicalKey,LegacyPath,Port,Presentation,_readStoredLiteral,_declaredDefault)=(logicalKey,legacyPath,port,presentation,readStoredLiteral,declaredDefault);
    public string LogicalKey{get;} public string LegacyPath{get;} public Port Port{get;} public FieldPresentation Presentation{get;}
    internal ParameterSetting Default(BlobAnalysisParameters defaults)=>_readStoredLiteral is null
        ?_declaredDefault??new UnsetSetting():new ConstantSetting(ValueRules.Freeze(Port,_readStoredLiteral(defaults)));
    internal void ValidateLiteral(BlobAnalysisParameters settings)
    {if(_readStoredLiteral is not null)ValueRules.Freeze(Port,_readStoredLiteral(settings));}
}
public sealed record BlobInputGroup(string StableId,string LogicalKey,string LegacyProperty,IReadOnlyList<string> PortIds);

/// <summary>Explicit S1 input declarations and legacy literal validation. This is not Runtime producer registration.</summary>
public static class BlobInputContract
{
    private static RecordValue Rgba(Color color)=>PresentationDeclarations.Color(color.R,color.G,color.B,color.A);
    private static readonly BlobInputField[] Entries={
'@)
foreach($entry in $entries){
 $spec=switch($entry.type){
 'roi' {@{type='RoiDeclarations.Sequence';editor='Geometry';storage='null';choices=@()}}
 'region' {@{type='TypeRef.ResourceOf(ResourceKind.Region)';editor='Region';storage='null';choices=@()}}
 'region-mode' {@{type='(TypeRef)DataType.Text';editor='Choice';storage='null';choices=@('LocalGeometry','InheritedGeometry','InheritedRegion')}}
 default {Spec $entry.type $entry.logical}
 }
 $choiceCode=if($spec.choices.Count -gt 0){'new[]{'+(@($spec.choices|ForEach-Object{Q $_}) -join ',')+'}'}else{'Array.Empty<string>()'}
 $constraint=if($spec.choices.Count -gt 0){'new TextChoiceConstraint('+ $choiceCode +')'}else{'null'}
 $required=if($entry.logical -match '^input\.(image|pose|inspection\.(local|geometrySource|regionSource)|mask\.(local|geometrySource|regionSource))$'){'false'}else{'true'}
 $reader=if([string]::IsNullOrEmpty($entry.read)){'null'}elseif($entry.type -eq 'ImageSourceReference?'){'null'}elseif($entry.type -eq 'int'){'p=>(object)(long)p.'+$entry.read}elseif($entry.type -eq 'Color'){'p=>(object)Rgba(p.'+$entry.read+')'}elseif($spec.editor -eq 'Choice'){'p=>(object)p.'+$entry.read+'.ToString()'}else{'p=>(object)p.'+$entry.read}
 $id=Q $entry.id;$logical=Q $entry.logical;$path=Q $entry.path;$label=Q ($entry.logical.Split('.')[-1]);$group=Q $(if($entry.logical.StartsWith('presentation.')){'结果显示'}else{'参数'})
 $mode=Modes $entry.modes
 [void]$builder.AppendLine("        new($logical,$path,new Port($id,$($spec.type),Required:$required,Storage:$($spec.storage),EditModes:$mode,Choices:$constraint),new FieldPresentation($id,$label,$group,$($entries.IndexOf($entry)),FieldEditorKind.$($spec.editor),$choiceCode),$reader,$($entry.default)),")
}
[void]$builder.AppendLine('    };')
[void]$builder.AppendLine('    public static IReadOnlyList<BlobInputField> Fields{get;}=Array.AsReadOnly(Entries);')
[void]$builder.AppendLine('    public static IReadOnlyList<BlobInputGroup> Groups{get;}=Array.AsReadOnly(new BlobInputGroup[]{')
foreach($field in $map.fields){
 $ids=if($field.logicalKey -eq 'presentation'){@($entries|Where-Object {$_.logical.StartsWith('presentation.')}|ForEach-Object{Q $_.id})}elseif($field.logicalKey -in 'input.inspection','input.mask'){@($entries|Where-Object {$_.logical.StartsWith($field.logicalKey+'.')}|ForEach-Object{Q $_.id})}else{@(Q $field.fieldIdCandidate)}
 [void]$builder.AppendLine('        new('+(Q $field.fieldIdCandidate)+','+(Q $field.logicalKey)+','+(Q $field.currentClrProperty)+',Array.AsReadOnly(new[]{'+($ids -join ',')+'})),')
}
[void]$builder.AppendLine('    });')
[void]$builder.AppendLine('    public static IReadOnlyDictionary<string,IReadOnlyList<string>> LegacyStyleAliases{get;}=new ReadOnlyDictionary<string,IReadOnlyList<string>>(new Dictionary<string,IReadOnlyList<string>>{')
foreach($alias in $map.aliases){$prefix=$alias.canonical.Replace('[','[Key=')+'.';$ids=@($entries|Where-Object {$_.path.StartsWith($prefix)}|ForEach-Object {Q $_.id});if($ids.Count -eq 0){throw 'Missing explicit style alias members'};[void]$builder.AppendLine('        ['+(Q $alias.property)+']=Array.AsReadOnly(new[]{'+($ids -join ',')+'}),')}
[void]$builder.AppendLine('    });')
[void]$builder.AppendLine('    private static readonly RegionFieldGroup[] Regions={')
foreach($group in $map.regionModeGroups){$parts=@($group.members|ForEach-Object{Q $_.fieldIdCandidate});[void]$builder.AppendLine('        new('+(Q $group.role)+','+($parts -join ',')+'),')}
[void]$builder.AppendLine(@'
    };
    public static ToolSchema Describe()=>new(Entries.Select(field=>field.Port),BlobOutputContract.Describe().Select(SunEyeVision.Plugin.SDK.Execution.Parameters.BindingClrAdapter.DescribeOutput),
        new ToolPresentation(Entries.Select(field=>field.Presentation),false,Regions));
    public static IReadOnlyDictionary<string,ParameterSetting> CreateDefaults()
    {
        var defaults=new BlobAnalysisParameters();
        return new ReadOnlyDictionary<string,ParameterSetting>(Entries.ToDictionary(field=>field.Port.Id,field=>field.Default(defaults),StringComparer.Ordinal));
    }
    public static IReadOnlyList<Diagnostic> ValidateStoredConfiguration(BlobAnalysisParameters settings,IReadOnlyCollection<string>? boundParameterNames=null)
    {
        var issues=new List<Diagnostic>();
        if(settings.ResultPresentation is null)return Array.AsReadOnly(new[]{new Diagnostic("stored-setting","ResultPresentation must be present.")});
        void ValidateKeys(string collection,IEnumerable<string>? supplied,string[] expected)
        {
            var keys=supplied?.ToArray();
            if(keys is null||keys.Length!=expected.Length||keys.Distinct(StringComparer.Ordinal).Count()!=keys.Length
                ||!keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected))
                issues.Add(new("stored-setting",collection+" has missing, duplicate or unknown entries; an explicit schema migration is required."));
        }
        ValidateKeys("ResultPresentation.Judgments",settings.ResultPresentation.Judgments?.Select(item=>item?.Key??""),new[]{__JUDGMENTS__});
        ValidateKeys("ResultPresentation.Graphics",settings.ResultPresentation.Graphics?.Select(item=>item?.Key??""),new[]{__GRAPHICS__});
        ValidateKeys("ResultPresentation.Texts",settings.ResultPresentation.Texts?.Select(item=>item?.Key??""),new[]{__TEXTS__});
        foreach(var field in Entries)
        {
            // Reference expressions are validated by the workflow owner. Do not resolve runtime sources during persistence.
            if(boundParameterNames?.Contains(field.LegacyPath,StringComparer.Ordinal)==true)continue;
            try{field.ValidateLiteral(settings);}
            catch(Exception error) when(error is ArgumentException or InvalidOperationException or NullReferenceException)
            {issues.Add(new("stored-setting",field.LegacyPath+": "+error.Message,field.Port.Id));}
        }
        return issues.AsReadOnly();
    }
}
'@)
$outputPath=Join-Path (Get-Location) 'tools/SunEyeVision.BlobAnalysis/BlobInputContract.cs'
$emission=$builder.ToString()
foreach($group in @(@{name='judgments';token='__JUDGMENTS__'},@{name='graphics';token='__GRAPHICS__'},@{name='texts';token='__TEXTS__'})){
 $keys=@($map.presentationLeaves|Where-Object {$_.logicalKey.StartsWith('presentation.'+$group.name+'.')}|ForEach-Object {$_.logicalKey.Split('.')[2]}|Select-Object -Unique|ForEach-Object {Q $_})
 $emission=$emission.Replace($group.token,($keys -join ','))
}
[System.IO.File]::WriteAllText($outputPath,$emission,[System.Text.UTF8Encoding]::new($false))
[ordered]@{sourceSpecSha=(Get-FileHash -LiteralPath $specPath).Hash;concreteInputs=$entries.Count;topGroups=@($map.fields).Count;presentationLeaves=@($map.presentationLeaves).Count;outputPath=$outputPath;outputSha=(Get-FileHash -LiteralPath $outputPath).Hash}|ConvertTo-Json|Set-Content artifacts/validation/blob-s1-field-schema/generated-source-receipt.json
