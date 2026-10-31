param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$plan = Get-Content (Join-Path $root 'docs/collaboration/evidence/B08_NEUTRAL_NAMING_FILE_MAP_20261030.json') -Raw | ConvertFrom-Json
$moves = @($plan.fileMoves)
$types = @($plan.typeMoves)
foreach ($move in $moves) {
    $source = Join-Path $root $move.source
    $target = Join-Path $root $move.destination
    if (!(Test-Path -LiteralPath $source)) { throw "Missing source: $source" }
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $move.sha256) { throw "Source changed: $source" }
    if (Test-Path -LiteralPath $target) { throw "Destination exists: $target" }
    if (![IO.Path]::GetFullPath($target).StartsWith($root + [IO.Path]::DirectorySeparatorChar)) { throw 'Destination outside workspace' }
}
$files = @(& rg --files src tools tests scripts/verification installer | Where-Object { $_ -match '\.(cs|xaml|props|targets|csproj)$' -and $_ -notmatch '(^|[\\/])(obj|bin)([\\/])|_wpftmp\.csproj$' })
$original = @{}
$changed = @{}
$namespaceFiles = @{}
foreach ($file in $files) {
    $text = [IO.File]::ReadAllText((Join-Path $root $file))
    $original[$file.Replace('\','/')] = $text
    if ($file -like '*.cs') {
        $ns = [regex]::Match($text, '(?m)^namespace\s+([\w.]+)\s*[;{]').Groups[1].Value
        if ($ns) { $namespaceFiles[$ns] = @($namespaceFiles[$ns]) + $file.Replace('\','/') }
    }
}
$movedSources = @($moves.source)
$extensionMethods = @{}
foreach ($type in $types) {
    $extensionMethods[$type.sourceType] = @([regex]::Matches($original[$type.sourceFile], '\b(\w+)(?:<[^>]+>)?\s*\(\s*this\s') | ForEach-Object { $_.Groups[1].Value })
}
foreach ($file in @($original.Keys)) {
    $text = $original[$file]
    $before = $text
    foreach ($type in ($types | Sort-Object { $_.sourceType.Length } -Descending)) {
        $text = [regex]::Replace($text, '(?<![\w.])' + [regex]::Escape($type.sourceType) + '(?![\w])', $type.destinationType)
        if ($file -like '*.cs') {
            $relativeType = $type.sourceType.Replace('SunEyeVision.Plugin.Wpf.','')
            $text = [regex]::Replace($text, '(?<![\w.])' + [regex]::Escape($relativeType) + '(?![\w])', $type.destinationType)
        }
    }
    if ($file -like '*.cs') {
        $oldNs = [regex]::Match($before, '(?m)^namespace\s+([\w.]+)\s*[;{]').Groups[1].Value
        $ownMove = $moves | Where-Object source -EQ $file | Select-Object -First 1
        $imports = @([regex]::Matches($before, '(?m)^using\s+([\w.]+)\s*;') | ForEach-Object { $_.Groups[1].Value })
        foreach ($group in ($types | Group-Object sourceNamespace)) {
            $old = $group.Name
            if ($old -ne $oldNs -and $old -notin $imports) { continue }
            $body = [regex]::Replace($before, '(?m)^(using|namespace)\s+[^\r\n]+', '')
            $neededTypes = @($group.Group | Where-Object {
                if ([regex]::IsMatch($body, '\b' + [regex]::Escape($_.sourceName) + '\b')) { return $true }
                foreach ($method in $extensionMethods[$_.sourceType]) { if ([regex]::IsMatch($body, '\.' + [regex]::Escape($method) + '\s*(<|\()')) { return $true } }
                return $false
            })
            if (!$neededTypes.Count -and !($ownMove -and $ownMove.sourceNamespace -eq $old)) { continue }
            $newImports = @($group.Group.destinationNamespace | Sort-Object -Unique)
            foreach ($new in $newImports) {
                if ($text -notmatch ('(?m)^using ' + [regex]::Escape($new) + ';')) { $text = "using $new;`r`n" + $text }
            }
            $remaining = @($namespaceFiles[$old] | Where-Object { $_ -and $_ -notin $movedSources })
            if (!$remaining.Count) { $text = [regex]::Replace($text, '(?m)^using\s+' + [regex]::Escape($old) + '\s*;\r?\n', '') }
            elseif ($ownMove -and $ownMove.sourceNamespace -eq $old -and $old -notin $imports) { $text = "using $old;`r`n" + $text }
            foreach ($type in $group.Group | Where-Object { $_.sourceName -ne $_.destinationName }) {
                $text = [regex]::Replace($text, '\b' + [regex]::Escape($type.sourceName) + '\b', $type.destinationName)
            }
        }
        if ($ownMove -and $ownMove.sourceNamespace) {
            $text = [regex]::Replace($text, '(?m)^(namespace\s+)' + [regex]::Escape($ownMove.sourceNamespace) + '(\s*[;{])', '${1}' + $ownMove.destinationNamespace + '${2}')
        }
    }
    if ($file -like '*.xaml') {
        $extra = @()
        $prefixNumber = 0
        foreach ($decl in [regex]::Matches($before, 'xmlns:(\w+)="clr-namespace:([^";]+)(;assembly=[^"]+)?"')) {
            $prefix = $decl.Groups[1].Value
            $namespace = $decl.Groups[2].Value
            if (!@($types | Where-Object sourceNamespace -EQ $namespace).Count) { continue }
            foreach ($group in ($types | Where-Object sourceNamespace -EQ $namespace | Group-Object destinationNamespace)) {
                $newPrefix = 'b08n' + $prefixNumber++
                $used = $false
                foreach ($type in $group.Group) {
                    $pattern = '\b' + [regex]::Escape($prefix + ':' + $type.sourceName) + '(?![\w])'
                    if ([regex]::IsMatch($text,$pattern)) { $used = $true; $text = [regex]::Replace($text,$pattern,$newPrefix + ':' + $type.destinationName) }
                }
                if ($used) { $extra += ' xmlns:' + $newPrefix + '="clr-namespace:' + $group.Name + $decl.Groups[3].Value + '"' }
            }
            if ($text -notmatch ('\b' + [regex]::Escape($prefix) + ':')) { $text = $text.Replace($decl.Value,'') }
        }
        if ($extra.Count) { $rootElementPattern = [regex]::new('(<[\w:]+)(?=\s)'); $text = $rootElementPattern.Replace($text, { param($m) $m.Value + ($extra -join '') }, 1) }
    }
    foreach ($move in $moves) {
        foreach ($prefix in @('src/Plugin.Wpf/','')) {
            if ($prefix -eq '') { continue }
            $oldPath = $move.source.Substring($prefix.Length)
            $newPath = $move.destination.Substring($prefix.Length)
            $text = [regex]::Replace($text,[regex]::Escape($oldPath),$newPath,[Text.RegularExpressions.RegexOptions]::IgnoreCase)
            $text = $text.Replace($oldPath.Replace('/','\'),$newPath.Replace('/','\'))
        }
    }
    if ($file -eq 'src/Plugin.Wpf/Controls/Native/Display/AppsColorFun/Apps.ColorFun/ColorThemesResourceDictionary.cs') {
        $text = $text.Replace('component/Themes/Native/Display/AppsColorFun/','component/Themes/Palettes/')
    }
    if ($text -ne $before) { $changed[$file] = $text }
}
$xmlFailures = @()
foreach ($file in @($changed.Keys | Where-Object { $_ -match '\.(xaml|props|targets|csproj)$' })) {
    try { $document = [xml]$changed[$file] }
    catch { $xmlFailures += [pscustomobject]@{ file=$file; error=$_.Exception.Message } }
}
$aliases = @()
foreach ($file in @($original.Keys | Where-Object { $_ -like '*.cs' })) {
    foreach ($alias in [regex]::Matches($original[$file], '(?m)^using\s+(\w+)\s*=\s*([\w.]+)\s*;')) {
        if ($alias.Groups[2].Value -in $types.sourceNamespace) { $aliases += [pscustomobject]@{ file=$file; declaration=$alias.Value } }
    }
}
$report = [ordered]@{ mode = $(if($Apply){'apply'}else{'preview'}); fileMoves=$moves.Count; typeMoves=$types.Count; textChanges=$changed.Count; consumers=@($changed.Keys | Sort-Object); xmlFailures=$xmlFailures; namespaceAliasesRequiringReview=$aliases; notes='All hashes and destinations validated before writes. Preview does not prove compilation or semantic closure.' }
if ($Apply) {
    if ($xmlFailures.Count -or $aliases.Count) { throw 'Unresolved XML or namespace aliases' }
    $catalogPath = 'src/Plugin.Wpf/Catalog/controls.json'
    $catalogText = [IO.File]::ReadAllText((Join-Path $root $catalogPath))
    $catalog = $catalogText | ConvertFrom-Json -AsHashtable
    function Update-CatalogNode($node) {
        if ($node -is [Collections.IDictionary]) {
            foreach ($key in @($node.Keys)) {
                if ($key -match '^(original|assembly|mvid|sourceAssembly)') { continue }
                $value = $node[$key]
                if ($value -is [string]) {
                    foreach ($move in $moves) { $value = [regex]::Replace($value,[regex]::Escape($move.source),$move.destination,[Text.RegularExpressions.RegexOptions]::IgnoreCase) }
                    foreach ($type in $types) { $value = $value.Replace($type.sourceType,$type.destinationType) }
                    $node[$key] = $value
                } else { Update-CatalogNode $value }
            }
        } elseif ($node -is [Collections.IEnumerable] -and $node -isnot [string]) {
            foreach ($item in $node) { Update-CatalogNode $item }
        }
    }
    Update-CatalogNode $catalog
    $backups = @{}
    foreach ($file in @(@($changed.Keys) + @($moves.source) + @($catalogPath) | Sort-Object -Unique)) {
        $backups[$file] = [IO.File]::ReadAllBytes((Join-Path $root $file))
    }
    $completedMoves = [Collections.Generic.List[object]]::new()
    try {
        foreach ($file in $changed.Keys) { [IO.File]::WriteAllText((Join-Path $root $file),$changed[$file],[Text.UTF8Encoding]::new($false)) }
        foreach ($move in $moves) {
            $target = Join-Path $root $move.destination
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            Move-Item -LiteralPath (Join-Path $root $move.source) -Destination $target
            $completedMoves.Add($move)
        }
        function Update-LocalHashes($node) {
            if ($node -is [Collections.IDictionary]) {
                if ($node.Contains('localPath') -and $node.Contains('localSha256')) {
                    $local = Join-Path $root $node.localPath
                    if (Test-Path -LiteralPath $local -PathType Leaf) { $node.localSha256 = (Get-FileHash -LiteralPath $local -Algorithm SHA256).Hash.ToLowerInvariant() }
                }
                foreach ($key in @($node.Keys)) { Update-LocalHashes $node[$key] }
            } elseif ($node -is [Collections.IEnumerable] -and $node -isnot [string]) { foreach ($item in $node) { Update-LocalHashes $item } }
        }
        Update-LocalHashes $catalog
        [IO.File]::WriteAllText((Join-Path $root $catalogPath),($catalog | ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
        $report['catalogUpdated'] = $true
        $report['productChanged'] = $true
    } catch {
        for ($i=$completedMoves.Count-1; $i -ge 0; $i--) {
            $move = $completedMoves[$i]
            Move-Item -LiteralPath (Join-Path $root $move.destination) -Destination (Join-Path $root $move.source)
        }
        foreach ($file in $backups.Keys) { [IO.File]::WriteAllBytes((Join-Path $root $file),$backups[$file]) }
        throw
    }
}
$reportName = if($Apply){'B08_NEUTRAL_NAMING_APPLIED_20261030.json'}else{'B08_NEUTRAL_NAMING_PREVIEW_20261030.json'}
$report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root "docs/collaboration/evidence/$reportName") -Encoding utf8
[pscustomobject]$report | Select-Object mode,fileMoves,typeMoves,textChanges | ConvertTo-Json
