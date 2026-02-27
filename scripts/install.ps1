<#
.SYNOPSIS
    Compass installer for Windows (PowerShell).
.DESCRIPTION
    Interactive setup that writes environment variables to .env.compass.
    Equivalent to scripts/install.sh for Linux/macOS.
#>
$ErrorActionPreference = 'Stop'

$RootDir = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$EnvFile = Join-Path $RootDir '.env.compass'

Write-Host 'Compass installer'
Write-Host 'Select model provider:'
Write-Host '  1) OpenAI'
Write-Host '  2) Anthropic'
Write-Host '  3) Gemini'
$providerChoice = Read-Host '>'

switch ($providerChoice) {
    '1' { $provider = 'openai';    $keyName = 'OPENAI_API_KEY';    $defaultModel = 'gpt-4o-mini' }
    '2' { $provider = 'anthropic'; $keyName = 'ANTHROPIC_API_KEY'; $defaultModel = 'claude-3-5-haiku-latest' }
    '3' { $provider = 'gemini';    $keyName = 'GEMINI_API_KEY';    $defaultModel = 'gemini-2.0-flash' }
    default { Write-Error 'Invalid provider choice'; exit 1 }
}

$apiKey = Read-Host "Enter $keyName"
$selectedModel = Read-Host "Enter model name [$defaultModel]"
if ([string]::IsNullOrWhiteSpace($selectedModel)) { $selectedModel = $defaultModel }

Write-Host ''
Write-Host 'Select deployment mode:'
Write-Host '  1) Local console'
Write-Host '  2) Discord channel'
$deployChoice = Read-Host '>'

$lines = @(
    "`$env:COMPASS_MODEL_PROVIDER='$provider'"
    "`$env:${keyName}='$apiKey'"
    "`$env:COMPASS_MODEL_NAME='$selectedModel'"
)

if ($deployChoice -eq '2') {
    $discordToken   = Read-Host 'Enter DISCORD_BOT_TOKEN'
    $discordChannel = Read-Host 'Enter DISCORD_CHANNEL_ID'
    $lines += "`$env:DISCORD_BOT_TOKEN='$discordToken'"
    $lines += "`$env:DISCORD_CHANNEL_ID='$discordChannel'"
}

$lines | Set-Content -Path $EnvFile -Encoding UTF8

Write-Host ''
Write-Host "Configuration saved to: $EnvFile"
Write-Host ''
Write-Host 'Next steps:'
$hasSourceLayout = (Test-Path (Join-Path $RootDir 'UtilityAi.Compass.sln')) -and (Test-Path (Join-Path $RootDir 'samples\Compass.SampleHost'))
if ($hasSourceLayout) {
    Write-Host "  1. dotnet build `"$RootDir\UtilityAi.Compass.sln`""
    Write-Host "  2. dotnet run --project `"$RootDir\samples\Compass.SampleHost`""
}
else {
    Write-Host '  1. Run: compass'
    Write-Host '  2. Use /help to view available commands.'
}
Write-Host ''
Write-Host 'The host loads .env.compass automatically — no need to source the file.'
Write-Host 'If Discord variables are configured, the host will start in Discord mode automatically.'
