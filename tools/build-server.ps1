<#
.SYNOPSIS
    Builds, tests and publishes the Rhetos language server without Visual Studio.

.DESCRIPTION
    Uses only the .NET SDK command-line tools on the Rhetos.LanguageServices.Server.slnf
    solution filter, which excludes the Visual Studio specific projects (VSIX, installer,
    test Rhetos applications).

    Steps:
    1. dotnet restore of the solution filter.
    2. dotnet build of the solution filter.
    3. dotnet test of CodeAnalysis.Test and LspIntegration.Test (unless -SkipTests).
    4. dotnet publish of the server (framework-dependent) to dist\server.

    The script can be run from any current directory; all paths are resolved
    relative to the repository root (the parent folder of this script's folder).
    The script stops with the dotnet exit code on the first failed step.

.PARAMETER Configuration
    Build configuration passed to dotnet (-c). Default is 'Release'.

.PARAMETER SkipTests
    Skips the dotnet test steps.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\build-server.ps1

.EXAMPLE
    .\tools\build-server.ps1 -Configuration Debug -SkipTests
#>
param(
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionFilter = Join-Path $repoRoot 'Rhetos.LanguageServices.Server.slnf'
$serverProject = Join-Path $repoRoot 'src\Server\Server.csproj'
$testProjects = @(
    (Join-Path $repoRoot 'test\CodeAnalysis.Test\CodeAnalysis.Test.csproj'),
    (Join-Path $repoRoot 'test\LspIntegration.Test\LspIntegration.Test.csproj')
)
$publishFolder = Join-Path $repoRoot 'dist\server'

function Invoke-Dotnet ([string]$stepName, [string[]]$arguments)
{
    Write-Output "=== $stepName ==="
    Write-Output ("dotnet " + ($arguments -join ' '))
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0)
    {
        Write-Error "Step '$stepName' failed: dotnet exited with code $LASTEXITCODE." -ErrorAction Continue
        exit $LASTEXITCODE
    }
}

Push-Location $repoRoot
try
{
    Invoke-Dotnet 'Restore' @('restore', $solutionFilter)
    Invoke-Dotnet 'Build' @('build', $solutionFilter, '-c', $Configuration, '--no-restore')

    if (-not $SkipTests)
    {
        foreach ($testProject in $testProjects)
        {
            Invoke-Dotnet ('Test ' + [IO.Path]::GetFileNameWithoutExtension($testProject)) @('test', $testProject, '-c', $Configuration, '--no-build')
        }
    }

    Invoke-Dotnet 'Publish' @('publish', $serverProject, '-c', $Configuration, '--no-build', '-o', $publishFolder)
}
finally
{
    Pop-Location
}

Write-Output "Server published to: $publishFolder"
