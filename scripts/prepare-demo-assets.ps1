param(
    [string]$Root = "artifacts/demo"
)

$ErrorActionPreference = "Stop"

function Test-FileLocked {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return $false
    }

    try {
        $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $stream.Close()
        return $false
    } catch {
        return $true
    }
}

$rootPath = Join-Path (Get-Location) $Root
New-Item -ItemType Directory -Force -Path $rootPath | Out-Null

$csvPath = Join-Path $rootPath "customers.csv"
@"
name,region,score
Alice,Tokyo,98
Bob,Osaka,87
Carol,Fukuoka,91
"@ | Set-Content -Path $csvPath -Encoding UTF8

$jsonPath = Join-Path $rootPath "config.json"
@"
{
  "demo": true,
  "source": "Slasher demo",
  "browser": {
    "name": "Edge"
  }
}
"@ | Set-Content -Path $jsonPath -Encoding UTF8

$xlsxPath = Join-Path $rootPath "workbook.xlsx"
$createdWithExcel = $false
$keepExistingWorkbook = $false

try {
    $excelApp = New-Object -ComObject Excel.Application
    $excelApp.Visible = $false
    $excelApp.DisplayAlerts = $false
    $workbook = $excelApp.Workbooks.Add()
    $sheet = $workbook.Worksheets.Item(1)
    $sheet.Name = "Sheet1"

    $values = @(
        @("name", "status", "amount"),
        @("Alpha", "ready", "1200"),
        @("Beta", "review", "850"),
        @("Gamma", "done", "1630")
    )

    for ($row = 0; $row -lt $values.Count; $row++) {
        for ($col = 0; $col -lt $values[$row].Count; $col++) {
            $sheet.Cells.Item($row + 1, $col + 1).Value2 = [string]$values[$row][$col]
        }
    }

    $sheet.Columns.AutoFit() | Out-Null
    if (Test-Path $xlsxPath) {
        Remove-Item -LiteralPath $xlsxPath -Force
    }
    $workbook.SaveAs((Resolve-Path $rootPath).Path + "\workbook.xlsx", 51)
    $workbook.Close($false)
    $excelApp.Quit()
    $createdWithExcel = $true
} catch {
    Write-Warning "Excel COM workbook generation failed; falling back to minimal OpenXML workbook. $($_.Exception.Message)"
} finally {
    if ($null -ne $sheet) {
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($sheet)
    }
    if ($null -ne $workbook) {
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($workbook)
    }
    if ($null -ne $excelApp) {
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($excelApp)
    }
}

if (-not $createdWithExcel) {
if ((Test-Path $xlsxPath) -and (Test-FileLocked $xlsxPath)) {
    Write-Warning "Keeping existing workbook because it is currently open: $xlsxPath"
    $keepExistingWorkbook = $true
}
}

if (-not $createdWithExcel -and -not $keepExistingWorkbook) {
$temp = Join-Path $env:TEMP ("slasher-demo-xlsx-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $temp | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $temp "_rels") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $temp "xl/_rels"), (Join-Path $temp "xl/worksheets") | Out-Null

@"
<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
  <Default Extension="xml" ContentType="application/xml" />
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
  <Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml" />
</Types>
"@ | Set-Content -LiteralPath (Join-Path $temp "[Content_Types].xml") -Encoding UTF8

@"
<?xml version="1.0" encoding="utf-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" />
</Relationships>
"@ | Set-Content -LiteralPath (Join-Path $temp "_rels/.rels") -Encoding UTF8

@"
<?xml version="1.0" encoding="utf-8"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="Sheet1" sheetId="1" r:id="rId1" />
  </sheets>
</workbook>
"@ | Set-Content -Path (Join-Path $temp "xl/workbook.xml") -Encoding UTF8

@"
<?xml version="1.0" encoding="utf-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
</Relationships>
"@ | Set-Content -Path (Join-Path $temp "xl/_rels/workbook.xml.rels") -Encoding UTF8

@"
<?xml version="1.0" encoding="utf-8"?>
<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <si><t>name</t></si>
  <si><t>status</t></si>
  <si><t>amount</t></si>
  <si><t>Alpha</t></si>
  <si><t>ready</t></si>
  <si><t>Beta</t></si>
  <si><t>review</t></si>
  <si><t>Gamma</t></si>
  <si><t>done</t></si>
</sst>
"@ | Set-Content -Path (Join-Path $temp "xl/sharedStrings.xml") -Encoding UTF8

@"
<?xml version="1.0" encoding="utf-8"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <sheetData>
    <row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c><c r="C1" t="s"><v>2</v></c></row>
    <row r="2"><c r="A2" t="s"><v>3</v></c><c r="B2" t="s"><v>4</v></c><c r="C2"><v>1200</v></c></row>
    <row r="3"><c r="A3" t="s"><v>5</v></c><c r="B3" t="s"><v>6</v></c><c r="C3"><v>850</v></c></row>
    <row r="4"><c r="A4" t="s"><v>7</v></c><c r="B4" t="s"><v>8</v></c><c r="C4"><v>1630</v></c></row>
  </sheetData>
</worksheet>
"@ | Set-Content -Path (Join-Path $temp "xl/worksheets/sheet1.xml") -Encoding UTF8

if (Test-Path $xlsxPath) {
    Remove-Item -LiteralPath $xlsxPath -Force
}

$zipPath = Join-Path $rootPath "workbook.zip"
if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $temp "*") -DestinationPath $zipPath -Force
Move-Item -LiteralPath $zipPath -Destination $xlsxPath -Force
Remove-Item -LiteralPath $temp -Recurse -Force
}

Write-Host "Prepared demo assets:"
Write-Host "  $csvPath"
Write-Host "  $jsonPath"
Write-Host "  $xlsxPath"
