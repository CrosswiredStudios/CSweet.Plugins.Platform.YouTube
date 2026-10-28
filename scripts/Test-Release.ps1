[CmdletBinding()]
param([string]$ExpectedVersion)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    $manifest = Get-Content csweet-plugin.json -Raw | ConvertFrom-Json
    $version = [string]$manifest.version
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'A three-part release version is required.' }
    if ($ExpectedVersion -and $ExpectedVersion -ne $version) { throw "Requested $ExpectedVersion, manifest is $version." }
    $props = [xml](Get-Content Directory.Build.props -Raw)
    if ([string]$props.Project.PropertyGroup.Version -ne $version) { throw 'Manifest and package versions differ.' }
    $notes = "releases/$version.md"
    if (!(Test-Path -LiteralPath $notes) -or (Get-Content $notes -First 1) -ne "# $version") {
        throw "Missing or mismatched release notes: $notes"
    }
    dotnet restore CSweet.Plugins.Platform.YouTube.slnx --configfile NuGet.Config
    dotnet test CSweet.Plugins.Platform.YouTube.slnx -c Release --no-restore --logger 'trx;LogFileName=connector.trx' --results-directory artifacts/test-results
    dotnet run --project src/CSweet.Plugins.Platform.YouTube -c Release --no-build -- --self-test
    dotnet pack src/CSweet.Plugins.Platform.YouTube -c Release --no-build --no-restore -o artifacts/packages

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $packageName = "CSweet.Plugins.Platform.YouTube.$version.nupkg"
    $package = Join-Path $repoRoot "artifacts/packages/$packageName"
    $archive = [System.IO.Compression.ZipFile]::OpenRead($package)
    try {
        function Read-Entry([string]$name) {
            $entry = $archive.GetEntry($name)
            if (!$entry) { throw "Missing package entry: $name" }
            $reader = [System.IO.StreamReader]::new($entry.Open())
            try { $reader.ReadToEnd() } finally { $reader.Dispose() }
        }
        $nuspec = [xml](Read-Entry 'CSweet.Plugins.Platform.YouTube.nuspec')
        $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        if ($metadata.id -ne 'CSweet.Plugins.Platform.YouTube' -or $metadata.version -ne $version) { throw 'Incorrect NuGet identity.' }
        if ($metadata.repository.url -ne [string]$props.Project.PropertyGroup.RepositoryUrl) { throw 'Incorrect repository metadata.' }
        $sdk = $metadata.SelectSingleNode(".//*[local-name()='dependency' and @id='CSweet.Agent.SDK']")
        $project = [xml](Get-Content src/CSweet.Plugins.Platform.YouTube/CSweet.Plugins.Platform.YouTube.csproj -Raw)
        $sdkVersion = [string]($project.Project.ItemGroup.PackageReference | Where-Object Include -eq 'CSweet.Agent.SDK').Version
        if (!$sdk -or $sdk.version -ne $sdkVersion) { throw 'Incorrect SDK dependency.' }
        if ((Read-Entry 'contentFiles/any/any/csweet-plugin.json') -cne (Get-Content csweet-plugin.json -Raw)) { throw 'Packaged manifest differs from reviewed source.' }
        foreach ($file in @('README.md', 'GOOGLE_SETUP.md', 'PRIVACY.md', 'TERMS.md')) { $null = Read-Entry $file }
        if (!$archive.GetEntry('lib/net10.0/CSweet.Plugins.Platform.YouTube.dll')) { throw 'Missing consumer assembly.' }
    } finally { $archive.Dispose() }

    # Fresh cache and exact source mapping prevent project/global caches from hiding broken packaging.
    $consumerPackages = Join-Path $repoRoot ('artifacts/consumer-packages/' + [Guid]::NewGuid().ToString('N'))
    dotnet restore tests/PackageConsumer/PackageConsumer.csproj --configfile tests/PackageConsumer/NuGet.Config --packages $consumerPackages --force --no-http-cache "-p:ConnectorPackageVersion=$version"
    dotnet run --project tests/PackageConsumer -c Release --no-restore "-p:ConnectorPackageVersion=$version" "-p:RestorePackagesPath=$consumerPackages"

    $releaseDir = Join-Path $repoRoot "artifacts/release/$version"
    $null = New-Item -ItemType Directory -Path $releaseDir -Force
    Copy-Item -LiteralPath $package -Destination $releaseDir
    Copy-Item -LiteralPath csweet-plugin.json, $notes -Destination $releaseDir
    $commit = git -C $repoRoot rev-parse HEAD
    $dirty = @(git -C $repoRoot status --porcelain).Count -gt 0
    $evidence = [ordered]@{ packageId = 'CSweet.Plugins.Platform.YouTube'; version = $version; sourceCommit = $commit;
        sourceHasLocalChanges = $dirty; sdkVersion = $sdkVersion; protocolMinimum = $manifest.protocol.minimumVersion;
        manifestSha256 = (Get-FileHash csweet-plugin.json -Algorithm SHA256).Hash.ToLowerInvariant();
        packageSha256 = (Get-FileHash $package -Algorithm SHA256).Hash.ToLowerInvariant();
        verification = @('connector tests', 'self-test', 'package inspection', 'isolated NuGet-only consumer');
        realProviderAccepted = $false; publishedByThisScript = $false }
    [System.IO.File]::WriteAllText((Join-Path $releaseDir 'release-evidence.json'), ($evidence | ConvertTo-Json -Depth 4), [System.Text.UTF8Encoding]::new($false))
    $hashes = @($packageName, 'csweet-plugin.json', "$version.md", 'release-evidence.json') | ForEach-Object {
        $digest = (Get-FileHash -LiteralPath (Join-Path $releaseDir $_) -Algorithm SHA256).Hash.ToLowerInvariant()
        "$digest  $_"
    }
    [System.IO.File]::WriteAllLines((Join-Path $releaseDir 'SHA256SUMS'), $hashes, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Verified connector $version. Local artifacts: $releaseDir. Nothing was published or deployed."
} finally { Pop-Location }
