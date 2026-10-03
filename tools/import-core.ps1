# Перенос Core из CommCodeChecker 0.9.0 в CommCodeVerifier.Core.
# Запуск из корня репозитория:
#   powershell -ExecutionPolicy Bypass -File tools\import-core.ps1
# Если есть локальная копия CommCodeChecker, можно взять файлы из неё:
#   powershell -ExecutionPolicy Bypass -File tools\import-core.ps1 -LocalCore "D:\Git\CommCodeChecker\src\CommCodeChecker\Core"

param([string]$LocalCore = "")

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$base  = 'https://raw.githubusercontent.com/UncleKamineko/CommCodeChecker/main/src/CommCodeChecker/Core'
$dest  = Join-Path $PSScriptRoot '..\src\CommCodeVerifier.Core'
$files = 'BatchProcessor.cs', 'CharReplacements.cs', 'CodeProcessor.cs', 'ImChecker.cs', 'ImRules.cs',
         'Morphology.cs', 'ReportWriter.cs', 'RuleCatalog.cs', 'SelfTest.cs', 'TrackedString.cs'
$utf8Bom = New-Object System.Text.UTF8Encoding($true)

foreach ($f in $files) {
    $path = Join-Path $dest $f
    if ($LocalCore) { Copy-Item (Join-Path $LocalCore $f) $path -Force }
    else            { Invoke-WebRequest -Uri "$base/$f" -OutFile $path -UseBasicParsing }

    $text = [IO.File]::ReadAllText($path)
    if ($text -notmatch 'namespace CommCodeChecker\.Core;') { throw "$f : не найдено пространство имён CommCodeChecker.Core" }
    $text = $text.Replace('namespace CommCodeChecker.Core;', 'namespace CommCodeVerifier.Core;')
    if ($text -match 'CommCodeChecker') { Write-Warning "$f : осталось упоминание CommCodeChecker" }

    [IO.File]::WriteAllText($path, $text, $utf8Bom)
    Write-Host "OK  $f"
}
Write-Host "Готово: $($files.Count) файлов в $dest"