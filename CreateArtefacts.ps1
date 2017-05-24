$artefactDir = "Artefacts"
$tempDir = "Temp"
$version = "1.2.0"
$rootPath = "."

function Main {
    if (Test-Path $artefactDir) {
        rd $artefactDir -Recurse | Out-Null
    }
    md -Name $artefactDir | Out-Null

    if (Test-Path $tempDir) {
        rd $tempDir -Recurse | Out-Null
    }
    md -Name $tempDir | Out-Null

    # FlaUInspect
    $inspectDir = Join-Path $tempDir "FlaUInspect-$version"
    Copy-Item -Path $rootPath\src\FlaUInspect\bin -Destination $inspectDir -Recurse
    Get-ChildItem $inspectDir -Include *.pdb,*.xml,*.vshost.*,*RANDOM_SEED* -Recurse | Remove-Item
    Deploy-License $inspectDir
    
    # Create Zips
    [Reflection.Assembly]::LoadWithPartialName("System.IO.Compression.FileSystem") | Out-Null
    $compression = [System.IO.Compression.CompressionLevel]::Optimal
    $includeBaseDirectory = $false
    [System.IO.Compression.ZipFile]::CreateFromDirectory($inspectDir, (Join-Path $artefactDir "FlaUInspect-$version.zip"), $compression, $includeBaseDirectory)

    Create-Packages

    # Cleanup
    rd $tempDir -Recurse
}

function Deploy-License($dest) {
    Copy-Item -Path $rootPath\CHANGELOG.md -Destination $dest
    Copy-Item -Path $rootPath\LICENSE.txt -Destination $dest
}

function Create-Packages() {
    choco pack "$rootPath\nuspec\FlaUInspect.nuspec" -OutputDirectory $artefactDir --version $version
}

Main