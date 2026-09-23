# Prove that a consumer can restore the complete release with nuget.org as its only other feed.
[CmdletBinding()]
param([string] $Packages = 'artifacts')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packagePath = (Resolve-Path $Packages).Path
$archives = @(Get-ChildItem -LiteralPath $packagePath -Filter *.nupkg)
if (!$archives.Count) { throw 'No packages to validate.' }
$scratch = Join-Path ([IO.Path]::GetTempPath()) ("broiler-feed-check-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $scratch | Out-Null
$references = @()
$mappings = @()
$dependencies = @()
foreach ($archive in $archives) {
    $zip = [IO.Compression.ZipFile]::OpenRead($archive.FullName)
    try {
        $entry = @($zip.Entries | Where-Object FullName -like '*.nuspec')[0]
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $id = [Security.SecurityElement]::Escape($nuspec.package.metadata.id)
        $version = [Security.SecurityElement]::Escape($nuspec.package.metadata.version)
        $references += "    <PackageReference Include=`"$id`" Version=`"[$version]`" />"
        $mappings += "      <package pattern=`"$id`" />"
        foreach ($dependency in $nuspec.SelectNodes('//*[local-name()="dependency"]')) {
            $dependencies += [pscustomobject]@{ Package = $nuspec.package.metadata.id; Id = $dependency.id; Version = $dependency.version }
        }
    } finally { $zip.Dispose() }
}
$escapedPath = [Security.SecurityElement]::Escape($packagePath)
@"
<configuration>
  <packageSources>
    <clear />
    <add key="release" value="$escapedPath" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <disabledPackageSources><clear /></disabledPackageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="release">
$($mappings -join "`n")
    </packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content (Join-Path $scratch 'NuGet.config') -Encoding utf8
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
$($references -join "`n")
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $scratch 'Consumer.csproj') -Encoding utf8
# An isolated cache prevents installed developer packages from hiding feed gaps.
& dotnet restore (Join-Path $scratch 'Consumer.csproj') --configfile (Join-Path $scratch 'NuGet.config') --packages (Join-Path $scratch 'packages') --no-http-cache --nologo
if ($LASTEXITCODE -ne 0) { throw "Consumer restore from nuget.org failed. Publish missing dependencies there first. Diagnostics: $scratch" }
# A declared dependency version missing from nuget.org does not fail the restore: NuGet
# resolves the next higher version with only an NU1603 warning, so consumers would get
# bits this release was never built against. Check each declared dependency directly.
$assets = Get-Content -LiteralPath (Join-Path $scratch 'obj/project.assets.json') -Raw | ConvertFrom-Json
$libraries = @($assets.libraries.PSObject.Properties.Name)
foreach ($dependency in $dependencies) {
    $version = $dependency.Version -replace '^\[([^,\]]+).*$', '$1'
    if ($libraries -notcontains "$($dependency.Id)/$version") {
        $restored = @($libraries | Where-Object { $_ -like "$($dependency.Id)/*" }) -join ', '
        throw "$($dependency.Package) declares $($dependency.Id) $version, which nuget.org does not have (restored: $restored)."
    }
}
Write-Host "Consumer restore verified $($archives.Count) packages against nuget.org."
