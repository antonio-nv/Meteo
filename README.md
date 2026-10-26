# Meteo

Zobrazovač počasí pro Windows 11 (C#, .NET 10, WPF), buď jako samostatné okno přes celý monitor, nebo jako živé pozadí
plochy. Zobrazuje 72hodinový meteogram z modelu ALADIN (ČHMÚ), údaje „nyní“,
denní přehled a (pokud existuje) meteogram Flymet. Místa se po nastavené době samy střídají.

Místa se zadávají **zkratkou**: ICAO kód letiště (`LKPR`, `LKTB`, `LKPI`…), názvem vlastního místa, nebo souřadnicemi.
Nastavení není v souboru ani v dialogu: mění se přímo v kódu, ve třídě `AppSettings` (`src/Meteo.Core/Settings.cs`).

Licence: [MIT](LICENSE).

## Struktura řešení

| Projekt | Cíl | Obsah |
|---|---|---|
| `Meteo.Core` | `net10.0` | Bez závislosti na OS a UI: stažení dat (Open-Meteo / ALADIN), databáze letišť, cache, nastavení (`AppSettings`), Flymet, `IScreenProvider`. |
| `Meteo.Windows` | `net10.0-windows` | Celá aplikace pro Windows 11: WPF (kreslení meteogramu, samostatné okno, živé pozadí, ikona vedle hodin; bez XAML) a ve složce `Win32` vše, co se týká Windows API (monitory v pixelech, zamčení okna, zachytávání Win+šipek, parametry příkazové řádky, umístění dat). Sestaví se jako `Meteo.exe`. |

Pro Linux Mint přibude **samostatný projekt `Meteo.Linux`** (vlastní `IScreenProvider`, okno, umístění dat; překlad z příkazové řádky
pomocí `dotnet`), bez společného UI frameworku (Avalonia se na podobné úloze neosvědčila: ve fullscreenu na Windows zůstává
nechtěné okno). Společné je jen jádro `Meteo.Core`. Kreslení meteogramu je zatím v `Meteo.Windows/Dashboard.cs`; až se začne
dělat Linux, vyplatí se rozvržení a výpočty vytáhnout do jádra za malé rozhraní „plátno“, aby se nepsaly dvakrát.

## Místa

- **Výchozí střídání:** `LKLT`, `LKPR`, `LKKO`, `LKSU`, `LKTB`, `LKJI`, `LKPI`.
- **Letiště:** vestavěná databáze (917 letišť: celá ČR bez heliportů, plus SK, AT, DE, PL s kódem ICAO). Hledá se bez internetu,
  podle kódu, názvu nebo obce, bez ohledu na diakritiku (`pribyslav`, `LKK`, `ruzyne`, `wien`).
- **Vlastní místa:** do `AppSettings.CustomPlaces` se přidá `new Place("", "Název", šířka, délka, "Obec pro Flymet")` a název se
  vloží do `AppSettings.Places`. Příklad je v kódu (Nové Veselí).
- **Souřadnice:** přímo v `Places`, `"49.58, 15.76"` (desetinná tečka).
- Pořadí a výběr míst, doba střídání (`RotateSeconds`), obnovení předpovědi (`RefreshMinutes`), radar, Flymet, font a další volby jsou
  ve stejné třídě. Po změně stačí program znovu sestavit.

## Co se zobrazuje

- **Meteogram (72 h):** teplota (s rosným bodem, denní maxima a minima), **oblačnost – nízká, střední a vysoká zvlášť**,
  srážky (sníh bíle), vítr (rychlost, nárazy, šipky směru), tlak, pruh den/noc a celková oblačnost. Panel oblačnosti se
  zobrazí, jen když je na obrazovce dost řádků (na 1366×768 je).
- **Údaje „nyní“** a denní přehled na 3 dny.
- **Srážkový radar ČHMÚ** (animace): posledních `RadarHours` hodin (výchozí 2, snímky po 10 minutách) a volitelně extrapolace
  ČHMÚ na dalších 60 minut. Na mapě je státní hranice, hranice krajů, orientační města a žlutý křížek zobrazeného místa;
  pod mapou je čas snímku a ukazatel průběhu (cyan = extrapolace). Radar neukazuje déšť „na zítřek“ – jen aktuální stav
  a nejbližší hodinu; zítřejší srážky jsou v panelu Srážky v meteogramu.
- **Flymet**, pokud pro místo existuje obrázek. Na šířku se radar a Flymet střídají (30 s / 15 s), když se pod sebe nevejdou;
  na výšku jsou vedle sebe.

## Režimy

| Spuštění | Co se stane |
|---|---|
| `Meteo.exe` nebo `Meteo.exe /f` | **Samostatné okno** přes jeden monitor. Není nad ostatními okny, je na hlavním panelu. Ukončí se klávesou **Esc**. |
| `Meteo.exe /f:2` | totéž na 2. monitoru zleva (jinak `AppSettings.Monitor`, jinak hlavní monitor) |
| `Meteo.exe /d` | **Živé pozadí plochy** na všech monitorech (viz níže). Ukončí se klávesou **Win+D**. |
| `Meteo.exe /w 1080x1920` | Ladicí okno běžné velikosti (Esc zavře) |

Neznámý nebo starý parametr (např. `/s`, `/c`) otevře samostatné okno. Šetřič obrazovky (`.scr`) byl zrušen.

**Samostatné okno:** nejde měnit velikost, maximalizovat, přesouvat ani připnout (Win+šipky, Aero Snap, tažení nic nezmění;
hlídá to `FullscreenGuard`). Minimalizace (kliknutím na tlačítko na hlavním panelu) zůstává, aby šlo okno schovat.
Klávesy: **Win+Shift+Vlevo / Vpravo** přesune okno na sousední monitor (zleva doprava, dokola),
**Vlevo / Vpravo** = předchozí / další místo, **Esc** nebo Alt+F4 = konec.
Win+šipky se zachytávají nízkoúrovňovým hákem klávesnice (`ShellKeyHook`) a jen dokud je v popředí okno Meteo.

**Pravý dolní roh** obou režimů ukazuje, co to je a jak se ukončí („Meteo · Esc = konec“, „Meteo · Win+D = konec“).
**Ikona vedle hodin** ukazuje, že Meteo běží, a slouží k diagnostice: Informace o Meteo (režim, místo, stav, čas sestavení,
složka s daty), Otevřít složku s daty a logem, Další / Předchozí místo, Obnovit data, Ukončit (s potvrzením).

## Živé pozadí plochy (`/d`)

`Meteo.exe /d` otevře na každém monitoru okno přes celou obrazovku, které leží **pod všemi ostatními okny**, nemá tlačítko na
hlavním panelu a není v Alt+Tab. Chová se tedy jako pozadí plochy: ostatní programy ho překrývají. Ikony plochy jsou pod ním
skryté. Běží, dokud se neukončí, nebo dokud se nezhasne počítač. Spustí se jen jednou (druhé spuštění se tiše ukončí).

- **Win+D** (zobrazit plochu), tlačítko „Zobrazit plochu“ na konci hlavního panelu i Win+M živé pozadí **ukončí**, takže zůstane
  obyčejná plocha s ikonami. (Win+Ctrl+D, nová virtuální plocha, ho neukončí.)
- **Ikona vedle hodin** (pravé nebo levé tlačítko): jako výše, a navíc **Spouštět s Windows**
  (zapíše `Meteo.exe /d` do `HKCU\...\Run`, bez práv správce).
- **Klávesy:** `Ctrl+Alt+Vlevo / Vpravo` kdekoli, a samotné `Vlevo / Vpravo`, když je v popředí plocha. V ostatních aplikacích se
  šipky nikdy nezachytávají.
- Při změně monitorů nebo restartu Průzkumníka se okna po chvíli vytvoří znovu.

**Proč pod okny a ne zavěšené do plochy.** Zavěšení okna do vrstvy plochy (starý trik s `WorkerW`) se na Windows 11 24H2
(sestavení 10.0.26300) nezobrazilo: okno bylo v seznamu oken, správně umístěné a viditelné, ale Windows ho nevykreslily.
Zkoušené varianty (potomek `WorkerW` v `Progman`, mezi ikonami a tapetou, nad ikonami, i se softwarovým kreslením)
se nezobrazily; nezkoušelo se jen `progman` a `legacy`. Funguje obyčejné okno posunuté na dno pořadí oken. Ostatní způsoby jsou k dispozici pro ladění:
`Meteo.exe /d:auto` (zkusí zavěšení do plochy), nebo vynuceně `/d:legacy`, `/d:top`, `/d:between`, `/d:workerw`,
`/d:progman`. Další volby, oddělené čárkou: `sw` (softwarové kreslení), `diag` (zapíše do `error.log` strom oken plochy),
např. `/d:auto,diag`.

## Sestavení

- Visual Studio s podporou .NET 10 (Visual Studio 2026), .NET 10 SDK, úloha „.NET desktop development“. Spouštěcí projekt: **`Meteo.Windows`**.
- Ladění: `src/Meteo.Windows/Properties/launchSettings.json` obsahuje profily „Celá obrazovka“, „Živé pozadí (/d)“, „Okno 1080x1920“,
  „Okno 1200x1920“ a „Okno 1366x768“. Vybírají se v nabídce vedle tlačítka Start.

## Rychlý start ze zdrojů

Potřebuješ **.NET 10 SDK** (je i ve Visual Studiu), Windows 11 a nic dalšího. Žádný instalátor ani podpis není, program se zkompiluje
na tvém počítači, takže Windows nehlásí SmartScreen.

```
git clone https://github.com/antonio-nv/Meteo.git
cd Meteo
dotnet run --project src/Meteo.Windows -c Release            (celá obrazovka)
dotnet run --project src/Meteo.Windows -c Release -- /d      (živé pozadí)
```

Ve Visual Studiu stačí otevřít `Meteo.sln` a spustit (F5).

## Instalace

`dotnet run` nic neinstaluje: zkompiluje program do složky `bin` a `obj` uvnitř naklonovaného repozitáře a odtud ho spustí.
Při dalším spuštění se kompiluje znovu jen tehdy, když se změnily zdrojové soubory.

Pro trvalé umístění do `C:\Program Files\Meteo` spusť **příkazový řádek jako správce** (Win → `cmd` → Spustit jako správce) a v kořeni repozitáře:

```
dotnet publish src\Meteo.Windows -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "C:\Program Files\Meteo"
```

Vznikne jeden samostatný `Meteo.exe` (`Meteo.exe` = celá obrazovka, `Meteo.exe /d` = živé pozadí). Je potřeba **.NET 10 Desktop Runtime**
(v SDK je). Kompilace se opakuje jen při změně zdrojů. „Spouštět s Windows“ zapni v nabídce u ikony vedle hodin až z tohoto umístění, ne z `bin`.

Zkratky v nabídce Start (**Meteo** = celá obrazovka, **Meteo - pozadí** = živé pozadí) vytvoří skript (spuštěný jako správce, protože ikony se kopírují do `Program Files`):

```
powershell -ExecutionPolicy Bypass -File tools\Vytvor-zkratky.ps1 -Exe "C:\Program Files\Meteo\Meteo.exe"
```

Skript vedle `Meteo.exe` zkopíruje i dvě ikony zkratek (`tools/ikony`). Odstraní je `-Odstranit`. Ikona (`src/Meteo.Windows/meteo.ico`, čtyři pidi-obrázky 2×2) je součástí `Meteo.exe`.

## Kde jsou data

Do `Program Files` se nezapisuje. Cache a log jsou ve složce (první zapisovatelná vyhrává):

1. proměnná prostředí `METEO_HOME`,
2. `%LOCALAPPDATA%\Meteo`.

Uvnitř: `cache\` (předpovědi), `radar\`, `flymet\` (obrázky), `fonts\` (volitelně `.ttf`), `error.log`. Nastavení tam není,
to je v kódu. Odinstalace = smazat `C:\Program Files\Meteo`, `%LOCALAPPDATA%\Meteo`, zkratky ve Startu a případně položku „Meteo“
v registru `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (zapíná se v nabídce u ikony vedle hodin).

## Font

Preferuje se **PxPlus IBM VGA 8x16** (VileR, *The Ultimate Oldschool PC Font Pack*, licence CC BY-SA 4.0; do repozitáře
není přibalen). Hledá se v tomto pořadí:

1. nainstalovaný font `PxPlus IBM VGA8`, `PxPlus IBM VGA 8x16`, `Px437 IBM VGA 8x16` (nebo jméno z `AppSettings.FontFamily`),
2. soubor `.ttf` ve složce `fonts` v datovém adresáři nebo vedle programu,
3. záložní neproporcionální: Consolas → Lucida Console → Courier New.

Text je vykreslován bez vyhlazování (`TextRenderingMode.Aliased`, `EdgeMode.Aliased`), nezávisle na ClearType.
Rozvržení je v buňkách 8×16 px. Pokud font nemá českou diakritiku, WPF ji dokreslí ze záložního fontu; kdyby to vadilo,
nastavte v `AppSettings` `AsciiOnly = true`.

## Data a licence třetích stran

- Předpověď: model ALADIN ČHMÚ přes **Open-Meteo** (`chmi_aladin_seamless`, 1 km pro ČR, 3 dny, aktualizace po 6 hodinách).
  Bezplatné pro nekomerční použití, bez klíče; data pod licencí CC BY 4.0, zdroj je uveden v patičce.
- Letiště: **OurAirports** (<https://ourairports.com/data/>), volné dílo (public domain). Názvy obcí jsou upraveny do místních tvarů.
- Radar: **ČHMÚ, otevřená data** (<https://opendata.chmi.cz/meteorology/weather/radar/>): `composite/maxz/png/` (maximální
  odrazivost, snímek po 5 minutách) a `composite/fct_maxz/png/` (extrapolace +10 až +60 min). Snímky jsou v projekci
  EPSG:3857 a pokrývají východní délku 11,267–20,770° a severní šířku 48,047–52,167° (`Meteo.Core/RadarGeo.cs`); zobrazuje se
  výřez, kam až sahají data. Podklad (státní a krajské hranice) je z **Natural Earth** (volné dílo, `Data/map.txt`).
- Slunce a Měsíc (azimut, výška, fáze, východy a západy Měsíce): **počítá se v programu** (`Meteo.Core/Astronomy.cs`), nic se
  nestahuje. Slunce podle vzorců NOAA, Měsíc zkrácenou řadou podle Meeuse (Astronomical Algorithms) s opravou na paralaxu.
  Polohy se shodují s knihovnou PyEphem na asi 0,02°, časy východů a západů do 1–2 minut. Open data ČHMÚ astronomické údaje nenabízejí.
- Flymet: `https://flymet.meteopress.cz/meteogram1/<NÁZEV>.png`. Název se odvodí z obce místa (velká písmena bez diakritiky
  a mezer, např. `PRIBYSLAV`); pokud obrázek neexistuje, další pokus je za 24 hodin. Ruční přepsání:
  `AppSettings.FlymetSlugs`, např. `["LKSU"] = "SUMPERK"` (letiště LKSU leží v Novém Malíně, meteogram je pro Šumperk).
  Pokud pro místo obrázek není, zůstane zobrazen předchozí meteogram (bílý obrázek by při přepínání blikal). Obrázky jsou majetek Meteopress, jen pro osobní použití
  a v repozitáři nejsou.

## Co bylo ověřeno

Jádro a Windows vrstva se zkompilovaly a otestovaly na syntetických datech (parsování odpovědi Open-Meteo včetně vrstev
oblačnosti, databáze letišť a hledání, výchozí nastavení, parametry příkazové řádky, georeference a mapový podklad radaru,
stahování a cache radarových snímků proti falešnému serveru, zamykání okna). Zdrojový kód WPF části prošel jen syntaktickou
kontrolou. WPF projekt nebyl v prostředí, kde vznikl, zkompilován ani spuštěn
a skutečné servery Open-Meteo, Flymet a ČHMÚ nebyly dostupné. Názvy radarových souborů a rozsah snímku vycházejí z popisu
ČHMÚ (`radar_description_en.pdf`) a nebyly ověřeny na skutečném snímku. Kdyby značka místa na radaru neseděla, upraví se
konstanty v `RadarGeo.cs` (rozsah snímku) a `Dashboard.cs` (`RadarViewLonMax`, `RadarViewLatMax`).
