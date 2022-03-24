param([string]$version, [string]$prerelease)

$ErrorActionPreference = 'Stop'

[string[]]$exclude = @('bin', 'obj', 'dist', '.*', 'Logs')
[string[]]$folders = @()
[string[]]$foldersNew = Get-Location
while ($foldersNew.Count -gt 0)
{
    [string[]]$folders += $foldersNew
    [string[]]$foldersNew = Get-ChildItem $foldersNew -Directory -Exclude $exclude
}
$folders = $folders | Sort-Object

function RegexReplace ($fileSearch, $replacePattern, $replaceWith)
{
    Get-ChildItem -File -Path $folders -Filter $fileSearch `
        | Select-Object -ExpandProperty FullName `
        | ForEach-Object {
            $text = [IO.File]::ReadAllText($_, [System.Text.Encoding]::UTF8)
            $replaced = $text -Replace $replacePattern, $replaceWith
            if ($replaced -ne $text) {
                Write-Output $_
                [IO.File]::WriteAllText($_, $replaced, [System.Text.Encoding]::UTF8)
            }
        }
}

If ($prerelease -eq 'auto')
{
    $prereleaseSuffix = ('-dev' + (get-date -format 'yyMMddHHmm') + (git rev-parse --short HEAD)).Substring(0,20)
}
ElseIf ($prerelease)
{
    $prereleaseSuffix = '-' + $prerelease
}
Else
{
    $prereleaseSuffix = ''
}
$fullVersion = $version.ToString() + $prereleaseSuffix
Write-Output "Setting version '$fullVersion'."

function RegexReplace ($fileSearch, $replacePattern, $replaceWith)
{
    Get-ChildItem $fileSearch -r `
        | ForEach-Object {
        $c = [IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::Default) -Replace $replacePattern, $replaceWith;
        [IO.File]::WriteAllText($_.FullName, $c, [System.Text.Encoding]::UTF8)
    }
}

Write-Output "Setting version '$fullVersion'."

RegexReplace '*AssemblyInfo.cs' '([\n^]\[assembly: Assembly(File)?Version(Attribute)?\(\").*(\"\)\])' ('${1}' + $version + '${4}')
RegexReplace '*AssemblyInfo.cs' '([\n^]\[assembly: AssemblyInformationalVersion(Attribute)?\(\").*(\"\)\])' ('${1}' + $fullVersion + '${3}')
RegexReplace '*.nuspec' '([\n^]\s*\<version\>).*(\<\/version\>\s*)' ('${1}' + $fullVersion + '${2}')
RegexReplace 'Directory.Build.props' '([\n^]\s*\<InformationalVersion\>).*(\<\/InformationalVersion\>\s*)' ('${1}' + $fullVersion + '${2}')
RegexReplace 'Directory.Build.props' '([\n^]\s*\<AssemblyVersion\>).*(\<\/AssemblyVersion\>\s*)' ('${1}' + $version + '${2}')
RegexReplace 'Directory.Build.props' '([\n^]\s*\<FileVersion\>).*(\<\/FileVersion\>\s*)' ('${1}' + $version + '${2}')
RegexReplace 'source.extension.vsixmanifest' '(\<Identity.*Version=\").*?(\")' ('${1}' + $version + '${2}')
RegexReplace 'RhetosLanguageServicesInstaller.vdproj' '(\"ProductVersion\" = \"8:).*?(\")' ('${1}' + $version + '${2}')
