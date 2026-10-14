param(
    [Parameter(Mandatory = $true)][string]$Destination
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$mods = @(
    @{ Name = 'UI Fixes'; Version = '6.0.4'; Url = 'https://github.com/tyfon7/UIFixes/releases/download/v6.0.4/Tyfon-UIFixes-6.0.4.zip' },
    @{ Name = 'Climbable Ladders'; Version = '1.0.4'; Url = 'https://github.com/bmpq/spt-ladders/releases/download/v1.0.4/tarkin-ladders-v1.0.4.zip' },
    @{ Name = 'Ref Friendly Quests'; Version = '2.1.0'; Url = 'https://github.com/acidphantasm/reffriendlyquests-csharp/releases/download/2.1.0/acidphantasm-reffriendlyquests.7z' },
    @{ Name = 'Kaeno Trader Scrolling'; Version = '4.1.0'; Url = 'https://github.com/DrakiaXYZ/SPT-Kaeno-TraderScrolling/releases/download/4.1.0/Kaeno-TraderScrolling-4.1.0.7z' },
    @{ Name = 'Search Open Containers'; Version = '1.5.0'; Url = 'https://github.com/DrakiaXYZ/SPT-SearchOpenContainers/releases/download/1.5.0/DrakiaXYZ-SearchOpenContainers-1.5.0.7z' }
)

$destinationRoot = [System.IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Force -Path $destinationRoot | Out-Null

$sevenZip = Get-Command 7z -ErrorAction SilentlyContinue
if (-not $sevenZip) { $sevenZip = Get-Command 7za -ErrorAction SilentlyContinue }
if (-not $sevenZip) {
    throw '7-Zip CLI (7z or 7za) must be installed for default SPT mods to be bundled.'
}

$working = Join-Path ([System.IO.Path]::GetTempPath()) ('spt-default-mods-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $working -Force | Out-Null
try {
    foreach ($mod in $mods) {
        Write-Host "Bundling $($mod.Name) $($mod.Version) into SPT..."
        $extension = [System.IO.Path]::GetExtension(([uri]$mod.Url).AbsolutePath)
        $archive = Join-Path $working ([guid]::NewGuid().ToString('N') + $extension)
        $extract = Join-Path $working ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $extract | Out-Null
        Invoke-WebRequest -Uri $mod.Url -OutFile $archive
        & $sevenZip.Source x "-o$extract" '-y' $archive | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Extraction failed for $($mod.Name)" }

        $foundFiles = Get-ChildItem -Path $extract -Recurse -File
        $installed = 0
        foreach ($file in $foundFiles) {
            # Preserve only recognized SPT mod installation roots, even when
            # an archive wraps its payload in an additional top-level directory.
            $relative = $file.FullName.Substring($extract.Length).TrimStart('\', '/')
            $normalized = $relative.Replace('\', '/')
            if ($normalized -match '(?i)(?:^|/)(BepInEx/plugins|BepInEx/patchers|user/mods)/(.+)$') {
                $root = $Matches[1]
                $rest = $Matches[2]
                $target = Join-Path $destinationRoot ($root.Replace('/', [System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar + $rest.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
                New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
                Copy-Item -LiteralPath $file.FullName -Destination $target -Force
                $installed++
            }
        }
        if ($installed -eq 0) {
            throw "No recognized BepInEx or user/mods files found in $($mod.Name) release. Refusing to silently publish an incomplete default installation."
        }
        Write-Host "Bundled $installed files for $($mod.Name)"
    }
}
finally {
    Remove-Item -LiteralPath $working -Recurse -Force -ErrorAction SilentlyContinue
}
