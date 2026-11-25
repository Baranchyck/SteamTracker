# Перевірка NuGet-пакетів на відомі вразливості (запускається в CI і локально, після restore).
# `dotnet list package --vulnerable` повертає код 0 навіть коли вразливості є,
# тому розбираємо JSON самі: High і Critical валять збірку, Low і Moderate лише показуються.
param(
    [string]$Solution = 'SteamTracker/SteamTracker.sln',
    # Готовий JSON замість запуску dotnet — щоб перевірити сам скрипт на прикладі
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

if ($ReportPath) {
    $json = Get-Content $ReportPath -Raw -Encoding UTF8
}
else {
    $json = dotnet list $Solution package --vulnerable --include-transitive --format json | Out-String
    if ($LASTEXITCODE -ne 0) {
        Write-Host $json
        Write-Host "::error::dotnet list package завершився з кодом $LASTEXITCODE"
        exit 1
    }
}

$report = $json | ConvertFrom-Json

# Без цього зламана перевірка (немає зв'язку з nuget.org, проєкт без restore) виглядала б як «усе чисто»
$errors = @($report.problems | Where-Object { $_.level -eq 'error' })
if ($errors.Count -gt 0 -or -not $report.projects) {
    $errors | ForEach-Object { Write-Host "::error::$($_.text)" }
    Write-Host '::error::Не вдалося перевірити пакети на вразливості'
    exit 1
}

$found = @(
    foreach ($project in $report.projects) {
        foreach ($framework in $project.frameworks) {
            foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
                foreach ($vulnerability in $package.vulnerabilities) {
                    [pscustomobject]@{
                        Project  = [System.IO.Path]::GetFileNameWithoutExtension($project.path)
                        Package  = $package.id
                        Version  = $package.resolvedVersion
                        Severity = $vulnerability.severity
                        Url      = $vulnerability.advisoryurl
                    }
                }
            }
        }
    }
)

$blocking = @($found | Where-Object { $_.Severity -in 'High', 'Critical' })

$summary = @('## Вразливості NuGet-пакетів', '')
if ($found.Count -eq 0) {
    $summary += "✅ Відомих вразливостей не знайдено (проєктів перевірено: $(@($report.projects).Count))."
}
else {
    $summary += if ($blocking.Count -gt 0) { "❌ High/Critical: $($blocking.Count)" }
                else { '⚠️ Є вразливості Low/Moderate, збірку вони не валять.' }
    $summary += ''
    $summary += '| Проєкт | Пакет | Версія | Рівень | Advisory |'
    $summary += '|---|---|---|---|---|'
    $summary += $found | ForEach-Object {
        "| $($_.Project) | $($_.Package) | $($_.Version) | $($_.Severity) | $($_.Url) |"
    }
}

$summary | ForEach-Object { Write-Host $_ }
if ($env:GITHUB_STEP_SUMMARY) {
    $summary | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
}

if ($blocking.Count -gt 0) {
    Write-Host '::error::Знайдено пакети з вразливістю High або Critical — онови їх'
    exit 1
}

exit 0
