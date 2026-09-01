[CmdletBinding()]
param(
    [ValidateSet('Codex', 'Claude', 'Copilot', 'All', 'Custom')]
    [string] $Target = 'Codex',

    [string] $Destination,

    [switch] $SkipCli
)

$ErrorActionPreference = 'Stop'

function Get-SkillRoots {
    param([string] $RequestedTarget, [string] $CustomDestination)

    $homeDirectory = [Environment]::GetFolderPath('UserProfile')
    $roots = @{
        Codex = (Join-Path $homeDirectory '.codex\skills')
        Claude = (Join-Path $homeDirectory '.claude\skills')
        Copilot = (Join-Path $homeDirectory '.copilot\skills')
    }

    if ($RequestedTarget -eq 'Custom') {
        if ([string]::IsNullOrWhiteSpace($CustomDestination)) {
            throw 'Custom target requires -Destination <skills-root>.'
        }

        return @($CustomDestination)
    }

    if ($RequestedTarget -eq 'All') {
        return @($roots.Codex, $roots.Claude, $roots.Copilot)
    }

    return @($roots[$RequestedTarget])
}

function Install-Skill {
    param([string] $Source, [string] $SkillsRoot)

    New-Item -ItemType Directory -Path $SkillsRoot -Force | Out-Null
    $target = Join-Path $SkillsRoot 'project-wiki'
    if (Test-Path -LiteralPath $target) {
        $backup = "$target.backup.$(Get-Date -Format 'yyyyMMddHHmmss')"
        Move-Item -LiteralPath $target -Destination $backup
        Write-Host "Backed up existing skill: $backup"
    }

    Copy-Item -LiteralPath $Source -Destination $target -Recurse -Force
    Write-Host "Installed skill: $target"
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$skillSource = Join-Path $repositoryRoot '.agents\skills\project-wiki'
$cliProject = Join-Path $repositoryRoot 'src\ProjectWiki.Cli\ProjectWiki.Cli.csproj'

if (-not (Test-Path -LiteralPath (Join-Path $skillSource 'SKILL.md'))) {
    throw "Could not find the project-wiki skill at $skillSource."
}

Get-SkillRoots -RequestedTarget $Target -CustomDestination $Destination |
    ForEach-Object { Install-Skill -Source $skillSource -SkillsRoot $_ }

if ($SkipCli) {
    Write-Host 'Skipped global CLI installation.'
    exit 0
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK is required to package and install the project-wiki CLI.'
}

$packageDirectory = Join-Path ([IO.Path]::GetTempPath()) "project-wiki-tool-$([guid]::NewGuid().ToString('N'))"
try {
    New-Item -ItemType Directory -Path $packageDirectory | Out-Null
    & dotnet pack $cliProject --configuration Release --output $packageDirectory
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to package ProjectWiki.Cli.'
    }

    $installed = & dotnet tool list --global | Select-String -Pattern '^projectwiki\.cli\s' -CaseSensitive:$false
    if ($installed) {
        & dotnet tool uninstall --global ProjectWiki.Cli
        if ($LASTEXITCODE -ne 0) {
            throw 'Failed to replace the existing global ProjectWiki.Cli tool.'
        }
    }

    & dotnet tool install --global ProjectWiki.Cli --add-source $packageDirectory
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to install ProjectWiki.Cli as a global .NET tool.'
    }

    Write-Host 'Installed global command: project-wiki'
    Write-Host 'Restart the agent session, then run: project-wiki --help'
}
finally {
    if (Test-Path -LiteralPath $packageDirectory) {
        Remove-Item -LiteralPath $packageDirectory -Recurse -Force
    }
}
