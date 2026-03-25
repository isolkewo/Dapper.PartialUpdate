param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$args = @("pack", ".\Dapper.PartialUpdate.csproj", "-c", "Release")

if ($Version) {
    $args += "/p:PackageVersion=$Version"
}

dotnet @args
