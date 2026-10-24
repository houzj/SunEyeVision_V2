function Get-ReleaseIdentity {
    param([Parameter(Mandatory)]$Source, [switch]$Next)
    if ($Source.version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw '产品版本必须为三段数字且无前导零。' }
    if ($Source.channel -notin 'alpha', 'beta', 'rc', 'stable', 'local') { throw '无效发布渠道。' }
    foreach ($field in 'fileRevision', 'schemaVersion') {
        if ($Source.$field -isnot [long] -and $Source.$field -isnot [int]) { throw "$field 必须是整数。" }
        if ($Source.$field -lt 0) { throw "$field 不得为负数。" }
    }
    $revision = [int]$Source.fileRevision
    if ($Next) { $revision++ }
    foreach ($part in @($Source.version.Split('.')) + @($revision)) {
        if ([long]$part -gt 65534) { throw 'Windows 文件版本每段必须在 0..65534。' }
    }
    $date = [datetime]::MinValue
    if (![datetime]::TryParseExact($Source.releaseDate, 'yyyy-MM-dd', [cultureinfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref]$date)) { throw '发布日期无效。' }
    $release = "$($Source.version).$revision"
    $artifact = $release
    $package = $Source.version
    if ($Source.channel -ne 'stable') { $artifact += "_$($Source.channel)"; $package += "-$($Source.channel).$revision" }
    [pscustomobject]@{Version=$Source.version; Channel=$Source.channel; Revision=$revision;
        ReleaseVersion=$release; ArtifactVersion=$artifact; PackageVersion=$package;
        FileVersion=$release; ReleaseDate=$Source.releaseDate; SchemaVersion=$Source.schemaVersion}
}

