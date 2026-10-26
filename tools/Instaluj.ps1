# Meteo: instalace, aktualizace a odinstalace jedním skriptem. Spouští se v příkazovém řádku jako správce:
#   powershell -ExecutionPolicy Bypass -File "C:\Program Files\Meteo\tools\Instaluj.ps1"
#   powershell -ExecutionPolicy Bypass -File "C:\Program Files\Meteo\tools\Instaluj.ps1" -Odinstalovat
#
# První spuštění (zdroje ještě nejsou) stáhne sám i zdroje z GitHubu: stačí tento skript uložit kamkoli a spustit.
# Instalace: stáhne novou verzi (git pull, je-li složka z gitu), zkompiluje ji do C:\Program Files\Meteo\Program
# a vytvoří v nabídce Start složku Meteo se dvěma zkratkami (celá obrazovka a živé pozadí).
#Requires -RunAsAdministrator
param([switch]$Odinstalovat)

$ErrorActionPreference = "Stop"
$koren   = "C:\Program Files\Meteo"
$program = Join-Path $koren "Program"
$exe     = Join-Path $program "Meteo.exe"
$start   = Join-Path ([Environment]::GetFolderPath("CommonPrograms")) "Meteo"   # nabídka Start pro všechny uživatele
$data    = Join-Path $env:LOCALAPPDATA "Meteo"

# Běžící Meteo drží Meteo.exe zamčený, takže se musí ukončit.
Get-Process -Name "Meteo" -ErrorAction SilentlyContinue | Stop-Process -Force

if ($Odinstalovat) {
    foreach ($cesta in $program, $start, $data) {
        if (Test-Path $cesta) { Remove-Item $cesta -Recurse -Force }
    }
    Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "Meteo" -ErrorAction SilentlyContinue
    Write-Host "Odinstalováno: program, zkratky, data a spouštění po přihlášení."
    Write-Host "Zbývá smazat složku $koren (zdroje a tento skript) ručně v Průzkumníku."
    return
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "Chybí .NET SDK. Stáhni ho na https://dotnet.microsoft.com/download (nebo příkazem: winget install Microsoft.DotNet.SDK.10) a spusť skript znovu v novém okně." }

# První spuštění: zdroje ještě nejsou, stáhnou se z GitHubu (git clone vyžaduje nainstalovaný Git).
if (-not (Test-Path (Join-Path $koren "src\Meteo.Windows"))) {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw "Chybí Git. Stáhni ho na https://git-scm.com/download/win (nebo příkazem: winget install Git.Git) a spusť skript znovu v novém okně." }
    Write-Host "Stahuji zdroje do $koren ..."
    & git clone https://github.com/antonio-nv/Meteo.git $koren
    if ($LASTEXITCODE -ne 0) { throw "git clone se nepovedl." }
}

if ((Test-Path (Join-Path $koren ".git")) -and (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "Stahuji novou verzi (git pull)..."
    & git -C $koren pull
    if ($LASTEXITCODE -ne 0) { Write-Warning "git pull se nepovedl, pokračuji s tím, co ve složce je." }
}

Write-Host "Kompiluji do $program ..."
& dotnet publish (Join-Path $koren "src\Meteo.Windows") -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $program
if ($LASTEXITCODE -ne 0) { throw "Kompilace se nepovedla (viz výpis výše)." }

# Ikony zkratek leží vedle Meteo.exe, odtud je zkratky berou.
Copy-Item (Join-Path $koren "tools\ikony\*.ico") $program -Force

New-Item -ItemType Directory -Force -Path $start | Out-Null
$shell = New-Object -ComObject WScript.Shell
function Zkratka($nazev, $parametry, $popis, $ikona) {
    $lnk = $shell.CreateShortcut((Join-Path $start "$nazev.lnk"))
    $lnk.TargetPath = $exe
    $lnk.Arguments = $parametry
    $lnk.WorkingDirectory = $program
    $lnk.IconLocation = (Join-Path $program $ikona) + ",0"
    $lnk.Description = $popis
    $lnk.Save()
}
Zkratka "Meteo" "" "Předpověď počasí na celou obrazovku (Esc = konec)" "fullscreen.ico"
Zkratka "Meteo - pozadí" "/d" "Předpověď počasí jako živé pozadí plochy (Win+D = konec)" "pozadi.ico"

Write-Host ""
Write-Host "Hotovo. Program: $exe"
Write-Host "Zkratky najdeš v nabídce Start ve složce Meteo (pravým tlačítkem je můžeš připnout na Start)."
