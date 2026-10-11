param(
    [string]$Source = (Join-Path $PSScriptRoot '../docs/accounting-user-manual.md'),
    [string]$Destination = (Join-Path $PSScriptRoot '../docs/accounting-user-manual.html')
)
$ErrorActionPreference = 'Stop'
# The manual uses headings, paragraphs, lists, tables and inline Markdown links.
function Format-Inline([string]$Value) {
    $safe = [System.Net.WebUtility]::HtmlEncode($Value)
    $safe = [regex]::Replace($safe, '\x60([^\x60]+)\x60', '<code>$1</code>')
    $safe = [regex]::Replace($safe, '\*\*(.+?)\*\*', '<strong>$1</strong>')
    return [regex]::Replace($safe, '\[([^\]]+)\]\((#[a-z0-9-]+)\)', '<a href="$2">$1</a>')
}
$parts = [System.Collections.Generic.List[string]]::new()
$list = ''
$table = $false
foreach ($line in (Get-Content -LiteralPath $Source -Encoding UTF8)) {
    $text = $line.Trim()
    $nextList = if ($text -match '^\d+\.\s') { 'ol' } elseif ($text -match '^-\s') { 'ul' } else { '' }
    if ($list -and $nextList -ne $list) { $parts.Add("</$list>"); $list = '' }
    if ($table -and !$text.StartsWith('|')) { $parts.Add('</tbody></table></div>'); $table = $false }
    if (!$text) { continue }
    if ($text -match '^(#{1,6})\s+(.+)$') {
        $level = $Matches[1].Length
        $title = $Matches[2]
        $id = [regex]::Replace($title.ToLowerInvariant(), '[^a-z0-9\s-]', '')
        $id = [regex]::Replace($id.Trim(), '\s+', '-')
        $parts.Add("<h$level id='$id'>$(Format-Inline $title)</h$level>")
    } elseif ($nextList) {
        if (!$list) { $parts.Add("<$nextList>"); $list = $nextList }
        $value = [regex]::Replace($text, '^(\d+\.|-)\s+', '')
        $parts.Add("<li>$(Format-Inline $value)</li>")
    } elseif ($text.StartsWith('|')) {
        if ($text -match '^\|[\s:|-]+\|$') { continue }
        $tag = if ($table) { 'td' } else { 'th' }
        if (!$table) { $parts.Add('<div class="table-wrap"><table><tbody>'); $table = $true }
        $cells = $text.Trim('|').Split('|') | ForEach-Object { "<$tag>$(Format-Inline $_.Trim())</$tag>" }
        $parts.Add('<tr>' + ($cells -join '') + '</tr>')
    } else { $parts.Add("<p>$(Format-Inline $text)</p>") }
}
if ($list) { $parts.Add("</$list>") }
if ($table) { $parts.Add('</tbody></table></div>') }
$body = $parts -join "`n"
$html = @"
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Accounting &amp; Inventory User Manual</title>
<style>
body{font:16px/1.65 Arial,sans-serif;color:#172536;background:#edf2f6;margin:0}
main{max-width:900px;margin:30px auto;padding:40px;background:white;border-radius:8px}
h1{font-size:30px}h2{margin-top:40px;border-top:1px solid #ccd5df;padding-top:20px}h3{margin-top:28px}
a{color:#1558a1}li{margin:8px 0}table{border-collapse:collapse;width:100%;font-size:14px}
th,td{border:1px solid #ccd5df;padding:10px;text-align:left;vertical-align:top}th{background:#eef3f8}
.table-wrap{overflow-x:auto}code{background:#eef3f8;padding:2px 4px}button{padding:10px 16px;cursor:pointer}
@media(max-width:600px){main{margin:0;padding:20px;border-radius:0}}
@media print{@page{size:A4;margin:18mm}body{background:white;font-size:11pt}main{margin:0;padding:0;max-width:none}.print-tools{display:none}h2,h3{break-after:avoid}tr{break-inside:avoid}a{color:inherit;text-decoration:none}table{font-size:9pt}}
</style></head><body><main><div class="print-tools"><button onclick="window.print()">Print / Save PDF</button></div>
$body
</main></body></html>
"@
[System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($Destination), $html, [System.Text.UTF8Encoding]::new($false))
Write-Output "User manual exported to $Destination"
