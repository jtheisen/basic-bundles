param (
    [switch] $includeParticulars = $false,
    [switch] $updateAppVeyorVersion = $false
)

$version = [System.IO.File]::ReadAllText("version.txt")

$versionUntilDash = $version.Split('-')[0];

if ($includeParticulars) {
    $date = [DateTime]::Now.ToString("yyMMdd-HHmm");

    $ref = $(git rev-parse --short HEAD)

    $longversion = "${version}-${ref}-$date"
}
else {
    $longversion = $version
}

if ($updateAppVeyorVersion) {
    if (Get-Command "Update-AppveyorBuild" -ErrorAction SilentlyContinue) {
        Update-AppveyorBuild -Version "$longversion"
    } else {
        echo "Could not find 'Update-AppveyorBuild', AppVeyor version not updated."
    }
}

foreach ($file in dir -Recurse $path | where { $_.Name -eq "AssemblyInfo.cs" }) {
    (gc $file.FullName) `
    | foreach { $_ -replace '\[assembly: AssemblyVersion\(\"(.*)\"\)\]', "[assembly: AssemblyVersion(""$versionUntilDash"")]" } `
    | foreach { $_ -replace '\[assembly: AssemblyFileVersion\(\"(.*)\"\)\]', "[assembly: AssemblyFileVersion(""$versionUntilDash"")]" } `
    | foreach { $_ -replace '\[assembly: AssemblyInformationalVersion\(\"(.*)\"\)\]', "[assembly: AssemblyInformationalVersion(""$longversion"")]" } `
    | sc -Encoding UTF8 $file.FullName
}

foreach ($file in dir -Recurse $path | where { $_.Name -like "*.nuspec" }) {
    (gc $file.FullName) `
    | foreach { $_ -replace '<version>(.*)</version>', "<version>$version</version>" } `
    | sc -Encoding UTF8 $file.FullName
}
