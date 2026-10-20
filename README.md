# Meteo

Šetřič obrazovky pro Windows 11 (C#, .NET 8, WPF). Zobrazuje 72hodinový meteogram z modelu ALADIN (ČHMÚ), údaje „nyní“,
denní přehled a (pokud existuje) meteogram Flymet. Místa se po nastavené době samy střídají.

Místa se zadávají **zkratkou**: ICAO kód letiště (`LKPR`, `LKTB`, `LKPI`…), názvem obce, nebo souřadnicemi.
Souřadnice se nikde nezadávají ručně.

Licence: [MIT](LICENSE).

## Struktura řešení

| Projekt | Cíl | Obsah |
|---|---|---|
| `Meteo.Core` | `net8.0` | Bez závislosti na OS a UI: stažení dat (Open-Meteo / ALADIN), databáze letišť, geokódování, cache, nastavení, Flymet, `IScreenProvider`. |
| `Meteo.Windows` | `net8.0-windows` | Jen Win32: výčet monitorů v pixelech, umístění okna, parametry `/s /c /p` pro `.scr`, umístění dat. |
| `Meteo.Wpf` | `net8.0-windows` | WPF: kreslení meteogramu, okno šetřiče, nastavení. Bez XAML. Sestaví se jako `Meteo.exe` / `Meteo.scr`. |

Pro Linux Mint přibude `Meteo.Linux` (vlastní `IScreenProvider`, umístění dat, spuštění) a UI projekt (např. Avalonia).
Kreslení meteogramu je zatím v `Meteo.Wpf/Dashboard.cs`; až se začne dělat Linux, vyplatí se rozvržení a výpočty vytáhnout do jádra.

## Místa

- **Výchozí střídání:** `LKLT`, `LKPR`, `LKKO`, `LKSU`, `LKTB`, `LKJI`, `LKPI`.
- **Letiště:** vestavěná databáze (917 letišť: celá ČR bez heliportů, plus SK, AT, DE, PL s kódem ICAO). Hledá se bez internetu,
  podle kódu, názvu nebo obce, bez ohledu na diakritiku (`pribyslav`, `LKK`, `vaclav havel`, `wien`).
- **Obce:** vyhledání přes internet (Open-Meteo Geocoding, ČR, při málo výsledcích i SK, AT, DE, PL). Souřadnice se uloží do nastavení.
- **Souřadnice:** `49.58, 15.76` (desetinná tečka).
- Nastavení se otevře parametrem `/c` (nebo dvojklikem na `Meteo.exe` bez parametrů). Pořadí míst lze měnit.

## Režimy

| Spuštění | Co se stane |
|---|---|
| `Meteo.scr /s` (Windows po nečinnosti) | **Šetřič:** okno na každém monitoru, nad ostatními okny, zavře se klávesou, kliknutím nebo pohybem myši. Poloha a velikost oken jsou zamčené. |
| `Meteo.exe` nebo `Meteo.exe /f` | **Samostatné okno** přes jeden monitor. Není nad ostatními okny, je na hlavním panelu. |
| `Meteo.exe /f:2` | totéž na 2. monitoru zleva (jinak naposledy použitý, jinak hlavní) |
| `Meteo.exe /c` (nebo `.scr` bez parametru) | Nastavení |
| `Meteo.exe /w 1080x1920` | Ladicí okno běžné velikosti (Esc zavře) |

**Samostatné okno:** nejde měnit velikost, maximalizovat, přesouvat ani připnout (Win+šipky, Aero Snap, tažení nic nezmění;
hlídá to `FullscreenGuard`). Minimalizace (kliknutím na tlačítko na hlavním panelu) zůstává, aby šlo okno schovat.
Klávesy: **Win+Shift+Vlevo / Vpravo** přesune okno na sousední monitor (zleva doprava, dokola; poslední monitor se pamatuje),
**Vlevo / Vpravo** = předchozí / další místo, **F2** = nastavení, **Esc** nebo Alt+F4 = konec.
Win+šipky se zachytávají nízkoúrovňovým hákem klávesnice (`ShellKeyHook`) a jen dokud je v popředí okno Meteo;
ve šetřiči hák není.

## Sestavení

- Visual Studio 2022 (17.8+), .NET 8 SDK, úloha „.NET desktop development“. Spouštěcí projekt: **`Meteo.Wpf`**.
- Ladění: `src/Meteo.Wpf/Properties/launchSettings.json` obsahuje profily „Celá obrazovka“, „Šetřič (/s)“, „Okno 1080x1920“,
  „Okno 1200x1920“, „Okno 1366x768“ a „Nastavení“. Vybírají se v nabídce vedle tlačítka Start.

## Instalace jako šetřič

```
dotnet publish src/Meteo.Wpf -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Ve složce `publish` vznikne `Meteo.exe` a jeho kopie `Meteo.scr`. Pravým tlačítkem na `.scr` → **Nainstalovat** (šetřič).
`Meteo.exe` si lze nechat i vedle dat, např. v `C:\DOS\Meteo\`, a spouštět ručně jako samostatné okno.
Single-file je důležitý: Windows při instalaci kopíruje do `System32` jen samotný `.scr`.
Je potřeba nainstalované **.NET 8 Desktop Runtime**.

## Kde jsou data a nastavení

Pořadí hledání (první zapisovatelné vyhrává):

1. proměnná prostředí `METEO_HOME`,
2. složka `C:\DOS\Meteo`, **pokud existuje** (stačí ji vytvořit),
3. `%LOCALAPPDATA%\Meteo`.

Uvnitř: `settings.json`, `cache\` (předpovědi), `flymet\` (obrázky), `fonts\` (volitelně `.ttf`), `error.log`.
`C:\Program Files` se nepoužívá: zápis tam vyžaduje práva správce a konfigurace šetřiče by tiše skončila v přesměrované
složce virtualizace.

## Font

Preferuje se **PxPlus IBM VGA 8x16** (VileR, *The Ultimate Oldschool PC Font Pack*, licence CC BY-SA 4.0; do repozitáře
není přibalen). Hledá se v tomto pořadí:

1. nainstalovaný font `PxPlus IBM VGA8`, `PxPlus IBM VGA 8x16`, `Px437 IBM VGA 8x16` (nebo jméno z nastavení),
2. soubor `.ttf` ve složce `fonts` v datovém adresáři nebo vedle programu,
3. záložní neproporcionální: Consolas → Lucida Console → Courier New.

Text je vykreslován bez vyhlazování (`TextRenderingMode.Aliased`, `EdgeMode.Aliased`), nezávisle na ClearType.
Rozvržení je v buňkách 8×16 px. Pokud font nemá českou diakritiku, WPF ji dokreslí ze záložního fontu; kdyby to vadilo,
zapněte v nastavení „Pouze ASCII“.

## Data a licence třetích stran

- Předpověď: model ALADIN ČHMÚ přes **Open-Meteo** (`chmi_aladin_seamless`, 1 km pro ČR, 3 dny, aktualizace po 6 hodinách).
  Bezplatné pro nekomerční použití, bez klíče; data pod licencí CC BY 4.0, zdroj je uveden v patičce.
- Letiště: **OurAirports** (<https://ourairports.com/data/>), volné dílo (public domain). Názvy obcí jsou upraveny do místních tvarů.
- Flymet: `https://flymet.meteopress.cz/meteogram1/<NÁZEV>.png`. Název se odvodí z obce místa (velká písmena bez diakritiky
  a mezer, např. `PRIBYSLAV`); pokud obrázek neexistuje, ukáže se jen informace a další pokus je za 24 hodin. Ruční přepsání:
  `FlymetSlugs` v `settings.json`, např. `{ "LKPI": "PRIBYSLAV" }`. Obrázky jsou majetek Meteopress, jen pro osobní použití
  a v repozitáři nejsou.

## Co bylo ověřeno

Jádro a Windows vrstva se zkompilovaly a otestovaly na syntetických datech (parsování odpovědi Open-Meteo, databáze letišť
a hledání, nastavení, parametry příkazové řádky). WPF projekt nebyl v prostředí, kde vznikl, zkompilován ani spuštěn
a skutečné servery Open-Meteo a Flymet nebyly dostupné.
