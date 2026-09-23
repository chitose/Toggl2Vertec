
param(
    [string] $version
)

if (!$version) {
    Write-Error "Version argument is required"
    exit -1
}

Write-Output "Building package for release version $version"

$repoRoot = Resolve-Path "$PSScriptRoot/.."
$projectFile = "$repoRoot\src\Toggl2Vertec\Toggl2Vertec.csproj"
# outside the repo so that the "git add ." below doesn't pick it up
$publishDir = Join-Path ([System.IO.Path]::GetTempPath()) "t2v-publish"
$releaseFile = "$repoRoot\t2v-win-x64.zip"
$manifestFile = "$repoRoot\t2v.json"
$gitTag = "v$version"

Push-Location $repoRoot
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
# only the app project - the solution also contains the tests
dotnet publish $projectFile -c Release -r win-x64 --no-self-contained -p:Version="$version" -o $publishDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed"
    Pop-Location
    exit -1
}

Compress-Archive -Path "$publishDir\*" -DestinationPath $releaseFile -Force
$sha256 = (Get-FileHash -Algorithm SHA256 $releaseFile).Hash

$manifest = Get-Content $manifestFile | ConvertFrom-Json
$manifest.version = $version
$manifest.hash = $sha256
$manifest.url = $manifest.url -replace "/v\d+\.\d+\.\d+/","/$gitTag/"
# UTF-8 without BOM - '>' writes UTF-16 in Windows PowerShell 5.1, which scoop cannot parse
[System.IO.File]::WriteAllText($manifestFile, ($manifest | ConvertTo-Json), (New-Object System.Text.UTF8Encoding $false))

git add .
git commit -m "Release $gitTag"
git tag $gitTag

Write-Output ""
Write-Output "Remaining manual steps:"
Write-Output "* check and push the release commit"
Write-Output "  > git show HEAD"
Write-Output "  > git push"
Write-Output "* push the release tag with"
Write-Output "  > git push origin $gitTag"
Write-Output "* create a new github release attach the generated $releaseFile to the github release"

Pop-Location
