# Meteo

Šetřič obrazovky pro Windows 11 (C#, .NET 10, WPF). Zobrazuje 72hodinový meteogram z modelu ALADIN (ČHMÚ), údaje „nyní“,
denní přehled a (pokud existuje) meteogram Flymet. Místa se po nastavené době samy střídají.

Místa se zadávají **zkratkou**: ICAO kód letiště (`LKPR`, `LKTB`, `LKPI`…), názvem obce, nebo souřadnicemi.
Souřadnice se nikde nezadávají ručně.

Licence: [MIT](LICENSE).

## Struktura řešení

| Projekt | Cíl | Obsah |
|---|---|---|
| `Meteo.Core` | `net10.0` | Bez závislosti na OS a UI: stažení dat (Open-Meteo / ALADIN), databáze letišť, geokódování, cache, nastavení, Flymet, `IScreenProvider`. |
| `Meteo.Windows` | `net10.0-windows` | Celá aplikace pro Windows 11: WPF (kreslení meteogramu, okno šetřiče, nastavení; bez XAML) a ve složce `Win32` vše, co se týká Windows API (monitory v pixelech, zamčení okna, zachytávání Win+šipek, parametry `/s /c /p` pro `.scr`, umístění dat). Sestaví se jako `Meteo.exe` / `Meteo.scr`. |

Pro Linux Mint přibude **samostatný projekt `Meteo.Linux`** (vlastní `IScreenProvider`, okno, umístění dat; překlad z příkazové řádky
pomocí `dotnet`), bez společného UI frameworku (Avalonia se na podobné úloze neosvědčila: ve fullscreenu na Windows zůstává
nechtěné okno). Společné je jen jádro `Meteo.Core`. Kreslení meteogramu je zatím v `Meteo.Windows/Dashboard.cs`; až se začne
dělat Linux, vyplatí se rozvržení a výpočty vytáhnout do jádra za malé rozhraní „plátno“, aby se nepsaly dvakrát.

## Místa

- **Výchozí střídání:** `LKLT`, `LKPR`, `LKKO`, `LKSU`, `LKTB`, `LKJI`, `LKPI`.
- **Letiště:** vestavěná databáze (917 letišť: celá ČR bez heliportů, plus SK, AT, DE, PL s kódem ICAO). Hledá se bez internetu,
  podle kódu, názvu nebo obce, bez ohledu na diakritiku (`pribyslav`, `LKK`, `ruzyne`, `wien`).
- **Obce:** vyhledání přes internet (Open-Meteo Geocoding, ČR, při málo výsledcích i SK, AT, DE, PL). Souřadnice se uloží do nastavení.
- **Souřadnice:** `49.58, 15.76` (desetinná tečka).
- Nastavení se otevře parametrem `/c` (v samostatném okně klávesou F2; `.scr` bez parametru otevře také nastavení). Pořadí míst lze měnit.

## Co se zobrazuje

- **Meteogram (72 h):** teplota (s pocitovou, denní maxima a minima), **oblačnost – nízká, střední a vysoká zvlášť**,
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
| `Meteo.scr /s` (Windows po nečinnosti) | **Šetřič:** okno na každém monitoru, nad ostatními okny, zavře se klávesou, kliknutím nebo pohybem myši. Poloha a velikost oken jsou zamčené. |
| `Meteo.exe` nebo `Meteo.exe /f` | **Samostatné okno** přes jeden monitor. Není nad ostatními okny, je na hlavním panelu. |
| `Meteo.exe /f:2` | totéž na 2. monitoru zleva (jinak naposledy použitý, jinak hlavní) |
| `Meteo.exe /c` (nebo `.scr` bez parametru) | Nastavení |
| `Meteo.exe /d` | **Živé pozadí plochy** na všech monitorech (viz níže). |
| `Meteo.exe /w 1080x1920` | Ladicí okno běžné velikosti (Esc zavře) |

**Samostatné okno:** nejde měnit velikost, maximalizovat, přesouvat ani připnout (Win+šipky, Aero Snap, tažení nic nezmění;
hlídá to `FullscreenGuard`). Minimalizace (kliknutím na tlačítko na hlavním panelu) zůstává, aby šlo okno schovat.
Klávesy: **Win+Shift+Vlevo / Vpravo** přesune okno na sousední monitor (zleva doprava, dokola; poslední monitor se pamatuje),
**Vlevo / Vpravo** = předchozí / další místo, **F2** = nastavení, **Esc** nebo Alt+F4 = konec.
Win+šipky se zachytávají nízkoúrovňovým hákem klávesnice (`ShellKeyHook`) a jen dokud je v popředí okno Meteo;
ve šetřiči hák není.

## Živé pozadí plochy (`/d`)

`Meteo.exe /d` vloží okno Meteo na každém monitoru do plochy Windows (ve starších Windows za ikony, ve Windows 11 24H2 a novějších nad ně). Nemá tlačítko na hlavním
panelu, není v Alt+Tab a ostatní okna ho překrývají jako běžné pozadí. Běží, dokud se neukončí, nebo dokud se nezhasne
počítač; po pádu programu se Windows vrátí k původní tapetě. Spustí se jen jednou (druhé spuštění se tiše ukončí).

Okno za ikonami nedostává klávesy ani kliknutí, proto se ovládá takto:

- **Ikona vedle hodin** (pravé nebo levé tlačítko): Další místo, Předchozí místo, Obnovit data, Nastavení, Spouštět s Windows
  (zapíše `Meteo.exe /d` do `HKCU\...\Run`, bez práv správce), **Ukončit Meteo…** s potvrzením.
- **Klávesy:** `Ctrl+Alt+Vlevo / Vpravo` kdekoli, a samotné `Vlevo / Vpravo`, když je v popředí plocha (kliknutí na prázdné
  místo, `Win+D`). V ostatních aplikacích se šipky nikdy nezachytávají.
- `Win+D` pozadí nezavře, plocha jen ukáže ikony nad ním.
- Při změně monitorů nebo restartu Průzkumníka se okna po chvíli vytvoří znovu.

Vložení do plochy není oficiální rozhraní Windows: od verze 24H2 se uspořádání oken plochy změnilo, proto program zkouší víc
způsobů a výsledek zapíše do `error.log` (např. `zavěšeno (ProgmanChildWorkerW) na \\.\DISPLAY1 1200x1920`). Kdyby se nic nezavěsilo,
okno zůstane obyčejné, pod všemi ostatními (bez lišty a Alt+Tab). Na ladění lze způsob vynutit: `Meteo.exe /d:legacy`,
`/d:top`, `/d:between`, `/d:workerw`, `/d:progman` nebo `/d:bottom`.
V režimu `/d` se kreslí softwarově (WPF `SoftwareOnly`), protože okno zavěšené do plochy se při kreslení grafickou kartou nezobrazilo; `/d:hw` (případně `/d:top,hw`) vrátí kreslení grafickou kartou.
Ve Windows 11 24H2 a novějších se okno vkládá **nad ikony plochy** (`ProgmanTop`) a ikony tak nejsou vidět; zavěšení pod ikony (`between`) se na sestavení 10.0.26300 nezobrazilo.

## Sestavení

- Visual Studio s podporou .NET 10 (Visual Studio 2026), .NET 10 SDK, úloha „.NET desktop development“. Spouštěcí projekt: **`Meteo.Windows`**.
- Ladění: `src/Meteo.Windows/Properties/launchSettings.json` obsahuje profily „Celá obrazovka“, „Šetřič (/s)“, „Okno 1080x1920“,
  „Okno 1200x1920“, „Okno 1366x768“ a „Nastavení“. Vybírají se v nabídce vedle tlačítka Start.

## Instalace jako šetřič

```
dotnet publish src/Meteo.Windows -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Ve složce `publish` vznikne `Meteo.exe` a jeho kopie `Meteo.scr`. Pravým tlačítkem na `.scr` → **Nainstalovat** (šetřič).
`Meteo.exe` si lze nechat i vedle dat, např. v `C:\DOS\Meteo\`, a spouštět ručně jako samostatné okno.
Single-file je důležitý: Windows při instalaci kopíruje do `System32` jen samotný `.scr`.
Je potřeba nainstalované **.NET 10 Desktop Runtime**.

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
- Radar: **ČHMÚ, otevřená data** (<https://opendata.chmi.cz/meteorology/weather/radar/>): `composite/maxz/png/` (maximální
  odrazivost, snímek po 5 minutách) a `composite/fct_maxz/png/` (extrapolace +10 až +60 min). Snímky jsou v projekci
  EPSG:3857 a pokrývají východní délku 11,267–20,770° a severní šířku 48,047–52,167° (`Meteo.Core/RadarGeo.cs`); zobrazuje se
  výřez, kam až sahají data. Podklad (státní a krajské hranice) je z **Natural Earth** (volné dílo, `Data/map.txt`).
- Flymet: `https://flymet.meteopress.cz/meteogram1/<NÁZEV>.png`. Název se odvodí z obce místa (velká písmena bez diakritiky
  a mezer, např. `PRIBYSLAV`); pokud obrázek neexistuje, ukáže se jen informace a další pokus je za 24 hodin. Ruční přepsání:
  `FlymetSlugs` v `settings.json`, např. `{ "LKPI": "PRIBYSLAV" }`. Obrázky jsou majetek Meteopress, jen pro osobní použití
  a v repozitáři nejsou.

## Co bylo ověřeno

Jádro a Windows vrstva se zkompilovaly a otestovaly na syntetických datech (parsování odpovědi Open-Meteo včetně vrstev
oblačnosti, databáze letišť a hledání, nastavení, parametry příkazové řádky, georeference a mapový podklad radaru,
stahování a cache radarových snímků proti falešnému serveru, zamykání okna). Zdrojový kód WPF části prošel jen syntaktickou
kontrolou. WPF projekt nebyl v prostředí, kde vznikl, zkompilován ani spuštěn
a skutečné servery Open-Meteo, Flymet a ČHMÚ nebyly dostupné. Názvy radarových souborů a rozsah snímku vycházejí z popisu
ČHMÚ (`radar_description_en.pdf`) a nebyly ověřeny na skutečném snímku. Kdyby značka místa na radaru neseděla, upraví se
konstanty v `RadarGeo.cs` (rozsah snímku) a `Dashboard.cs` (`RadarViewLonMax`, `RadarViewLatMax`).