function Get-InstallerBranding {
    param([ValidateSet('alpha','beta','rc','stable','local')][string]$Channel,
        [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+\.\d+(_(alpha|beta|rc|local))?$')][string]$ArtifactVersion)
    $suffix=if($Channel -eq 'stable'){''}else{"_$Channel"}
    if ($ArtifactVersion -notmatch ('^\d+\.\d+\.\d+\.\d+'+[regex]::Escape($suffix)+'$')) {throw '安装名称的版本与发布渠道不一致。'}
    if ($Channel -eq 'local') {
        return [pscustomobject]@{DisplayName="SunEyeVision $ArtifactVersion";DefaultDir='SunEyeVision Local';
            AppId='{{D25646BF-9B3A-420F-B507-FA34FBB43DF6}';AssociationId='SunEyeVision.Local.SolutionFile'}
    }
    # Keep the identity of the already distributed alpha installer for upgrades.
    [pscustomobject]@{DisplayName="SunEyeVision $ArtifactVersion";DefaultDir='SunEyeVision';
        AppId='{{7A2E9C41-3B58-4C7E-9E31-2F6D8B1A5C04}';AssociationId='SunEyeVision.SolutionFile'}
}

function Invoke-ReleaseNative {
    param([string]$Executable, [string[]]$Arguments, [switch]$Capture)
    if ($Capture) { $result = & $Executable @Arguments; $exitCode=$LASTEXITCODE }
    else { & $Executable @Arguments | Out-Host; $exitCode=$LASTEXITCODE }
    if ($exitCode -ne 0) { throw "$Executable 失败，退出码 $exitCode" }
    if ($Capture) { return ($result -join "`n") }
}

function Assert-ReleaseFileVersionAdvance {
    param([string]$Current, [string]$Previous)
    if ([version]$Current -le [version]$Previous) { throw "Windows 文件版本必须递增：当前 $Current，上次 $Previous。" }
}

function Assert-ReleasePayload {
    param([string]$Path, [string]$FileVersion, [string[]]$Plugins, [string]$IconSourceRoot)
    foreach ($file in 'SunEyeVision.UI.exe', 'SunEyeVision.Core.dll', 'SunEyeVision.Plugin.SDK.dll', 'halcondotnetxl.dll', 'halconxl.dll', 'Icons/suneyevision.ico', 'Icons/solution.ico') {
        if (!(Test-Path (Join-Path $Path $file))) { throw "载荷缺失：$file" }
    }
    if ($IconSourceRoot) {
        foreach ($name in 'suneyevision.ico', 'solution.ico') {
            if ((Get-FileHash (Join-Path $IconSourceRoot $name)).Hash -ne
                (Get-FileHash (Join-Path $Path "Icons/$name")).Hash) {
                throw "发布图标与当前品牌源不一致：$name"
            }
        }
    }
    foreach ($plugin in $Plugins) {
        if (!(Test-Path "$Path/plugins/$plugin/$plugin.dll")) { throw "插件缺失：$plugin" }
    }
    foreach ($file in Get-ChildItem $Path -Recurse -File) {
        if ($file.Name -match '\.Tests\.(dll|exe)$') { throw "测试程序集混入载荷：$($file.Name)" }
        if ($file.Name -like 'SunEyeVision*.dll' -or $file.Name -eq 'SunEyeVision.UI.exe') {
            if ($file.VersionInfo.FileVersion -ne $FileVersion) { throw "载荷版本不一致：$($file.FullName)" }
        }
    }
}

function Get-ReleaseTestSummary {
    param([string]$Path)
    $total=0; $passed=0; $failed=0
    foreach ($file in Get-ChildItem $Path -Filter '*.trx') {
        [xml]$result = Get-Content $file.FullName -Raw
        $counters = $result.TestRun.ResultSummary.Counters
        $total += [int]$counters.total; $passed += [int]$counters.passed; $failed += [int]$counters.failed
    }
    if ($total -eq 0 -or $failed -gt 0 -or $passed -ne $total) { throw '发布测试结果为空、失败或有未通过项。' }
    [ordered]@{total=$total;passed=$passed;failed=$failed}
}

function Get-ReleaseBinaryFileVersion {
    param($VersionInfo)
    @($VersionInfo.FileMajorPart, $VersionInfo.FileMinorPart, $VersionInfo.FileBuildPart, $VersionInfo.FilePrivatePart) -join '.'
}

function Initialize-ReleaseToolMetadataReader {
    if('SunEyeVision.ReleaseToolMetadataReader' -as [type]){return}
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Linq;
namespace SunEyeVision {
    public sealed class ReleasedToolDeclaration {
        public string Id {get;set;}
        public string TypeName {get;set;}
    }
    public static class ReleaseToolMetadataReader {
        private static string TypeName(MetadataReader reader,TypeDefinitionHandle handle) {
            var type=reader.GetTypeDefinition(handle);
            var parent=type.GetDeclaringType();
            return parent.IsNil?(reader.GetString(type.Namespace)+"."+reader.GetString(type.Name)).TrimStart('.')
                :TypeName(reader,parent)+"+"+reader.GetString(type.Name);
        }
        public static ReleasedToolDeclaration[] Read(string path,string approvedSdkIdentity) {
            using(var stream=File.OpenRead(path))using(var pe=new PEReader(stream)) {
                var reader=pe.GetMetadataReader();
                var tools=new List<ReleasedToolDeclaration>();
                foreach(var handle in reader.TypeDefinitions) {
                    var type=reader.GetTypeDefinition(handle);
                    // Derived markers may compute Id in IL; this metadata-only contract
                    // deliberately rejects them rather than silently dropping tools.
                    if(type.BaseType.Kind==HandleKind.TypeReference) {
                        var parent=reader.GetTypeReference((TypeReferenceHandle)type.BaseType);
                        if(reader.GetString(parent.Name)=="ToolAttribute"&&reader.GetString(parent.Namespace)=="SunEyeVision.Plugin.SDK.Core")
                            throw new InvalidDataException("Derived ToolAttribute declarations are unsupported: "+TypeName(reader,handle));
                    }
                    if((type.Attributes&TypeAttributes.Abstract)!=0)continue;
                    foreach(var attributeHandle in type.GetCustomAttributes()) {
                        var attribute=reader.GetCustomAttribute(attributeHandle);
                        EntityHandle owner;BlobHandle signature;
                        if(attribute.Constructor.Kind==HandleKind.MemberReference){var member=reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);owner=member.Parent;signature=member.Signature;}
                        else if(attribute.Constructor.Kind==HandleKind.MethodDefinition){var method=reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);owner=method.GetDeclaringType();signature=method.Signature;}
                        else continue;
                        string name,ns;
                        if(owner.Kind==HandleKind.TypeReference) {
                            var reference=reader.GetTypeReference((TypeReferenceHandle)owner);
                            name=reader.GetString(reference.Name);ns=reader.GetString(reference.Namespace);
                            if(reference.ResolutionScope.Kind!=HandleKind.AssemblyReference)continue;
                            var ar=reader.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope);
                            if(reader.GetString(ar.Name)!="SunEyeVision.Plugin.SDK")continue;
                            var identity=new AssemblyName{ Name=reader.GetString(ar.Name),Version=ar.Version,CultureName=ar.Culture.IsNil?"":reader.GetString(ar.Culture)};
                            var key=reader.GetBlobBytes(ar.PublicKeyOrToken);
                            if((ar.Flags&AssemblyFlags.PublicKey)!=0)identity.SetPublicKey(key);else identity.SetPublicKeyToken(key);
                            if(identity.FullName!=approvedSdkIdentity)throw new InvalidDataException("Tool marker references an unapproved SDK identity.");
                        } else if(owner.Kind==HandleKind.TypeDefinition&&reader.GetString(reader.GetAssemblyDefinition().Name)=="SunEyeVision.Plugin.SDK") {
                            var definition=reader.GetTypeDefinition((TypeDefinitionHandle)owner);
                            name=reader.GetString(definition.Name);ns=reader.GetString(definition.Namespace);
                        } else continue;
                        if(name!="ToolAttribute"||ns!="SunEyeVision.Plugin.SDK.Core")continue;
                        if(!reader.GetBlobBytes(signature).SequenceEqual(new byte[]{0x20,0x02,0x01,0x0e,0x0e}))throw new InvalidDataException("Unsupported Tool attribute constructor.");
                        var blob=reader.GetBlobReader(attribute.Value);
                        if(blob.ReadUInt16()!=1)throw new InvalidDataException("Invalid Tool attribute prolog.");
                        var id=blob.ReadSerializedString();
                        if(blob.ReadSerializedString()==null)throw new InvalidDataException("Tool display name is null.");
                        if(String.IsNullOrWhiteSpace(id))throw new InvalidDataException("A tool declaration has no identity.");
                        var named=blob.ReadUInt16();var seen=new HashSet<string>(StringComparer.Ordinal);
                        for(var index=0;index<named;index++) {
                            if(blob.ReadByte()!=0x54)throw new InvalidDataException("Unsupported Tool attribute field.");
                            var kind=blob.ReadByte();var enumType=kind==0x55?blob.ReadSerializedString():null;
                            var property=blob.ReadSerializedString();
                            if(property==null||!seen.Add(property))throw new InvalidDataException("Duplicate Tool attribute property.");
                            if(new[]{"Description","Icon","Category","Version","Author"}.Contains(property)&&kind==0x0e)blob.ReadSerializedString();
                            else if(property=="Order"&&kind==0x08)blob.ReadInt32();
                            else if(property=="HasDebugWindow"&&kind==0x02){if(blob.ReadByte()>1)throw new InvalidDataException("Invalid Tool Boolean.");}
                            else if(property=="DebugWindowStyle"&&kind==0x55&&enumType=="SunEyeVision.Plugin.SDK.Core.DebugWindowStyle, "+approvedSdkIdentity)blob.ReadInt32();
                            else throw new InvalidDataException("Unsupported Tool attribute property: "+property);
                        }
                        if(blob.RemainingBytes!=0)throw new InvalidDataException("Trailing Tool attribute bytes.");
                        tools.Add(new ReleasedToolDeclaration{Id=id,TypeName=TypeName(reader,handle)});
                    }
                }
                return tools.ToArray();
            }
        }
    }
}
'@
}

