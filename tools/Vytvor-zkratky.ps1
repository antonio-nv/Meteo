# Vytvoří v nabídce Start dvě zkratky: Meteo (celá obrazovka) a Meteo - pozadí (živé pozadí).
# Použití (z PowerShellu, bez práv správce):
#   .\tools\Vytvor-zkratky.ps1 -Exe C:\DOS\Meteo\Meteo.exe
# Odstranění: .\tools\Vytvor-zkratky.ps1 -Odstranit
param(
    [string]$Exe = "C:\DOS\Meteo\Meteo.exe",
    [switch]$Odstranit
)

$slozka = Join-Path ([Environment]::GetFolderPath("Programs")) "Meteo"

if ($Odstranit) {
    if (Test-Path $slozka) { Remove-Item $slozka -Recurse -Force }
    Write-Host "Zkratky odstraněny."
    return
}

if (-not (Test-Path $Exe)) { throw "Soubor $Exe neexistuje. Zadej správnou cestu parametrem -Exe." }
$Exe = (Resolve-Path $Exe).Path
New-Item -ItemType Directory -Force -Path $slozka | Out-Null

# Ikony zkratek (černá = celá obrazovka, modrá = pozadí) se zkopírují vedle Meteo.exe, odkud je zkratky berou.
$zdroj = Join-Path $PSScriptRoot "ikony"
foreach ($ikona in "fullscreen.ico", "pozadi.ico") {
    $z = Join-Path $zdroj $ikona
    if (Test-Path $z) { Copy-Item $z (Join-Path (Split-Path $Exe) $ikona) -Force }
}

$shell = New-Object -ComObject WScript.Shell
function Zkratka($nazev, $parametry, $popis, $ikona) {
    $lnk = $shell.CreateShortcut((Join-Path $slozka "$nazev.lnk"))
    $lnk.TargetPath = $Exe
    $lnk.Arguments = $parametry
    $lnk.WorkingDirectory = Split-Path $Exe
    $soubor = Join-Path (Split-Path $Exe) $ikona
    $lnk.IconLocation = if (Test-Path $soubor) { "$soubor,0" } else { "$Exe,0" }
    $lnk.Description = $popis
    $lnk.Save()
}

Zkratka "Meteo" "" "Předpověď počasí na celou obrazovku (Esc = konec)" "fullscreen.ico"
Zkratka "Meteo - pozadí" "/d" "Předpověď počasí jako živé pozadí plochy (Win+D = konec)" "pozadi.ico"
Write-Host "Hotovo: $slozka"
