[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Add-Type -AssemblyName Microsoft.CodeAnalysis
Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp
$files = @{}; $types = @{}; $fullTypes = @{}; $views = @{}
foreach ($relative in (rg --files (Join-Path $root 'src/Plugin.Wpf') -g '*.cs' -g '*.xaml')) {
    $path = [IO.Path]::GetRelativePath($root, $relative).Replace('\', '/')
    $raw = [IO.File]::ReadAllText($relative)
    $namespace = ''; $declared = @()
    if ($path.EndsWith('.cs')) {
        $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($raw)
        foreach ($node in $tree.GetRoot().DescendantNodes()) {
            $extension = $node -is [Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax] -and
                $node.ParameterList.Parameters.Count -gt 0 -and
                @($node.ParameterList.Parameters[0].Modifiers | Where-Object Text -eq 'this').Count -gt 0
            if ($node -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax] -and
                $node -isnot [Microsoft.CodeAnalysis.CSharp.Syntax.DelegateDeclarationSyntax] -and
                -not $extension) { continue }
            $parts = [Collections.Generic.List[string]]::new()
            $owners = [Collections.Generic.List[string]]::new()
            for ($parent = $node.Parent; $null -ne $parent; $parent = $parent.Parent) {
                if ($parent -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax]) {
                    $parts.Insert(0, $parent.Name.ToString())
                } elseif ($parent -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax]) {
                    $owners.Insert(0, $parent.Identifier.ValueText)
                }
            }
            $ns = $parts -join '.'; $namespace = $ns
            $name = $node.Identifier.ValueText
            $fq = (@($ns) + @($owners) + @($name)) -join '.'
            $entry = @{ name = $name; namespace = $ns; fullName = $fq; file = $path }
            if (-not $types.ContainsKey($name)) { $types[$name] = [Collections.Generic.List[object]]::new() }
            if ($extension) { $types[$name].Add($entry); continue }
            if (-not $fullTypes.ContainsKey($fq)) { $fullTypes[$fq] = [Collections.Generic.List[string]]::new() }
            $types[$name].Add($entry); $fullTypes[$fq].Add($path); $declared += $fq
        }
    } else {
        $match = [regex]::Match($raw, 'x:Class\s*=\s*"([^"]+)"')
        if ($match.Success) {
            $fq = $match.Groups[1].Value
            if (-not $views.ContainsKey($fq)) { $views[$fq] = [Collections.Generic.List[string]]::new() }
            $views[$fq].Add($path)
        }
    }
    $files[$path] = @{ text = $raw; namespace = $namespace; declared = $declared }
}
$roots = @(
    'src/Plugin.Wpf/Presenters/References/NativeReferencePresenter.cs',
    'src/Plugin.Wpf/Presenters/References/ReferenceExpansionCoordinator.cs',
    'src/Plugin.Wpf/Adapters/References/ReferenceFieldSession.cs',
    'src/Plugin.Wpf/Controls/References/Variables/VariableSelectionDialog.xaml',
    'src/Plugin.Wpf/Themes/Native/Composition/References/ReferenceField.xaml'
)
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$queue = [Collections.Generic.Queue[string]]::new()
$edges = [Collections.Generic.List[object]]::new()
$unresolved = [Collections.Generic.List[object]]::new()
function Add-Dependency([string]$from, [string]$to, [string]$reason) {
    if (-not $files.ContainsKey($to)) { return }
    $edges.Add(@{ from = $from; to = $to; reason = $reason })
    if ($seen.Add($to)) { $queue.Enqueue($to) }
}
foreach ($path in $roots) {
    if (-not $files.ContainsKey($path)) { throw "根文件不存在：$path" }
    if ($seen.Add($path)) { $queue.Enqueue($path) }
}
while ($queue.Count) {
    $path = $queue.Dequeue(); $item = $files[$path]; $raw = $item.text
    if ($path.EndsWith('.cs')) {
        $imports = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        [void]$imports.Add($item.namespace)
        foreach ($m in [regex]::Matches($raw, '(?m)^\s*using\s+(?:[A-Za-z_]\w*\s*=\s*)?([A-Za-z_][A-Za-z0-9_.]*)\s*;')) {
            [void]$imports.Add($m.Groups[1].Value)
        }
        $tokens = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($m in [regex]::Matches($raw, '\b[A-Za-z_]\w*\b')) { [void]$tokens.Add($m.Value) }
        foreach ($token in $tokens) {
            if (-not $types.ContainsKey($token)) { continue }
            foreach ($type in $types[$token]) {
                if ($imports.Contains($type.namespace) -or $raw.Contains($type.fullName)) {
                    Add-Dependency $path $type.file '语法类型索引+保守文本使用；不是语义符号绑定'
                }
            }
        }
        foreach ($fq in $item.declared) {
            if ($views.ContainsKey($fq)) {
                foreach ($view in $views[$fq]) { Add-Dependency $path $view '按x:Class匹配异目录XAML' }
            }
        }
        if ($path.EndsWith('.xaml.cs')) { Add-Dependency $path $path.Substring(0, $path.Length - 3) '同目录XAML' }
    } else {
        $aliases = @{}
        foreach ($m in [regex]::Matches($raw, 'xmlns:([A-Za-z_][A-Za-z0-9_]*)="clr-namespace:([^";]+)')) {
            $aliases[$m.Groups[1].Value] = $m.Groups[2].Value
        }
        foreach ($m in [regex]::Matches($raw, '\b([A-Za-z_]\w*):([A-Za-z_]\w*)')) {
            if (-not $aliases.ContainsKey($m.Groups[1].Value)) { continue }
            $fq = $aliases[$m.Groups[1].Value] + '.' + $m.Groups[2].Value
            if ($fullTypes.ContainsKey($fq)) {
                foreach ($target in $fullTypes[$fq]) { Add-Dependency $path $target 'XAML前缀类型' }
            }
        }
        $match = [regex]::Match($raw, 'x:Class\s*=\s*"([^"]+)"')
        if ($match.Success -and $fullTypes.ContainsKey($match.Groups[1].Value)) {
            foreach ($target in $fullTypes[$match.Groups[1].Value]) { Add-Dependency $path $target 'x:Class对应partial声明' }
        }
        Add-Dependency $path ($path + '.cs') '同目录code-behind'
    }
    foreach ($m in [regex]::Matches($raw, '"([^"\r\n]+\.xaml)"')) {
        $uri = $m.Groups[1].Value; $component = $uri.IndexOf(';component/')
        if ($component -ge 0) {
            if ($uri.Substring(0, $component) -notmatch 'SunEyeVision.Plugin.Wpf') { continue }
            $target = 'src/Plugin.Wpf/' + $uri.Substring($component + 11)
        } elseif ($uri.StartsWith('Themes/')) { $target = 'src/Plugin.Wpf/' + $uri }
        else {
            $absolute = [IO.Path]::GetFullPath((Join-Path (Split-Path (Join-Path $root $path)) $uri))
            $target = [IO.Path]::GetRelativePath($root, $absolute).Replace('\', '/')
        }
        if ($files.ContainsKey($target)) { Add-Dependency $path $target '静态资源URI' }
        elseif ($path.EndsWith('ColorThemesResourceDictionary.cs') -and $uri -eq 'colorstyle.xaml') {
            foreach ($name in @('colorstyle', 'colorstyle1', 'colorstyle2', 'colorstyle3')) {
                Add-Dependency $path "src/Plugin.Wpf/Themes/Palettes/$name.xaml" '原代码ResourceRoot与名称白名单拼接'
            }
        } else { $unresolved.Add(@{ from = $path; uri = $uri; candidate = $target }) }
    }
}
$planPath = Join-Path $root 'docs/collaboration/evidence/B08_NEUTRAL_NAMING_CANDIDATE_20261030.json'
$plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
$selected = @($plan.files | Where-Object { $seen.Contains($_.source) })
$report = [ordered]@{
    status = '语法类型/委托/扩展方法及异目录XAML的保守候选；尚非完整语义/动态资源闭包，禁止直接执行改名'
    parserAssembly = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree].Assembly.GetName().FullName
    roots = $roots; includedFileCount = $seen.Count; includedFiles = @($seen | Sort-Object)
    edges = @($edges); unresolvedResourceReferences = @($unresolved)
    namespaceCandidateCount = $plan.sourceFileCount; selectedNamingCandidateCount = $selected.Count
    selectedNamingFiles = $selected
    remainingAccuracyGates = @('别名、同名类型与局部遮蔽须语义核对', '动态资源/资产/LogicalName/ResourceManager', '外部消费者及Catalog原件身份')
    productChanged = $false
}
$output = Join-Path $root 'docs/collaboration/evidence/B08_NAMING_REACHABILITY_CANDIDATE_20261030.json'
[IO.File]::WriteAllText($output, ($report | ConvertTo-Json -Depth 25), [Text.UTF8Encoding]::new($false))
Write-Output "候选图 $($seen.Count) 文件；原五族命名候选 $($selected.Count)；未解析静态资源 $($unresolved.Count)。未改产品。"