function New-ReleasePayload {
    param([Parameter(Mandatory)][string]$AppPath,
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$SourceCommit,
        [Parameter(Mandatory)][string]$ArtifactVersion,[string[]]$ExpectedPluginAssemblies=@(),[string]$FrameworkOriginPath)
    Initialize-ReleaseToolMetadataReader
    $appRoot=(Resolve-Path -LiteralPath $AppPath).Path
    $sdkIdentity=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $appRoot 'SunEyeVision.Plugin.SDK.dll')).FullName
    $entries=@(Get-ChildItem -LiteralPath $appRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
        $relative=[IO.Path]::GetRelativePath($appRoot,$_.FullName).Replace('\','/')
        if($relative -ne 'release-payload.json') {
            $entry=[ordered]@{path=$relative;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
            if($_.Extension -in '.dll','.exe') {
                try {
                    $entry.assemblyIdentity=[Reflection.AssemblyName]::GetAssemblyName($_.FullName).FullName
                    $entry.tools=@([SunEyeVision.ReleaseToolMetadataReader]::Read($_.FullName,$sdkIdentity)|ForEach-Object{[ordered]@{id=$_.Id;typeName=$_.TypeName}})
                }
                catch [BadImageFormatException] { } # Native binaries have hashes, not managed identities.
            }
            $entry
        }
    })
    foreach($name in $ExpectedPluginAssemblies) {
        $main="plugins/$name/$name.dll"
        $declared=@($entries|Where-Object path -eq $main)
        if($declared.Count -ne 1 -or $declared[0].tools.Count -eq 0){throw "Expected plugin has no managed tool declaration: $main"}
    }
    $toolIds=@($entries|Where-Object{$ExpectedPluginAssemblies -contains [IO.Path]::GetFileNameWithoutExtension($_.path) -and $_.path -eq ('plugins/'+[IO.Path]::GetFileNameWithoutExtension($_.path)+'/'+[IO.Path]::GetFileName($_.path))}|ForEach-Object{$_.tools}|ForEach-Object{$_.id})
    if(@($toolIds|Sort-Object -Unique).Count -ne $toolIds.Count){throw 'Release contains duplicate declared ToolIds.'}
    $frameworks=Get-ReleaseRuntimeFrameworks -AppPath $appRoot -Entries $entries -FrameworkOriginPath $FrameworkOriginPath
    [ordered]@{payloadSchemaVersion=1;buildId=$BuildId;sourceCommit=$SourceCommit;artifactVersion=$ArtifactVersion;expectedPluginAssemblies=@($ExpectedPluginAssemblies);runtimeFrameworks=$frameworks;entries=$entries}
}

