$ErrorActionPreference = 'Stop'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker Desktop and the Docker CLI must be available.'
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI is required for this repeatable setup. Install it, then run this script again.'
}

$container = docker compose ps --status running --services azurite
if ($LASTEXITCODE -ne 0 -or $container -notcontains 'azurite') {
    throw 'Start Azurite first with: docker compose up -d azurite'
}

$connectionString = 'UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://127.0.0.1:10001'

foreach ($containerName in 'event-banners', 'profile-pictures') {
    az storage container create `
        --name $containerName `
        --connection-string $connectionString `
        --only-show-errors `
        --output none
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create or confirm Azurite container '$containerName'."
    }
}

az storage container set-permission `
    --name event-banners `
    --public-access blob `
    --connection-string $connectionString `
    --only-show-errors `
    --output none
if ($LASTEXITCODE -ne 0) {
    throw 'Could not set event-banners to public Blob read access.'
}

az storage container set-permission `
    --name profile-pictures `
    --public-access off `
    --connection-string $connectionString `
    --only-show-errors `
    --output none
if ($LASTEXITCODE -ne 0) {
    throw 'Could not set profile-pictures to private access.'
}

Write-Host 'Azurite containers are ready: event-banners is public Blob-read; profile-pictures is private.'
