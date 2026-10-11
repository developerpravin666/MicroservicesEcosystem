param([Parameter(Mandatory)][string]$Archive, [string]$PostgresBin = '')
$ErrorActionPreference = 'Stop'
$archivePath = (Resolve-Path -LiteralPath $Archive).Path
$manifest = Get-Content -LiteralPath ($archivePath + '.manifest.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $manifest.sha256) { throw 'Backup checksum does not match.' }
$restoreDb = 'accounting_restore_check_' + [Guid]::NewGuid().ToString('N')
$createTool = if ($PostgresBin) { Join-Path $PostgresBin 'createdb.exe' } else { 'createdb' }
$restoreTool = if ($PostgresBin) { Join-Path $PostgresBin 'pg_restore.exe' } else { 'pg_restore' }
$queryTool = if ($PostgresBin) { Join-Path $PostgresBin 'psql.exe' } else { 'psql' }
# Create a new scratch database. Never overwrite or clean an existing database.
& $createTool $restoreDb
if ($LASTEXITCODE -ne 0) { throw 'Could not create the scratch restore database.' }
& $restoreTool --dbname=$restoreDb --exit-on-error --single-transaction --no-owner --no-privileges $archivePath
if ($LASTEXITCODE -ne 0) { throw "Restore failed. Inspect scratch database $restoreDb." }
$verification = @'
DO $$ DECLARE s record; bad bigint; BEGIN
 FOR s IN SELECT table_schema FROM information_schema.tables WHERE table_name='journal_lines' AND table_schema NOT IN ('public','information_schema') LOOP
  EXECUTE format('SELECT count(*) FROM (SELECT journal_entry_id FROM %I.journal_lines WHERE is_deleted=false GROUP BY journal_entry_id HAVING sum(debit)<>sum(credit)) q', s.table_schema) INTO bad;
  IF bad<>0 THEN RAISE EXCEPTION 'Unbalanced journals in schema %',s.table_schema; END IF;
 END LOOP;
END $$;
'@
$verification | & $queryTool --dbname=$restoreDb --set=ON_ERROR_STOP=1 --quiet
if ($LASTEXITCODE -ne 0) { throw 'Restored ledger validation failed.' }
Write-Output "Restore and ledger verification passed. Scratch database retained for migration rehearsal: $restoreDb"
