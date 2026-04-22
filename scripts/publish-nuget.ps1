<#
.SYNOPSIS
    VSMVVM NuGet 패키지 빌드/팩/푸시 자동화 스크립트

.DESCRIPTION
    VSMVVM.Core, VSMVVM.WPF, VSMVVM.WPF.Design 세 패키지를
    의존성 순서(Core -> WPF -> Design)대로 빌드, 팩, NuGet.org에 푸시합니다.

.PARAMETER Version
    배포할 버전 (예: 1.0.1). 지정하면 Directory.Build.props의 <Version>을 갱신합니다.
    생략하면 현재 Directory.Build.props의 버전을 그대로 사용합니다.

.PARAMETER ApiKey
    NuGet.org API 키. 생략하면 환경 변수 NUGET_API_KEY를 사용합니다.

.PARAMETER SkipPush
    지정하면 빌드/팩까지만 하고 푸시는 건너뜁니다.

.PARAMETER OutputDir
    .nupkg 출력 폴더 (기본: ./nupkgs)

.EXAMPLE
    .\scripts\publish-nuget.ps1 -Version 1.0.1 -ApiKey oy2...

.EXAMPLE
    $env:NUGET_API_KEY = "oy2..."
    .\scripts\publish-nuget.ps1 -Version 1.0.1

.EXAMPLE
    .\scripts\publish-nuget.ps1 -SkipPush
#>

[CmdletBinding()]
param(
    [string]$Version,
    [string]$ApiKey = $env:NUGET_API_KEY,
    [switch]$SkipPush,
    [string]$OutputDir = "./nupkgs",
    [string]$Configuration = "Release",
    [string]$Source = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$propsFile = Join-Path $repoRoot "Directory.Build.props"
if (-not (Test-Path $propsFile)) {
    throw "Directory.Build.props not found at $propsFile"
}

$projects = @(
    "src/VSMVVM.Core/VSMVVM.Core.csproj",
    "src/VSMVVM.WPF/VSMVVM.WPF.csproj",
    "src/VSMVVM.WPF.Design/VSMVVM.WPF.Design.csproj"
)

function Write-Step($msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}

if ($Version) {
    Write-Step "Directory.Build.props 버전 갱신: $Version"
    $content = Get-Content $propsFile -Raw
    $new = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        '<Version>[^<]+</Version>',
        "<Version>$Version</Version>"
    )
    Set-Content -Path $propsFile -Value $new -Encoding UTF8 -NoNewline
    Write-Host "   OK"
} else {
    $match = Select-String -Path $propsFile -Pattern '<Version>([^<]+)</Version>'
    if ($match) {
        $Version = $match.Matches[0].Groups[1].Value
    }
    Write-Host "현재 버전: $Version"
}

if (-not $SkipPush -and -not $ApiKey) {
    throw "API 키가 없습니다. -ApiKey 파라미터 또는 NUGET_API_KEY 환경 변수를 설정하세요. (빌드만 원하시면 -SkipPush)"
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

foreach ($proj in $projects) {
    Write-Step "빌드: $proj"
    dotnet build $proj -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "빌드 실패: $proj" }
}

foreach ($proj in $projects) {
    Write-Step "팩: $proj"
    dotnet pack $proj -c $Configuration -o $OutputDir --no-build
    if ($LASTEXITCODE -ne 0) { throw "팩 실패: $proj" }
}

if ($SkipPush) {
    Write-Step "SkipPush 지정됨 - 푸시 건너뜀"
    Write-Host "생성된 패키지:"
    Get-ChildItem -Path $OutputDir -Filter "*.$Version.nupkg" | ForEach-Object {
        Write-Host "   $($_.Name)"
    }
    return
}

$packageIds = @("VSMVVM.Core", "VSMVVM.WPF", "VSMVVM.WPF.Design")
foreach ($id in $packageIds) {
    $pkg = Join-Path $OutputDir "$id.$Version.nupkg"
    if (-not (Test-Path $pkg)) {
        throw "패키지를 찾을 수 없음: $pkg"
    }
    Write-Step "푸시: $id $Version"
    dotnet nuget push $pkg --api-key $ApiKey --source $Source --skip-duplicate
    if ($LASTEXITCODE -ne 0) { throw "푸시 실패: $id" }
}

Write-Step "완료!"
Write-Host "인덱싱에 최대 1시간 소요될 수 있습니다."
Write-Host "   https://www.nuget.org/packages/VSMVVM.Core/$Version"
Write-Host "   https://www.nuget.org/packages/VSMVVM.WPF/$Version"
Write-Host "   https://www.nuget.org/packages/VSMVVM.WPF.Design/$Version"