function Get-ReleaseRuntimeFrameworks {
    param([string]$AppPath,[object[]]$Entries,[string]$FrameworkOriginPath)
    $runtime=Get-Content (Join-Path $AppPath 'SunEyeVision.UI.runtimeconfig.json') -Raw|ConvertFrom-Json -AsHashtable
    $options=$runtime.runtimeOptions
    $selfContained=$options.ContainsKey('includedFrameworks')
    $declared=if($selfContained){@($options.includedFrameworks)}elseif($options.ContainsKey('frameworks')){@($options.frameworks)}elseif($options.ContainsKey('framework')){@($options.framework)}else{throw 'Runtime configuration has no framework declarations.'}
    $frameworks=@($declared|ForEach-Object{[ordered]@{name=$_.name;version=$_.version;managedAssets=@()}})
    if($selfContained) {
        if(!$FrameworkOriginPath -or !(Test-Path -LiteralPath $FrameworkOriginPath)){throw 'Self-contained payload lacks SDK publish provenance.'}
        $lines=@(Get-Content -LiteralPath $FrameworkOriginPath|ForEach-Object{,$_.Split('|')})
        $winners=@{};foreach($line in $lines){if($line[0] -eq 'winner'){$winners[[IO.Path]::GetFullPath($line[1])]=$line[2].Replace('\','/')}}
        foreach($line in $lines) {
            if($line[0] -ne 'asset' -or !$winners.ContainsKey([IO.Path]::GetFullPath($line[1]))){continue}
            $payloadPath=$winners[[IO.Path]::GetFullPath($line[1])]
            $entry=@($Entries|Where-Object path -eq $payloadPath)
            if($entry.Count -ne 1){throw "Framework publish winner missing from final payload: $payloadPath"}
            if(!$entry[0].Contains('assemblyIdentity')){continue}
            $pack=Split-Path -Parent $line[1]
            while($pack -and !(Test-Path -LiteralPath (Join-Path $pack 'data/RuntimeList.xml'))){$pack=Split-Path -Parent $pack}
            if(!$pack){throw "Runtime pack catalog is missing: $($line[1])"}
            [xml]$catalog=Get-Content (Join-Path $pack 'data/RuntimeList.xml') -Raw
            $name=$catalog.FileList.FrameworkName
            $framework=@($frameworks|Where-Object name -eq $name)
            if($framework.Count -ne 1){throw "Runtime pack is not a declared framework: $name"}
            $sourceRelative=[IO.Path]::GetRelativePath($pack,$line[1]).Replace('\','/')
            # Satellite assemblies are managed files catalogued as Resources by
            # the SDK; keep exact source-path provenance for both categories.
            $asset=@($catalog.FileList.File|Where-Object{ $_.Type -in @('Managed','Resources') -and $_.Path.Replace('\','/') -eq $sourceRelative })
            if($asset.Count -ne 1){throw "Framework source is not in RuntimeList: $sourceRelative"}
            if([Reflection.AssemblyName]::GetAssemblyName($line[1]).FullName -ne $entry[0].assemblyIdentity){throw "Framework identity changed after publish: $payloadPath"}
            $framework[0].managedAssets+=@([ordered]@{payloadPath=$payloadPath;runtimePath=$sourceRelative;packId=$line[3];packVersion=$line[4];assemblyIdentity=$entry[0].assemblyIdentity})
        }
        foreach($framework in $frameworks){if($framework.managedAssets.Count -eq 0){throw "Framework has no final managed publish winners: $($framework.name)"}}
    }
    [ordered]@{deploymentMode=$(if($selfContained){'selfContained'}else{'frameworkDependent'});frameworks=$frameworks}
}

function Write-ReleasePayload {
    param([Parameter(Mandatory)][string]$AppPath,[Parameter(Mandatory)][System.Collections.IDictionary]$Payload)
    if($Payload.payloadSchemaVersion -ne 1){throw 'Unsupported payload schema.'}
    $path=Join-Path $AppPath 'release-payload.json'
    [IO.File]::WriteAllText($path,($Payload|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
    (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

function Assert-ReleasePayloadUnchanged {
    param([Parameter(Mandatory)][string]$AppPath,[Parameter(Mandatory)][System.Collections.IDictionary]$Payload,
        [Parameter(Mandatory)][string]$PayloadSha256)
    $root=(Resolve-Path -LiteralPath $AppPath).Path
    if((Get-FileHash -LiteralPath (Join-Path $root 'release-payload.json') -Algorithm SHA256).Hash -ne $PayloadSha256){throw 'Payload manifest changed after sealing.'}
    $expected=@($Payload.entries.path)+@('release-payload.json')
    $actual=@(Get-ChildItem -LiteralPath $root -Recurse -File|ForEach-Object{[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/')})
    if(Compare-Object ($expected|Sort-Object) ($actual|Sort-Object)){throw 'Payload file set changed after sealing.'}
    foreach($entry in $Payload.entries) {
        if((Get-FileHash -LiteralPath (Join-Path $root $entry.path) -Algorithm SHA256).Hash -ne $entry.sha256){throw ('Payload bytes changed after sealing: '+$entry.path)}
    }
}

function Write-ReleaseArtifacts {
    param([string]$OutputPath, [System.Collections.IDictionary]$Manifest)
    $setup = Join-Path $OutputPath $Manifest.installer
    $info = (Get-Item -LiteralPath $setup).VersionInfo
    if ((Get-ReleaseBinaryFileVersion $info) -ne $Manifest.fileVersion -or $info.ProductVersion.Trim() -ne $Manifest.artifactVersion) {
        throw '安装包二进制版本或文本产品版本不匹配。'
    }
    $sha = (Get-FileHash $setup -Algorithm SHA256).Hash
    $Manifest.sha256 = $sha
    "$sha  $($Manifest.installer)" | Set-Content "$setup.sha256" -Encoding utf8
    $Manifest | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputPath "release-manifest-$($Manifest.artifactVersion).json") -Encoding utf8
    $sha
}

function Get-ReleaseSourceSnapshot {
    param([string]$Root)
    $paths = @(Invoke-ReleaseNative git @('-C',$Root,'-c','core.quotePath=false','ls-files','--cached','--others','--exclude-standard','--',
        'src','tools','tests','installer','Directory.Build.props','Directory.Build.targets') -Capture)
    $files = @($paths -split "`n" | Where-Object { $_ -and $_ -notlike '*.md' } | Sort-Object -Unique | ForEach-Object {
        $path = Join-Path $Root $_
        [ordered]@{path=$_;sha256=$(if(Test-Path -LiteralPath $path -PathType Leaf){(Get-FileHash -LiteralPath $path).Hash}else{'MISSING'})}
    })
    $json = ConvertTo-Json -InputObject $files -Depth 4 -Compress
    [ordered]@{sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($json)));files=$files}
}
