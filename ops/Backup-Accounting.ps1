param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_.-]+$')][string]$Database,
    [Parameter(Mandatory)][string]$Destination,
    [string]$PostgresBin = ''
)
$ErrorActionPreference = 'Stop'
# Use PGHOST, PGPORT, PGUSER and PGPASSFILE (or pg_service.conf). Never put passwords in command arguments.
$dumpTool = if ($PostgresBin) { Join-Path $PostgresBin 'pg_dump.exe' } else { 'pg_dump' }
$restoreTool = if ($PostgresBin) { Join-Path $PostgresBin 'pg_restore.exe' } else { 'pg_restore' }
$backupRoot = [IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
$archive = Join-Path $backupRoot ($Database + '-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '.dump')
if (Test-Path -LiteralPath $archive) { throw 'Backup target already exists.' }
& $dumpTool --dbname=$Database --format=custom --no-owner --no-privileges --file=$archive
if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed; this is not a valid backup.' }
& $restoreTool --list $archive | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Backup archive validation failed.' }
$checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
@{ archive = [IO.Path]::GetFileName($archive); sha256 = $checksum; createdUtc = [DateTime]::UtcNow.ToString('o'); database = $Database } |
    ConvertTo-Json | Set-Content -LiteralPath ($archive + '.manifest.json') -Encoding utf8
Write-Output "Validated backup: $archive"
