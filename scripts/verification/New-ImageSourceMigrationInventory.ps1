[CmdletBinding()]
param([Parameter(Mandatory)][string]$OriginalRoot,[Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$original=[IO.Path]::GetFullPath($OriginalRoot)
$relative=@('VMControls.WPF/LocalImageListView.xaml','VMControls.WPF/LocalImageListView.xaml.cs','VMControls.WPF/ImageListPanel.xaml','VMControls.WPF/ImageListPanel.xaml.cs','VMControls.WPF/ImageListPanelBase.cs')
$files=@($relative|ForEach-Object { $path=Join-Path $original $_;if(!(Test-Path -LiteralPath $path)){throw "Missing original: $path"};[ordered]@{relative=$_;path=$path;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash} })
$code=($files|Where-Object {$_.path.EndsWith('.cs')}|ForEach-Object {[IO.File]::ReadAllText($_.path)}) -join "`n"
Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp
$members=@($files|Where-Object {$_.path.EndsWith('.cs')}|ForEach-Object {
    $file=$_;$text=[IO.File]::ReadAllText($file.path)
    $tree=[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($text)
    $syntaxErrors=@($tree.GetDiagnostics()|Where-Object {$_.Severity.ToString() -eq 'Error'})
    if($syntaxErrors.Count){throw ('Cannot inventory malformed original '+$file.relative+': '+($syntaxErrors -join '; '))}
    foreach($node in $tree.GetRoot().DescendantNodes()) {
        $kind=$node.GetType().Name
        $names=switch($kind) {
            {$_ -in @('MethodDeclarationSyntax','ConstructorDeclarationSyntax','DestructorDeclarationSyntax','PropertyDeclarationSyntax','EventDeclarationSyntax')} {@($node.Identifier.ValueText);break}
            {$_ -in @('FieldDeclarationSyntax','EventFieldDeclarationSyntax')} {@($node.Declaration.Variables|ForEach-Object {$_.Identifier.ValueText});break}
            'IndexerDeclarationSyntax' {@('this[]');break}
            'OperatorDeclarationSyntax' {@('operator '+$node.OperatorToken.ValueText);break}
            'ConversionOperatorDeclarationSyntax' {@('operator '+$node.Type.ToString());break}
            default {@()}
        }
        if(!$names.Count){continue}
        $types=@($node.Ancestors()|Where-Object {$_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax]}|ForEach-Object {$_.Identifier.ValueText});[array]::Reverse($types)
        $range=$node.GetLocation().GetLineSpan()
        $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($node.ToString())))
        $calls=@($node.DescendantNodes()|Where-Object {$_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax]}|ForEach-Object {$_.Expression.ToString()}|Sort-Object -Unique -CaseSensitive)
        $subscriptions=@($node.DescendantNodes()|Where-Object {$_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.AssignmentExpressionSyntax] -and $_.OperatorToken.ValueText -in @('+=','-=')}|ForEach-Object {
            [ordered]@{eventExpression=$_.Left.ToString();operation=$_.OperatorToken.ValueText;handlerKind=$_.Right.GetType().Name;handler=if($_.Right -is [Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax]){$_.Right.ToString()}else{$null}}
        })
        foreach($name in $names) {[ordered]@{file=$file.relative;type=($types -join '.');kind=$kind;line=$range.StartLinePosition.Line+1;endLine=$range.EndLinePosition.Line+1;member=$name;syntaxSha256=$hash;invocationExpressions=$calls;eventSubscriptions=$subscriptions;review='unreviewed'}}
    }
})
$methods=@($members|Where-Object {$_.kind -eq 'MethodDeclarationSyntax'})
$methodNames=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach($method in $methods){[void]$methodNames.Add($method.member)}
function Read-XamlInventory([string]$path,[string]$identity) {
    $xml=[System.Xml.XmlDocument]::new();$xml.XmlResolver=$null;$xml.Load($path)
    $names=[Collections.Generic.List[object]]::new();$events=[Collections.Generic.List[object]]::new();$bindings=[Collections.Generic.List[object]]::new();$resources=[Collections.Generic.List[object]]::new()
    foreach($element in $xml.SelectNodes('//*')) {
        foreach($attribute in $element.Attributes) {
            $value=$attribute.Value
            if($attribute.LocalName -eq 'Name') {$names.Add([ordered]@{element=$element.LocalName;name=$value})}
            if($attribute.NamespaceURI -eq '' -and $value -match '^\w+$' -and $attribute.LocalName -ne 'Name' -and $methodNames.Contains($value)) {
                $events.Add([ordered]@{element=$element.LocalName;event=$attribute.Name;handler=$value;review='unreviewed'})
            }
            if($value -match '\{Binding') {$bindings.Add([ordered]@{element=$element.LocalName;property=$attribute.Name;expression=$value;review='unreviewed'})}
            foreach($match in [regex]::Matches($value,'\{(StaticResource|DynamicResource)\s+([^}]+)\}')) {
                $resources.Add([ordered]@{kind=$match.Groups[1].Value;reference=$match.Groups[2].Value;review='unreviewed'})
            }
            if($element.LocalName -eq 'ResourceDictionary' -and $attribute.LocalName -eq 'Source') {
                $resources.Add([ordered]@{kind='MergedDictionary';reference=$value;review='unreviewed'})
            }
        }
    }
    [ordered]@{file=$identity;names=@($names);eventCandidates=@($events);bindings=@($bindings);resourceReferences=@($resources)}
}
$originalXaml=@($files|Where-Object {$_.path.EndsWith('.xaml')}|ForEach-Object {Read-XamlInventory $_.path $_.relative})
$productPath=Join-Path $repo 'src/Plugin.Wpf/Controls/Native/Composition/ImageSources/ImageSourceStrip.xaml'
$product=Read-XamlInventory $productPath 'ImageSourceStrip.xaml'
$knownNames=@($product.names|ForEach-Object {$_.name})
$aliases=@{UserControl='ImagePanel';cbx='Mode';list='lst'}
$obligations=@($originalXaml|ForEach-Object {
    $file=$_.file
    foreach($name in $_.names) {
        $candidate=if($aliases.ContainsKey($name.name)){$aliases[$name.name]}else{$name.name}
        [ordered]@{originalFile=$file;originalElement=$name.element;originalName=$name.name;candidateName=$candidate;candidateNamePresent=$knownNames -contains $candidate;decision='unreviewed';note='Name presence alone does not prove type, behavior or completion.'}
    }
})
$result=[ordered]@{schemaVersion=2;parserAssembly=[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree].Assembly.FullName;scope='Static source inventory only. Not complete IL/BAML/resource closure or behavioral acceptance.';originalFiles=$files;originalXaml=$originalXaml;originalMethodCandidates=$methods;originalDeclaredMembers=$members;productXamlSha256=(Get-FileHash $productPath -Algorithm SHA256).Hash;namedElementObligations=$obligations;residual='Every event, method, binding, resource, ancestor/runtime edge and alias requires explicit evidence review; no completion inferred.'}
$output=[IO.Path]::GetFullPath($OutputPath,$repo);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output))|Out-Null
$result|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $output -Encoding utf8
[ordered]@{output=$output;originalFiles=$files.Count;methodCandidates=$methods.Count;declaredMembers=$members.Count;namedObligations=$obligations.Count;missingCandidateNames=@($obligations|Where-Object {!$_.candidateNamePresent}|ForEach-Object {$_.originalFile+':'+$_.originalName})}|ConvertTo-Json -Depth 4