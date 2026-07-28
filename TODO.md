# Diskora — TODO

Živý checklist projektu. Odškrtává se postupně napříč vývojem, nedělá se najednou.
Architektura a zdůvodnění rozhodnutí: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Fáze 0 — Základy repozitáře
- [x] `git init`, `.gitignore`
- [x] `LICENSE` (GPLv3)
- [x] `README.md`, `CHANGELOG.md`
- [x] `TODO.md` (tento soubor)
- [x] `docs/ARCHITECTURE.md`, `docs/CONTRIBUTING.md`, `docs/SECURITY.md`
- [x] Kostra `.sln` a projektů (`Diskora.App`, `Diskora.Core`, `Diskora.Native`, `Diskora.Core.Tests`)
- [x] CI pipeline (build/test na Windows runneru) — `.github/workflows/build.yml`
      (`windows-latest`, `actions/setup-dotnet` na `10.0.x`, `dotnet restore/build/test`
      nad `app/Diskora.slnx` v konfiguraci Release). Repozitář zatím nemá GitHub remote
      (`origin` je lokální bare repo), takže se pipeline nemůže spustit na GitHubu -
      ale přesně tahle posloupnost příkazů (restore → build Release → test Release)
      byla živě ověřena lokálně a projde (74 testů, 0 chyb), YAML syntax ověřena
      přes `js-yaml`

## Fáze 1 — Enumerace disků a dashboard
- [x] Modely `PhysicalDiskInfo`, `VolumeInfo`, `DiskMediaType`, `DiskBusType`
- [x] `DiskEnumerationService` (WMI `MSFT_PhysicalDisk` + fallback `Win32_DiskDrive`, `DriveInfo` pro svazky)
- [x] `ByteSizeFormatter` + testy
- [x] `ElevationHelper` (detekce admin práv)
- [x] Základní WPF shell (navigace, light/dark theming)
- [x] Dashboard view: seznam fyzických disků a svazků s kapacitou/typem/rozhraním
- [x] Mapování svazek → fyzický disk (WMI asociátorový řetězec Win32_LogicalDisk →
      Win32_LogicalDiskToPartition → Win32_DiskPartition → Win32_DiskDriveToDiskPartition →
      Win32_DiskDrive) - živě ověřeno na C:\ i E:\, u svazků přes více disků se bere první
- [x] Barevné odznaky typu disku (SSD/HDD/vyměnitelný/virtuální) u fyzických disků i svazků
      v dashboardu - živě ověřeno
- [x] Ikona aplikace (vlastní design, vícerozměrná .ico pro exe i titulek okna)
- [x] Horní menu (Soubor/Zobrazit/Nápověda) s funkčním přepínáním světlé/tmavé/podle systému
- [x] Oprava kontrastního bugu: přepis SystemColors + explicitní styly DataGridRow/Menu,
      aby výchozí (nestylované) části ovládacích prvků nedědily tmavé barvy OS nezávisle
      na zvoleném tématu aplikace

## Fáze 2 — S.M.A.R.T. monitoring
- [x] `Diskora.Native`: SMART READ DATA/THRESHOLDS pro ATA/SATA (legacy `IOCTL_SMART_RCV_DRIVE_DATA`)
- [x] Přechod/doplnění na `IOCTL_ATA_PASS_THROUGH` (spolehlivější u některých řadičů):
      `AtaPassThroughSmartReader` (obecný kanál pro libovolný ATA příkaz,
      `ATA_PASS_THROUGH_EX` s bufferem hned za strukturou) doplnil původní
      `LegacySmartIoctlReader`. `AtaSmartReader` je nově jen rozcestník: zkusí
      pass-through, při neúspěchu spadne na legacy, a když selžou obě, spojí
      důvody do jedné hlášky (shodné důvody nedupluje). Úspěch s prázdnou
      tabulkou se záměrně bere jako neúspěch - prázdný seznam nahlášený jako
      „v pořádku" by uživateli tvrdil, že disk je bez problémů, aniž by se
      cokoli změřilo. Parsování tabulky atributů vytaženo do sdíleného
      `SmartAttributeTableParser` (do teď bylo privátní uvnitř readeru, tedy
      netestovatelné) - 8 nových testů vč. 48bitových syrových hodnot a
      hraničních případů; prahy jsou nově best-effort, protože příkaz 0xD1
      je v novějších revizích ATA zastaralý a disk ho smí odmítnout.
      **Živý test s admin právy odhalil skutečnou chybu, která tu byla od
      Fáze 2**: legacy cesta měla hlavičku `SENDCMDOUTPARAMS` spočítanou na
      8 bajtů místo 16 (`cBufferSize` 4 + `DRIVERSTATUS` 12), takže
      `DeviceIoControl` vždy vrátil `ERROR_INSUFFICIENT_BUFFER` (122) a
      S.M.A.R.T. nefungoval na ŽÁDNÉM ATA disku - jen se to nikdy neprojevilo,
      protože bez elevace selhalo dřív už otevření disku a chyba vypadala jako
      chybějící práva. Po opravě obě cesty vrací na všech 4 SATA discích
      (2× SATA SSD, 1× M.2 SATA SSD, 1× 4TB HDD) **bajt po bajtu identická
      data** - nezávislé křížové ověření, že je pass-through implementace
      správně. Živě ověřeno i end-to-end: `diskora healthcheck` hlásí nově
      `Healthy` u 5 z 6 disků (dřív „nedostupné" u všech), `diskora smart 3`
      i okno S.M.A.R.T. přes izolovaný harness ukazují 17 reálných atributů
      HDD (20 613 h provozu, 34 °C, 113 138 pohybových cyklů). U NVMe (chyba
      50) a USB mostu (chyba 1/122) obě ATA cesty korektně selžou - u NVMe
      se stejně použije jeho vlastní cesta. Živé testování zároveň ukázalo,
      že reálný HDD hlásí ID 3/4/11/200, které katalog neznal a zobrazoval
      jako „Neznámý atribut" - doplněny.
- [x] NVMe health log (`IOCTL_STORAGE_QUERY_PROPERTY` / protocol-specific):
      `Diskora.Native.Smart.NvmeHealthReader` čte log stránku 0x02 přes
      `StorageDeviceProtocolSpecificProperty`. `SmartService` zkouší nejdřív NVMe
      a teprve pak ATA - dotaz na NVMe log uspěje jen u skutečně NVMe zařízení,
      je levný a **nepotřebuje admin práva** (handle se otevírá s
      `dwDesiredAccess = 0`; ATA passthrough elevaci vyžaduje vždy, a i vlastní
      `Get-StorageReliabilityCounter` ve Windows bez elevace odmítne CIM přístup).
      NVMe nemá ATA atributy s normalizovanou/nejhorší/prahovou hodnotou, ale
      pevnou sadu pojmenovaných polí - model se proto netváří jako ATA:
      `NvmeHealthInfo` (syrová data přepočtená na °C a bajty), `NvmeHealthCatalog`
      (11 řádků s českým názvem/hodnotou/vysvětlením - stejný odlišující prvek jako
      `SmartAttributeCatalog`) a `NvmeHealthEvaluator` s explicitními pravidly.
      `SmartReport` má nově nepovinné `NvmeHealth`, okno S.M.A.R.T. má druhou
      mřížku přepínanou přes `IsNvme`, CLI `smart`/`healthcheck` i export CSV/JSON
      pokrývají obě varianty. 15 testů. Živě ověřeno bez elevace na reálném
      Samsung SSD 980 PRO 1TB (PhysicalDrive4): CLI, `--json` i skutečná instance
      `SmartWindow` v izolovaném harness (NVMe mřížka 11 řádků viditelná, ATA
      skrytá; u SATA disku naopak). Dekódování ověřeno vnitřní konzistencí
      (36,15 TB zapsáno vs. 600 TBW udávaných výrobcem ≈ hlášených 6 %
      spotřebované životnosti). Ne-NVMe disky vrací `ERROR_INVALID_FUNCTION`
      a korektně padají zpět na ATA cestu. Cesta „vadný NVMe disk" (nenulové
      critical warning bity, vyčerpaná rezerva) živě ověřená není - takový disk
      v prostředí není, stejné omezení jako u vadných sektorů ve Fázi 3.
- [x] Dekódování atributů + srozumitelná vysvětlení rizika (odlišující prvek) - `SmartAttributeCatalog`
- [x] Health skóre (`SmartHealthEvaluator`, testováno) a graceful degradace, když SMART není dostupné
- [x] Minimální SMART UI (tlačítko u disku → okno s atributy, rizikem a celkovým verdiktem)
- [x] `Diskora.Data`: SQLite historie SMART hodnot (`SqliteDiskHistoryStore`, tabulka historie
      v okně S.M.A.R.T.) - živě ověřeno; grafické trendy (graf v čase) zatím ne, jen tabulka
- [x] Upozornění/notifikace při zhoršení zdraví (návaznost na Fázi 7 - tray):
      `DiskHealthChangeDetector` (čistá funkce, porovná pořadí Healthy &lt;
      Warning &lt; Critical, ignoruje Unknown a první kontrolu bez historie) +
      `DiskHealthMonitor` (pro každý disk přečte historii PŘED novým SMART
      čtením, aby se nesrovnávalo čerstvé čtení samo se sebou) - 19 testů
      (detektor i monitor s fake `ISmartService`/`IDiskHistoryStore`).
      `Diskora.App.Tray.DiskHealthNotifier` v `MainWindow` spouští kontrolu
      hned po startu a pak periodicky (`DispatcherTimer`, 30 min), SMART I/O
      běží přes `Task.Run` mimo UI vlákno; při zhoršení zavolá `TrayIconService.
      ShowBalloonTip`. Živě ověřeno jen zapojení (app čistě nastartuje a běží
      dál i bez admin práv, kdy SMART všude selže a kontrola se tiše
      přeskočí, žádný pád) - samotné zobrazení balónku při reálném zhoršení
      nebylo živě ověřeno: v tomto prostředí nejde SMART vůbec číst bez
      elevace (`Win32 chyba 5`) a žádný fyzický disk tu navíc reálně
      nedegraduje, stejné omezení jako u netestovaných cest v Fázích 3 a 5.

## Fáze 3 — Kontrola a oprava integrity
- [x] `FSCTL_IS_VOLUME_DIRTY` kontrola (`Diskora.Native.Fsctl.VolumeDirtyChecker`) - živě
      ověřeno na reálném nesystémovém svazku: funguje bez admin práv (na `C:`/boot svazku
      bez elevace nedostupné, na běžném svazku ano)
- [x] Orchestrace `chkdsk.exe /scan` (needestruktivní read-only sken), streamovaný výstup do UI
      (`Diskora.Repair.ChkdskRunner`, okno Kontrola integrity otevíratelné ze svazku) - živě
      ověřeno: vždy vyžaduje admin práva (VSS snapshot), i na nesystémovém svazku
- [x] Srozumitelný český průběh nad anglickým výstupem chkdsk (`ChkdskOutputParser` -
      parsuje "Stage N:"/"N percent complete", mapuje na české popisky fází, pohání
      grafický progress bar; syrový anglický log zůstává jako doplňkový detail, protože
      chkdsk vypisuje pevně anglicky bez ohledu na jazyk Windows) - 15 testů. S admin
      právy živě doověřeno izolovaným harness nad `ChkdskRunner` + `ChkdskOutputParser`
      (reálný `chkdsk /scan` na E:\): tam, kde bez elevace proces padal ještě před
      fází 1, teď proběhne `Started=True`, `ExitCode=0` a korektně detekuje průchod
      všemi třemi fázemi (Stage 1 → 2 → 3), takže výpočet `ProgressPercent` v
      `IntegrityViewModel` (baseline dle fáze + podíl v rámci fáze) dostává reálná
      data přes celý rozsah 0-100 %, ne jen "spadlo hned na startu". Pixelové
      screenshoty samotného okna nebylo možné pořídit v tomto vývojovém prostředí
      (nemá přístup k reálné interaktivní ploše - selhává i obecné GDI
      `Graphics.CopyFromScreen`, nezávisle na admin právech), vizuální kontrola
      progress baru v běžícím GUI proto zůstává na živém spuštění mimo tento nástroj
- [x] Skutečná oprava - jen `/spotfix` varianta zatím (`ChkdskRunner.RunSpotFixAsync`,
      `Repair-Volume -SpotFix` orchestrace stejným vzorem jako `IsoMounter` - cesta/písmeno
      přes proměnnou prostředí, ne interpolace). Vědomě NE `/f`/`/r` (offline scan+fix) -
      ty by na systémovém/uzamčeném svazku potřebovaly naplánovaný restart, což je
      samostatný, složitější UX problém ponechaný na příště. Spotfix je Windows 8+ online
      self-healing oprava (poškozené indexy, osiřelé soubory, bezpečnostní deskriptory)
      bez nutnosti odpojení ve většině případů, takže se týhle komplikaci vyhýbá.
      `IntegrityViewModel` má nové tlačítko „Opravit (spotfix)" s vlastním potvrzovacím
      dialogem PŘED zápisem (na rozdíl od needestruktivní kontroly výše) - `MessageBoxResult.
      No` je záměrně výchozí tlačítko, aby náhodný Enter/mezerník nemohl omylem potvrdit
      akci, která skutečně zapisuje na disk.
      Živě odhalené a opravené reálné chyby: (1) PowerShell skript bez admin práv Repair-Volume
      selhal jen jako NEterminating chyba (ne výjimka), `$result` zůstal null a skript to tiše
      prohlásil za úspěch (exit 0, prázdný `HealthStatus`) - opraveno `$ErrorActionPreference
      = 'Stop'` uvnitř try bloku + explicitní kontrola null/prázdného výsledku jako obrana do
      hloubky. (2) Live testování přes UI Automation nechtěně jednou skutečně vyvolalo
      potvrzovací dialog s "Ano" (pravděpodobně zdědění focusu/vstupu z předchozího kroku
      automatizace, přesný mechanismus se nepodařilo dohledat) - proto oprava (1) výše
      "Ano" jako výchozí tlačítko. Skutečný dopad ověřen: svazek E:\ zůstal po tomto
      nechtěném běhu beze změny (bez admin práv Windows operaci zablokoval dřív, než by
      mohla cokoliv zapsat - živě ověřeno výpisem obsahu E:\ před/po). Cestou deliberátně
      NEBYLO živě ověřeno: úspěšný běh spotfix s admin právy (tenhle krok automatický
      classifier prostředí zablokoval jako riziková akce - správně, jde o akci se skutečným
      zápisem na disk).
      DOPLNĚNO 2026-07-28: úspěšný běh spotfixu s admin právy ŽIVĚ OVĚŘEN na svazku H:
      (uživatel ho k destruktivním testům výslovně uvolnil) - a hned odhalil, že celá
      tahle cesta byla rozbitá. Skript kontroloval `$result.HealthStatus`, jenže
      `Repair-Volume` vrací PŘÍMO hodnotu typu `RepairStatus` (např. `NoErrorsFound`)
      a žádnou vlastnost `HealthStatus` nemá - kontrola tedy uvnitř try bloku vždycky
      vyhodila výjimku a i naprosto úspěšná oprava skončila jako `ExitCode=1`. Ověřeno
      samostatnou sondou přímo nad `Repair-Volume` (typ `RepairStatus`, hodnota
      `NoErrorsFound`, `HealthStatus` prázdný). Po opravě: `Started=True`, `ExitCode=0`,
      `AppearsClean=True`, výstup „Stav opravy: NoErrorsFound", 3,5 s, dirty bit před
      i po `Clean`.
- [x] Čtení Event Logu (`Diskora.Native.EventLog.DiskEventLogReader` přes
      `System.Diagnostics.Eventing.Reader.EventLogReader`, filtrováno na providery
      Ntfs/Disk/Volsnap/Virtual Disk Service/FilterManager/Wininit v protokolech
      System i Application) - nové okno „Systémový protokol" (menu Nástroje),
      read-only, funguje bez admin práv - živě ověřeno na reálném protokolu
      tohoto stroje (dirty-bit kontrola svazku E: se skutečně propsala jako
      Ntfs event 98, korektně česky lokalizovaná zpráva díky cs-CZ locale)
- [x] Read-only povrchový sken vadných sektorů + report (`Diskora.Native.Storage.
      PhysicalDiskSurfaceScanner` - sekvenční čtení `\\.\PhysicalDriveN` po 4MiB
      blocích, bufferovaný `FileStream` místo `FILE_FLAG_NO_BUFFERING` kvůli
      zarovnávacím požadavkům přímého I/O; při chybě čtení bloku se rozsah upřesní
      na 64KiB granularitu. `Diskora.Core.Services.SurfaceScanService` +
      `SurfaceScanResult`/`BadSectorRange` modely, nové okno „Povrchový sken disku"
      otevíratelné z řádku fyzického disku v dashboardu. Needestruktivní, nic
      nezapisuje - na rozdíl od `chkdsk /f`/`/spotfix` neřeší opravu. Živě ověřeno
      s admin právy: celý 4GB testovací fyzický disk (backing store E:\) proskenován
      za ~1s, `AppearsClean=True`, `ProgressPercent` plynule 0→100 %; zrušení skenu
      uprostřed (`CancellationToken`) korektně funguje (ověřeno na reálném 238GB
      systémovém fyzickém disku, zrušeno po ~5 % / 26s). Cestu "nalezené vadné
      oblasti" se živě ověřit nepodařilo - žádný disk se skutečně vadnými sektory
      není v tomto prostředí k dispozici (stejné omezení jako u netestované
      defragmentace HDD ve Fázi 5)
- [x] Historie výsledků kontrol (`SqliteDiskHistoryStore`, tabulka historie v okně Kontrola
      integrity - dirty-bit i výsledky skenů) - živě ověřeno

## Fáze 4 — Analýza zaplněnosti (styl TreeSize)
- [x] Rekurzivní scanner složek/souborů (`DiskUsageScanner`) - živě ověřeno na reálném
      naplněném svazku, výsledky (velikosti/počty souborů) sedí přesně a
      `System Volume Information` je správně nahlášena jako nedostupná
- [x] Zrychlení: vícevláknový sken s omezenou paralelitou (`SemaphoreSlim` přes
      `Environment.ProcessorCount * 2`) - sken celého `C:\` (437 311 souborů, 150 579
      složek, 101 GB) teď doběhne za ~48-76 s místo dřívějšího nedokončení ani po 4 minutách.
      Cestou se živě odhalily a opravily dvě reálné chyby vlastní paralelizace:
      1) první verze držela permit ze semaforu PO CELOU DOBU čekání na potomky - to je
         prioritní inverze (rodič drží permit a čeká na potomka, který ale potřebuje
         permit ze stejné fronty), hluboké větve se tak zasekávaly na minuty navíc, hůř
         než bez paralelizace. Opraveno: permit se drží jen po dobu synchronního I/O
         jedné složky a uvolní se PŘED rekurzí do potomků.
      2) i po opravě (1) sken v GUI (na rozdíl od izolovaného konzolového testu přímo
         nad `Diskora.Core`) trval přes 3 minuty i po dokončení skutečné I/O práce -
         hlášení postupu pro KAŽDOU navštívenou složku (150 tisíc+) zaplavovalo UI vlákno,
         protože každé volání skrz `SetField`/binding spouští drahé globální
         `CommandManager.InvalidateRequerySuggested()`. Opraveno prahováním hlášení na
         max. 10x/s (`ThrottledProgressReporter`) - a tahle oprava sama odhalila třetí
         bug: throttler inicializovaný na `long.MinValue` přetekl při prvním odečtu
         (`now - long.MinValue` > `long.MaxValue`) a tiše zahazoval úplně všechna
         hlášení navždy - chyceno existujícím testem, který čekal aspoň jedno hlášení
         a dostal prázdnou kolekci.
      Všechny tři opravy živě ověřeny (izolovaný harness nad `Diskora.Core` i plné GUI).
- [x] List view s drill-down navigací (řazeno dle velikosti, podíl v %, počet souborů)
- [x] Grafický přehled podílu (vodorovný kompoziční pruh + legenda, ověřená paleta ze
      skillu dataviz - part-to-whole formou segmentovaného pruhu, ne koláčem, protože
      u mnoha/dlouhých názvů složek se koláčové výseče špatně porovnávají; top 5 + "Ostatní")
      - živě ověřeno na reálných datech
- [x] Hledač velkých a starých souborů: `DiskUsageScanner` teď při skenu zároveň sleduje
      20 největších a 20 nejstarších souborů v celém stromu přes `BoundedTopTracker`
      (bez alokace na soubor, neškáluje pamětí s počtem souborů). Okno Analýza zaplněnosti
      má nové záložky „Největší soubory“/„Nejstarší soubory“ vedle „Složky“ - živě ověřeno
      na reálném svazku E:\ (správné řazení sestupně dle velikosti / vzestupně dle data)
- [x] Vlastní treemap control (squarified algoritmus): `Diskora.Core.Layout.SquarifiedTreemapLayout`
      - čistě geometrický port široce používaného referenčního algoritmu (Bruls/Huizing/
      van Wijk 2000, "squarify"), žádná závislost na WPF, 11 testů (zachování celkové
      plochy, žádné překryvy, zachování pořadí vstupu, hraniční případy). Nová záložka
      „Mapa" v okně Analýza zaplněnosti - Canvas vykreslovaný v kódu (`DiskUsageWindow.
      RebuildTreemap`), přepočítává se při změně dat i při změně velikosti okna.
      Barva buňky je sekvenční (jedna barva, světlá→tmavá dle podílu na celkové
      velikosti - viz skill dataviz: buňky nejsou pojmenované kategorie, kategoriální
      paleta by neseděla), dvě nové barvy motivu `TreemapCellLowBrush`/`HighBrush`
      (Light/Dark.xaml). Popisek se renderuje přímo do buňky (skillem zmíněná výjimka
      z "text nikdy nenese barvu dat") s barvou textu (bílá/tmavá) dle jasu výplně.
      Klik na buňku = drill-down (sdílí `NavigateInto` s tabulkovým zobrazením).
      Živě ověřeno (UI Automation + screenshoty, reálný scan `app/src` a drill-down do
      `Diskora.App`): rozvržení, barvy, popisky, klik-drilldown i resize okna fungují.
      Živé testování při té příležitosti odhalilo a opravilo skutečný bug: barvy
      buněk (i existujícího kompozičního pruhu) se počítaly v kódu jako statický
      `SolidColorBrush`, ne přes `DynamicResource` - při přepnutí světlé/tmavé téma
      za běhu (bez zavření okna) tak zůstávaly "zamrzlé" ve starém tématu. Přidán
      `ThemeService.ThemeChanged` event, `DiskUsageWindow` na něj přehraje oba
      výpočty - live ověřeno, že přepnutí tématu teď obě vizualizace okamžitě
      přebarví.
- [x] Hledač duplicit (hash-based): `Diskora.Core.Services.DuplicateFileFinder` - dvoufázově
      (seskupí podle velikosti souboru zdarma z metadat, teprve kandidáty se shodnou
      velikostí hashuje SHA-256, paralelně přes `Parallel.ForEachAsync`). Procházení stromu
      je záměrně jednovláknové (na rozdíl od `DiskUsageScanner`) - u paralelizace té scanner
      se objevily dvě netriviální souběžnostní chyby a zde by přinesla jen malý zisk, protože
      skutečné těžiště (hashování) je paralelizované samostatně a bezpečně. Nová záložka
      „Duplicity" v okně Analýza zaplněnosti (tlačítko „Najít duplicity", read-only, nic
      nemaže), řazeno podle reklamovatelné velikosti sestupně. 6 testů + živě ověřeno na
      reálném souboru (kopie `IMG_0001.jpg` na E:\ správně detekována jako duplicita,
      smazána po testu).
- [x] Export reportu (CSV i JSON): `Diskora.Core.Export.CsvWriter` (RFC 4180 escapování,
      5 testů) + tlačítko „Exportovat CSV..." a „Exportovat JSON..." v okně Analýza
      zaplněnosti, obě exportují aktuálně zobrazenou záložku (Složky/Největší soubory/
      Nejstarší soubory/Duplicity) přes `SaveFileDialog`. JSON přes `System.Text.Json`
      s `JavaScriptEncoder` omezeným na Basic Latin + Latin-1 Supplement + Latin Extended-A,
      aby čeština zůstala v souboru čitelná (přímo diakritika, ne `á` escapy) - živě ověřeno
      reálným exportem obou formátů, správná data i escapování.
- [x] Výběr libovolné složky ke skenování: menu Soubor → „Analyzovat složku..." otevírá
      `Microsoft.Win32.OpenFolderDialog`, funguje se stejným oknem Analýza zaplněnosti
      jako skenování celého svazku (`DiskUsageWindow` bere libovolnou cestu, ne jen kořen
      svazku) - živě ověřeno na `C:\Projekt\Diskora\docs` (správně naskenováno jen 6,85 KB
      / 3 soubory, ne celý disk C:)

## Fáze 5 — TRIM a defragmentace
- [x] Detekce SSD (`DeviceSeekPenaltyProperty`) a podpory TRIM (`DeviceTrimProperty`) přes
      `Diskora.Native.Storage.StoragePropertyReader` (IOCTL_STORAGE_QUERY_PROPERTY na svazku,
      funguje bez admin práv na nesystémových svazcích) - živě ověřeno
- [x] Ruční TRIM přes orchestraci `defrag.exe /L` (`Diskora.Repair.DefragRunner`) - živě
      ověřeno, vč. opravy mojibake v OEM-kódovaném výstupu (CP852)
- [x] Defragmentace HDD přes orchestraci `defrag.exe /D` (stejný `DefragRunner`) - kód
      sdílí ověřenou cestu s TRIM.
      DOPLNĚNO 2026-07-28: ŽIVĚ OVĚŘENO na skutečném mechanickém disku (USB WDC WD32,
      298 GB, svazek H:) - `Started=True`, `ExitCode=0`, 92 řádků výstupu s kompletním
      Pre-Optimization i Post Defragmentation reportem (296 978 přesouvatelných souborů,
      MFT 393,75 MB), doba ~3 s. Zjištěno i to, že přes USB most nejde určit
      `DeviceSeekPenaltyProperty` (vrací null), takže Diskora u takového disku
      defragmentaci vůbec nenabídne - správné chování dle pravidla „při nejistotě
      nenabízet nic", ale znamená to, že tenhle konkrétní disk jde defragmentovat jen
      přes službu přímo, ne z UI. Interní 4TB SATA HDD (F:) se naproti tomu rozpozná
      správně (`HasSeekPenalty=True`, `SupportsTrim=False`).
- [x] Vlastní analýza fragmentace (`FSCTL_GET_RETRIEVAL_POINTERS`) pro report před spuštěním:
      `Diskora.Native.Storage.FileFragmentationReader` čte počet fragmentů (nesouvislých
      rozsahů clusterů) jednoho souboru - na rozdíl od SMART/povrchového skenu stačí
      právo číst daný soubor, žádná elevace. Buffer dimenzovaný na 512 extentů, nad tuhle
      hranici se vrátí jen dolní odhad ("512+"), místo opakovaného volání IOCTL s
      posunutým `StartingVcn` - pro report to stačí. `Diskora.Core.Services.
      FragmentationAnalysisService` prochází strom jednovláknově (stejný důvod jako
      `DuplicateFileFinder`) a čtení jednotlivých souborů paralelizuje přes
      `Parallel.ForEachAsync`. Nové tlačítko „Analyzovat fragmentaci" + záložka
      „Fragmentace" (report + tabulka nejvíc fragmentovaných souborů) v okně
      Optimalizace disku - viditelné jen pro HDD (stejná logika jako u TRIM/defrag:
      nenabízet nesmyslnou akci pro zjištěný typ disku). 5 testů nad `Diskora.Core.
      Tests` běžících nad SKUTEČNÝMI soubory a reálným IOCTL voláním (ne fake) - živě
      ověřeno, že čerstvě zapsané malé soubory korektně nevycházejí jako fragmentované.
      Průchod UI (tlačítko/tabulka na skutečném HDD) se živě ověřit nepodařilo - v
      tomto prostředí jsou všechny disky SSD, stejné omezení jako u netestované
      defragmentace HDD ve Fázi 5.
- [x] Automatické skrytí irelevantních akcí podle typu disku (okno Optimalizace nabídne
      jen TRIM na SSD, jen defragmentaci na HDD, nic při nejistém zjištění)

## Fáze 6 — Virtuální disky a obrazy
- [x] VHD/VHDX: čtení metadat přes `virtdisk.dll` (`OpenVirtualDisk`/`GetVirtualDiskInformation`,
      funguje bez admin práv) - nový projekt `Diskora.VirtualDisks`
- [x] VHD/VHDX: připojení/odpojení (`AttachVirtualDisk`/`DetachVirtualDisk`) - vyžaduje admin
      práva (ověřeno: Win32 chyba 1314 bez elevace, srozumitelně zobrazeno v UI). S admin
      právy živě odhalen a opraven skutečný bug: `Attach` neposílal
      `ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME`, takže Windows vázal připojení na
      životnost handlu z `AttachVirtualDisk` - a `WithOpenHandle` ten handle hned po
      volání zavírá (`CloseHandle` ve `finally`), takže se disk vteřinu po "úspěšném"
      připojení tiše zase odpojil (`diskpart detail vdisk` pak ukazoval "Associated
      disk#: Not found."). Bez elevovaného testu se tohle nedalo odhalit - dřív ověřená
      byla jen cesta selhání bez admin práv, ne skutečné připojení. Po přidání flagu
      živě ověřeno na testovacím VHDX (vytvořen/naformátován přes diskpart): disk
      zůstává připojený, přidělí se mu písmeno, `Detach` funguje. Zároveň při
      testování narazil na druhý reálný rough-edge: opětovné připojení už
      připojeného souboru vracelo jen syrové "Win32 chyba 32" - přidán srozumitelný
      český popis (ERROR_SHARING_VIOLATION → "soubor už je otevřený/připojený
      jinde... nejdřív ho odpojte"), živě ověřeno dvojím připojením stejného VHDX
- [x] ISO: mount/dismount přes orchestraci `Mount-DiskImage`/`Dismount-DiskImage`
      (`Diskora.Repair.IsoMounter`) - živě ověřeno, funguje bez admin práv (na rozdíl od
      VHD/VHDX). Přímé `AttachVirtualDisk` s VIRTUAL_STORAGE_TYPE_DEVICE_ISO sice vrátí
      úspěch i bez elevace, ale výsledná jednotka zůstane bez souborového systému -
      zdokumentováno v kódu, proto orchestrace přes ověřený cmdlet místo P/Invoke
- [x] IMG/raw: read-only sektorová inspekce (mount jako virtuální disk vědomě
      vynechán - živě ověřeno, že `Mount-DiskImage` raw `.img` odmítá jako
      "soubor je porušen a není čitelný" a `virtdisk.dll`/`AttachVirtualDisk`
      pro tento formát vůbec neexistuje kontejner/hlavička k rozpoznání, takže
      by šlo jen o vlastní ovladač virtuálního disku - mimo filozofii minima
      závislostí). Nový `Diskora.VirtualDisks.RawImageInspector` čte MBR (offset
      446-509, boot signatura 0x55AA) i GPT (protective MBR typ 0xEE →
      hlavička "EFI PART" na LBA1 → tabulka oddílů), obyčejným čtením souboru
      bez admin práv - jen primární MBR záznamy, rozšířené/logické oddíly
      přes EBR řetěz se neprochází. `VirtualDiskWindow` dostal třetí větev
      (vedle VHD/VHDX attach/detach a ISO mount/dismount): tlačítko
      „Prozkoumat rozvržení", zobrazí schéma + počet oddílů. Živě ověřeno na
      dvou reálných discích (fixed VHD jako nosič syrových bajtů, ne
      mountované) - MBR se 2 oddíly správně `Scheme=Mbr, PartitionCount=2`,
      po `convert gpt` se stejným rozvržením správně `Scheme=Gpt,
      PartitionCount=2`; end-to-end i přes `VirtualDiskService` s reálnou
      příponou `.img`
- [x] Bezpečný unmount/cleanup (i při pádu aplikace): `AttachVirtualDisk` běží
      s `ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME`, takže OS drží připojení
      nezávisle na procesu Diskory - po pádu/zavření bez odpojení disk zůstane
      připojený, což je stejné chování jako `Mount-VHD`/diskpart. Řešením proto
      není auto-detach při zavření (to by šlo proti smyslu permanent lifetime
      flagu), ale upozornění: nový `IVirtualDiskAttachmentRegistry` (SQLite,
      `Diskora.Data.SqliteVirtualDiskAttachmentRegistry`, tabulka
      `AttachedVirtualDisks` ve stejné `diskora.db`) zapisuje úspěšné
      připojení/odpojení VHD/VHDX i ISO. Při startu aplikace
      (`App.OnStartup`) se zbylé záznamy z minulého běhu ukážou v
      informačním dialogu; záznamy pro mezitím smazané soubory se tiše
      promažou (`GetTrackedAttachments`). 8 testů (registr + ověření, že
      neúspěšný attach/detach registr nezasáhne). Skutečné admin-vyžadující
      připojení/odpojení nebylo v tomto prostředí živě ověřeno (žádná
      elevace k dispozici) - ověřeno jen sestavení, běh celé sady testů a
      čistý start/konec GUI se startovní kontrolou.
- [x] Znovupoužití integrity/SMART/TreeSize logiky nad připojenými virtuálními disky -
      živě ověřeno izolovaným harness nad připojeným testovacím VHDX (fyzický disk
      správně rozpoznán jako `BusType=FileBackedVirtual`): `IntegrityCheckService`
      (dirty bit i celý `chkdsk /scan` přes 3 fáze) i `DiskUsageScanner` fungují nad
      připojeným svazkem beze změny. `SmartService` korektně a srozumitelně degraduje
      (SMART na virtuálním disku není dostupné - stejná cesta jako u USB mostů/NVMe,
      žádná speciální větev navíc potřeba)

## Fáze 7 — Plánování a CLI companion
- [x] Integrace s Windows Task Scheduler (periodické kontroly zdraví): nové CLI
      příkazy `diskora healthcheck` (S.M.A.R.T. přes všechny fyzické disky najednou,
      stejná konvence návratových kódů jako `smart`) a `diskora schedule
      install/remove/status` (`Diskora.Repair.ScheduledTaskManager`, orchestrace
      `schtasks.exe` - stejný vzor jako `ChkdskRunner`/`DefragRunner`, cesta k
      vlastnímu `diskora.exe` přes `Environment.ProcessPath`, ne přes uživatelský
      vstup). Bez `/RU`/`/RP` se úloha vytvoří pod aktuálním uživatelem - nevyžaduje
      admin práva, na rozdíl od `Diskora.App.Tray.DiskHealthNotifier` (Fáze 2),
      který kontroluje jen po dobu běhu GUI - naplánovaná úloha běží i když GUI vůbec
      neběží. Stejná mojibake oprava OEM kódové stránky jako u `DefragRunner`
      (`schtasks.exe` píše diakritiku v CP852, ne UTF-8). Živě ověřeno celým
      cyklem: `schedule install` → `schedule status` (i nezávisle přes `Get-
      ScheduledTask`/`schtasks /V`) → `schedule remove` → potvrzeno smazáno;
      `healthcheck` živě ověřeno na obou fyzických discích (bez admin práv
      korektně "nedostupné", exit kód 2).
- [x] `Diskora.Cli`: headless mód s JSON výstupem pro skriptování - nový projekt
      (`AssemblyName=diskora`), top-level statements, žádná externí CLI-parsing
      závislost (ruční parsování, konzistentní s filozofií minima závislostí).
      Příkazy: `list` (fyzické disky + svazky), `smart <index>`, `integrity
      <písmeno> [--scan]`, `usage <cesta> [--top N]`, `duplicates <cesta>` -
      všechny skládají už hotové a otestované `Diskora.Core` služby, žádná nová
      byznys logika. Globální `--json` (System.Text.Json, čitelná diakritika
      stejně jako v GUI exportu, enumy jako řetězce přes `JsonStringEnumConverter`).
      `smart`/`integrity` sdílí stejnou SQLite historii jako GUI
      (`Diskora.Data.SqliteDiskHistoryStore`), takže kontrola z CLI se ukáže
      i v historii v okně S.M.A.R.T./Kontrola integrity. Smysluplné exit kódy
      (0 v pořádku, 1 chyba použití/cesty, 2 nalezen problém/duplicity/SMART
      nedostupné, 130 přerušeno Ctrl+C). Živě ověřeno - všechny příkazy
      (člověku čitelný i `--json` výstup), reálná data (fyzické disky, svazky,
      dirty-bit E:\, sken zaplněnosti, skutečně vytvořená a smazaná duplicita).
- [x] Tray ikona: `Diskora.App.Tray.TrayIconService` přes `System.Windows.Forms.NotifyIcon`
      (`UseWindowsForms` v csproj - WPF vlastní tray API nemá, žádná externí závislost
      navíc; auto-přidané global usingy `System.Windows.Forms`/`System.Drawing` odstraněny
      z csproj, protože kolidovaly s WPF typy `Application`/`Color` napříč projektem -
      WinForms typy jsou plně kvalifikované). Ikona je vidět po celou dobu běhu (ne jen
      po minimalizaci - připraveno na budoucí notifikace bez nutnosti mít okno otevřené),
      kontextové menu „Zobrazit Diskoru"/„Konec", dvojklik obnoví okno. Minimalizace okno
      úplně schová (`Hide`, zmizí i z hlavního panelu), zavření (×) aplikaci normálně
      ukončí - žádné překvapivé "zmizení" aplikace. Živě ověřeno (UI Automation +
      screenshoty): ikona se objeví ve skryté oblasti hlavního panelu se správným
      obrázkem aplikace, minimalizace okno schová i z hlavního panelu, dvojklik na
      ikonu okno spolehlivě obnoví.
      Zbývá: skutečná notifikace při zhoršení zdraví disku (`ShowBalloonTip` už
      existuje a je připravené k použití, ale nic ho zatím nevolá - potřeba
      periodická kontrola na pozadí, mimo rozsah tohoto kroku).

## Fáze 8 — Reporting, nastavení, lokalizace, přístupnost
- [x] Export do CSV/JSON napříč okny (PDF zatím ne): dřív mělo export jen okno Analýza
      zaplněnosti. Přidáno do S.M.A.R.T., Kontrola integrity, Povrchový sken a Systémový
      protokol - stejný vzor (`Diskora.Core.Export.CsvWriter` + `System.Text.Json` s
      encoderem omezeným na Basic Latin/Latin-1/Latin Extended-A pro čitelnou diakritiku),
      teď sdílený přes nový `Diskora.App.Export.ExportHelper` (SaveFileDialog + zápis +
      ošetření chyby na jednom místě - dřív duplikováno jen v okně Analýza zaplněnosti,
      teď by se to opakovalo pětkrát). `SurfaceScanViewModel` dostal novou veřejnou
      `BadRanges` property (syrová offset/délka data), aby JSON export mohl nabídnout
      strukturovaná čísla, ne jen naformátované řetězce z `BadRangeRows`. Živě ověřeno
      (UI Automation): okno Systémový protokol export CSV i JSON nad reálnými daty
      (skutečné události z protokolu tohoto stroje, čeština i escapování v pořádku),
      okno S.M.A.R.T. export obou formátů i nad prázdnými daty (SMART nedostupné bez
      admin práv) - žádný pád, smysluplný prázdný/degradovaný výstup. Kontrola integrity
      a Povrchový sken sdílí identický, tímto už ověřený mechanismus, ale jejich konkrétní
      tlačítka se živě neklikala zvlášť.
- [x] Perzistence volby tématu: `Diskora.App.Settings.JsonAppSettingsStore` (obyčejný
      JSON v `%LocalAppData%\Diskora\settings.json` - jen hrstka skalárních hodnot,
      SQLite jako u historie by tu byl zbytečný). `ThemeService.Apply` volbu při každém
      přepnutí uloží, `App.OnStartup` ji při startu načte zpátky místo pevného
      `AppTheme.System` - zbytek Fáze 8 (jazyk, chování elevace, práh notifikací) zatím
      ne, žádná nová Nastavení obrazovka, jen perzistence už existující volby z menu
      Zobrazit. 9 testů (store i parsování uložené hodnoty). Živě ověřeno: přepnutí na
      Světlé, zavření, nové spuštění - okno naběhne rovnou světlé, i když má tenhle
      stroj systémové téma tmavé (`AppsUseLightTheme=0`), takže jde skutečně o uloženou
      volbu, ne o shodu se systémem.
- [x] Nastavení (práh notifikací, chování elevace) - jazyk viz samostatná Lokalizace
      níže. Nové okno „Nastavení" (menu Nástroje → Nastavení...): práh, od kterého
      tray upozorní na zhoršení zdraví disku (Varování a horší / Jen kritické -
      `AppSettings.NotificationThreshold`, filtruje se v `DiskHealthNotifier`) a
      volba „Při startu bez práv administrátora nabídnout restart s elevací"
      (`AppSettings.PromptForElevationAtStartup`, výchozí vypnuto). Když je
      zapnutá a Diskora zrovna neběží jako administrátor, `App.OnStartup` nabídne
      restart dialogem s výchozí volbou „Ne" (stejný bezpečnostní vzor jako
      potvrzení spotfixu ve Fázi 3) - potvrzení spustí `Environment.ProcessPath`
      znovu s `Verb="runas"` a ukončí aktuální instanci; zrušení UAC promptu
      (Win32 chyba 1223) se tiše ignoruje, appka pokračuje bez elevace.
      Menu-klik na „Nastavení..." se v tomhle prostředí nepodařilo živě ověřit
      přes UI Automation (stejný problém má i preexistující položka „Systémový
      protokol..." - jde o obecné omezení automatizace Menu/MenuItem v tomhle
      prostředí, ne bug nového kódu). Samotné okno je ale živě ověřeno izolovaným
      harness (skutečná instance `SettingsWindow` nad dočasným JSON souborem):
      výchozí hodnoty se správně načtou, změna comboboxu + checkboxu a Uložit
      korektně zapíše do JSON, a nově otevřené okno nad stejným souborem správně
      načte uloženou volbu zpátky. Restart-s-elevací cesta (skutečné potvrzení
      UAC) záměrně NEBYLA živě zkoušená - stejný důvod jako u spotfixu ve Fázi 3
      (jde o akci se skutečným dopadem, ne o něco k náhodnému odklikání).
- [ ] Lokalizace (cs-CZ, en-US)
- [x] Přístupnost (screen reader labels, klávesová navigace, vysoký kontrast) - téměř
      celé, zbývá jen vysoký kontrast a treemapa:
      - Screen reader labels: všech 5 `ProgressBar` v aplikaci dostalo
        `AutomationProperties.Name` s aktuální hodnotou (viz předchozí záznam).
      - Klávesová navigace (Tab pořadí): audit napříč všemi okny aplikace - nikde
        se nepoužívá explicitní `TabIndex`, takže pořadí procházení Tabem všude
        odpovídá pořadí deklarace v XAML, a to ve všech Gridech/DockPanelech
        důsledně odpovídá vizuálnímu pořadí čtení (shora dolů, zleva doprava).
        Živě ověřeno na novém okně Nastavení (izolovaný harness, skutečné
        `MoveFocus`): pořadí ComboBox → CheckBox → „Zrušit" → „Uložit" přesně
        odpovídá vizuálnímu rozložení (Zrušit vlevo od Uložit).
      - Vlastní buňky treemapy zaplněnosti (Fáze 4, vykreslované v kódu jako
        `Border`) nemají vlastní automation peer a čtečkou obrazovky ani Tabem
        nejdou procházet/aktivovat - vědomě zatím neřešeno, protože stejná data
        má i plně přístupná záložka „Složky" (DataGrid) vedle; oprava by
        vyžadovala buňky předělat na `Button` s vlastní šablonou.
      - Vysoký kontrast (Windows High Contrast mode) zůstává neřešený - vyžadoval
        by systematický průchod všech `Themes/Light.xaml`/`Dark.xaml` barev a
        živé přepnutí Windows High Contrast režimu, což je samostatný, větší kus
        práce ponechaný na příště.

## Fáze 9 — Bezpečnost a release engineering
- [x] `SECURITY.md` — threat model, responsible disclosure: hlášení zranitelnosti
      a návrhové principy existovaly už dřív, doplněna nová sekce „Model hrozeb"
      (aktiva, důvěryhodné hranice/vstupy, modelovaní útočníci vč. explicitně
      MIMO rozsah, zmírnění podle Fáze s odkazem na konkrétní commity/opravy).
      Čistě dokumentační/analytická práce nad již existujícím kódem a
      bezpečnostními rozhodnutími zdokumentovanými jinde v tomhle souboru -
      není co živě ověřovat.
- [x] Statická analýza (Roslyn analyzery) + CodeQL v CI: Roslyn analyzery už běžely
      od Fáze 0 (`EnableNETAnalyzers` v `Directory.Build.props`, `AnalysisLevel=latest`) -
      chybělo jen CodeQL. Nový `.github/workflows/codeql.yml` (`github/codeql-action/
      init` + `analyze` pro C#, spouští se na push/PR do master i týdně navíc přes
      `schedule: cron`). Stejné omezení jako CI pipeline z Fáze 0 - repozitář zatím
      nemá GitHub remote, takže se nemůže spustit naživo, YAML syntax ověřena lokálně
      přes `js-yaml`.
- [x] Dependency scanning (Dependabot/Renovate): nový `.github/dependabot.yml` -
      tři ekosystémy (`nuget` pro `/app`, `npm` pro `/web`, `github-actions` pro
      samotné workflow soubory), týdenní interval. Stejné omezení - nemůže se
      spustit naživo bez GitHub remote, YAML syntax ověřena lokálně.
- [ ] Code signing pipeline (Authenticode) pro release buildy
- [x] Portable build (self-contained, single-file): nový `app/publish-portable.ps1`
      spouští `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=
      true -p:IncludeNativeLibrariesForSelfExtract=true` - výsledek je jediný
      přenositelný `Diskora.exe` (~176 MB, obsahuje celý .NET runtime), který
      nepotřebuje .NET nainstalované v cíli. Výstup se negeneruje do repozitáře
      (`/dist/` v `.gitignore`, stejný princip jako `app/sbom/`). Živě ověřeno:
      publikovaný `.exe` zkopírovaný do vlastní složky se spustil a otevřel hlavní
      okno (potvrzeno přes UI Automation), bez jakékoliv závislosti na globálně
      nainstalovaném SDK/runtime.
- [x] SBOM generování: `dotnet-CycloneDX` jako lokální (ne globální) .NET nástroj
      přes tool manifest (`app/.config/dotnet-tools.json`, `dotnet tool restore`) -
      žádná trvalá změna systému, jen per-repo pin verze nástroje, stejný princip
      jako package-lock. Krok `dotnet tool restore` + `dotnet tool run dotnet-CycloneDX`
      přidán do `.github/workflows/build.yml`, výsledný `bom.json` se nahrává jako
      CI artefakt (`actions/upload-artifact`) - generuje se čerstvě při každém
      buildu, proto se sám soubor necommituje (`app/sbom/` v `.gitignore`). Na
      rozdíl od CodeQL/Dependabot (čistě konfigurační, nemohly se spustit bez
      GitHub remote) tohle šlo živě vyzkoušet lokálně: `dotnet tool restore` +
      `dotnet tool run dotnet-CycloneDX -- Diskora.slnx -o sbom --json` reálně
      vygeneroval platný CycloneDX 1.7 JSON (30 komponent, správné hashe/licence
      NuGet balíčků) - jen samotný krok "spustit se v GitHub Actions" zůstává
      needověřený kvůli chybějícímu remote.

## Fáze 10 — Produktový web (Diskora Web)
- [x] Scaffold Astro 7 + Tailwind v4 (`web/`), bez trackerů/analytiky, `noindex` dokud
      není veřejné vydání - živě ověřeno (`npm run build`, 0 zranitelností při instalaci)
- [x] Landing page (hero s verzí 0.1.0 a upřímným "rané vývojové stádium" místo
      fake download tlačítka, 4 pilíře se skutečnými screenshoty ze živého testování,
      4 odlišující prvky, sekce Bezpečnost)
- [x] Nápověda (`web/src/pages/docs/`) — vlastní lehké Astro stránky se sdíleným
      `DocsLayout` (sidebar navigace) místo plného Starlight frameworku (méně
      komplexity pro tuto velikost obsahu, konzistentní vzhled s landing page);
      3 reálné podstránky se screenshoty: Kontrola integrity, Analýza zaplněnosti,
      Bezpečnost a oprávnění - živě ověřeno (`astro build` + `astro preview`,
      všech 5 stránek vrací HTTP 200)
- [x] Reuse ikony aplikace jako favicon/logo webu (stejný soubor jako `Diskora.App`)
- [x] Nasazení na podcestu (`https://www.magorak.cz/diskora`), ne kořen domény:
      `astro.config.mjs` dostal `site`/`base: '/diskora/'`. Astro sám přizná base
      jen svým vlastním vygenerovaným assetům (`_astro/*.css`) - všechny ručně
      psané odkazy/obrázky v kódu (`href="/docs/"`, `src="/logo.png"` apod.) base
      nedostávají automaticky a musely se ručně přepsat na
      `` `${import.meta.env.BASE_URL}docs/` `` napříč `BaseLayout`, `DocsLayout`
      a všemi stránkami - jinak by na reálném hostingu pod podcestou byly
      všechny styly, obrázky i interní odkazy rozbité (fungovalo by to jen na
      kořeni domény). Živě odhaleno: otevření `web/dist/index.html` přímo přes
      `file://` v editoru ukázalo holý needostylovaný text bez obrázků - to je
      ale jen důsledek root-relative cest bez HTTP serveru, ne příznak nedodělané
      appky. Skutečná oprava (base path) ověřena jinak: `astro build` a ruční
      simulace produkčního nasazení (zkopírování `dist/` do `<root>/diskora/` a
      obsloužení přes obyčejný HTTP server na tom samém portu jako doména) -
      HTML, CSS, obrázky i interní odkazy (`/docs/security/`, `/docs/changelog/`)
      všechny vracely HTTP 200 správně pod `/diskora/...`, včetně zvýraznění
      aktivní položky v postranním menu nápovědy.
- [x] Changelog page (synchronizace s `CHANGELOG.md`): `web/src/pages/docs/changelog.astro`
      čte kořenový `CHANGELOG.md` přímo při buildu (`fs.readFileSync` + malý
      vlastní parser šitý přesně na tvar, který tenhle jeden soubor používá -
      "## verze"/"### kategorie"/"- položka" s odsazenými pokračovacími řádky,
      ne obecný markdown engine - konzistentní s filozofií minima závislostí,
      žádná nová npm závislost jen pro tuhle stránku). Stránka je tak vždy
      doslova ve shodě se skutečným changelogem, žádná ruční kopie, která by se
      mohla rozjet s realitou. Živě ověřeno: `astro build` (7 stránek) +
      `astro preview`, HTTP 200 i správně vyrenderované nadpisy verzí
      ("Nevydáno (aktuální vývoj)", "Verze 0.1.0 — 2026-07-25") a vnořené
      `<code>` značky z markdown zpětných uvozovek uvnitř položek.
- [x] Stránka ochrana soukromí (rozšíření sekce Bezpečnost v nápovědě):
      `web/src/pages/docs/privacy.astro` - žádná telemetrie/síť (ověřeno grepem
      přes `app/src`, v kódu není žádné `HttpClient`/`WebRequest`), co se ukládá
      lokálně a proč (SQLite historie, JSON předvolby, evidence připojených
      virtuálních disků - vše jen v `%LocalAppData%\Diskora`), co appka čte, ale
      neukládá (obsah souborů při hashování duplicit). Čistě faktický popis
      existujícího chování, ne marketingový text. Živě ověřeno (`astro build` +
      `astro preview`, HTTP 200), odkaz přidán do `DocsLayout` navigace i
      dlaždice na `/docs/`.
- [ ] Migrace nápovědy na Starlight, pokud obsah naroste natolik, že se vyplatí
      full-text search a auto-generovaný sidebar
- [ ] CI: build + deploy, kontrola bezpečnostních hlaviček/CSP

## Fáze 11 — Průběžná dokumentace a verzování
- [ ] Udržovat `docs/ARCHITECTURE.md`, `docs/CONTRIBUTING.md` aktuální
- [x] SemVer tagy + `CHANGELOG.md` per release: první skutečné vydání proběhlo
      2026-07-27 jako `v0.2.0`. Do té doby měl projekt 43 commitů, nula tagů,
      `Version` pořád na `0.1.0` a v sekci „Nevydáno" leželo 50 položek - což se
      naplno projevilo až u okna „Co je nového", které uživateli jako první
      ukazovalo právě tu vývojářskou hromadu. Minor bump podle SemVer (samé
      přírůstky funkcí, žádná breaking změna). Při té příležitosti uklizen i
      syrový CHANGELOG.md: sekce měla 10 střídajících se nadpisů kategorií,
      protože každý commit přidával vlastní blok - přeskládáno skriptem na jedno
      `### Opraveno` (21) a jedno `### Přidáno` (29) s ověřením, že počet položek
      před i po je shodný (50), takže se nic neztratilo. Prázdná sekce
      „Nevydáno" se nově nezobrazuje ani v aplikaci, ani na webu (dřív by
      zbyl osamocený nadpis bez obsahu). Živě ověřeno: aplikace hlásí
      `0.2.0` a okno „Co je nového" má nahoře `[0.2.0] - 2026-07-27`
      (21 + 29 položek), vygenerovaný web ukazuje „Verze 0.2.0 — 2026-07-27"
      se shodným počtem kategorií, a portable single-file build z Fáze 9
      se z otagovaného kódu sestavil a spustil.
- [x] In-app "Co je nového" propojené s webovou dokumentací per verze: nové okno
      (menu Nápověda → „Co je nového...") čte kořenový `CHANGELOG.md` zabalený jako
      embedded resource (funguje i v portable single-file buildu z Fáze 9).
      `Diskora.Core.Changelog.ChangelogParser` je záměrně stejná pravidla jako
      parser ve `web/src/pages/docs/changelog.astro` - obě verze changelogu čtou
      týž soubor, takže se nemůžou rozejít. Po aktualizaci na novou verzi se okno
      ukáže jednou samo (`AppSettings.LastSeenVersion`, porovnává se verze
      sestavení, ne datum - přeinstalace téže verze neotravuje); hodnota se uloží
      PŘED zobrazením, ať se okno neotvírá dokola, kdyby appka skončila zavřením
      křížkem. Tlačítko „Otevřít na webu..." předá adresu prohlížeči (Diskora sama
      žádné spojení nenavazuje - stránka o soukromí na webu na to nově upozorňuje).
      13 nových testů. Živě ověřeno izolovaným harness nad skutečnou instancí
      `WhatsNewWindow` nad reálným `CHANGELOG.md` (2 verze, 48 + 19 položek,
      282 úseků kódu vysázených monospace, zvýraznění správně, žádné doslovné
      `**` v textu). Živé testování při té příležitosti odhalilo dvě věci, které
      měl stejně špatně i web, a opravilo je na obou stranách: (1) `**tučný text**`
      se sázel doslova včetně hvězdiček, (2) jedna verze měla tolik nadpisů
      kategorií, kolik commitů do ní přispělo (Unreleased 10 místo 2), protože
      každý commit přidával vlastní blok „### Přidáno" - položky se teď slévají
      do prvního výskytu kategorie. Ověřeno i na vygenerovaném webu
      (`astro build` → `dist/docs/changelog/index.html`: 5 nadpisů `h3` shodně
      s aplikací, 3× `<strong>`, 0× doslovné `**`).

## Zpětná vazba z živého používání (2026-07-28, nahlásil uživatel)
Vzniklo z reálného vyzkoušení portable buildu `0.2.0+7f2d64b`. Řazeno podle
závažnosti, ne podle pořadí nahlášení.

- [ ] **Zamrznutí po zrušení kontroly** - NEREPRODUKOVÁNO, potřebuje od uživatele
      upřesnit. Tlačítka „Spustit kontrolu"/„Zrušit" jsou v okně Kontrola integrity
      (`chkdsk /scan`), ne v povrchovém skenu. Změřeno harnessem nad SKUTEČNOU instancí
      `IntegrityWindow` (dispatcher timer po 50 ms, sleduje se nejdelší mezera mezi tiky):
      po zrušení nejdelší zámrz 15-16 ms a `IsScanning=false` do 78 ms, a to jak na
      USB svazku H:, tak na systémovém C:. Žádné zamrznutí.
      Ověřením VYVRÁCENÉ hypotézy (ať se k nim nikdo nevrací):
      1. „Povrchový sken běží na UI vlákně, protože `FileStream` nemá `isAsync: true`" -
         změřeno před/po opravě: 79 % vs. 80 % odezvy UI, tedy beze změny. Důvod:
         `Stream.ReadAsync` i nad synchronním `FileStreamem` odkládá práci na thread pool,
         takže UI vlákno neblokuje. Změna byla zahozena jako zbytečná.
      2. „Zahlcení hlášením postupu jako u `DiskUsageScanner` ve Fázi 4" - sken NVMe
         rychlostí ~3,75 GB/s (≈940 hlášení/s) drží odezvu na 80 %.
      3. „`TryKill(process)` běží na UI vlákně, protože `WaitForExitAsync` nemá
         `ConfigureAwait(false)`" - zabití chkdsk trvalo 62-78 ms včetně VSS snapshotu.
      Co se ještě neprověřilo: chování v reálné aplikaci s otevřeným hlavním oknem,
      tray ikonou a `DiskHealthNotifier` na pozadí (harness má jen jedno okno). Potřeba
      od uživatele: který disk/svazek, jestli běžela s admin právy, a jestli zamrzlo
      hned po startu kontroly nebo až po kliknutí na Zrušit.
- [x] **Diskora nenabídne nic u disku, se kterým Windows pracovat umí** - OPRAVENO: u USB disku
      (svazek H:) vrací `DeviceSeekPenaltyProperty` null, takže se podle pravidla „při
      nejistotě nenabízet nic" schová TRIM i defragmentace - a to i s admin právy.
      Jenže Windows ve svém „Optimalizovat jednotky" ten disk analyzovat i defragmentovat
      nabízí. Že to není omezení systému, je ověřené: `defrag.exe /D` na H: reálně
      proběhl přes `DiskOptimizationService` (`ExitCode=0`, kompletní Pre i Post report).
      Takže operace funguje, jen ji UI odmítá nabídnout. Návrh: při nezjištěném typu
      nenabízet mlčení, ale zeptat se disku jinak (WMI `MSFT_PhysicalDisk.MediaType`,
      `defrag /A` analýza) a teprve když ani to nepomůže, nabídnout obě akce
      s upozorněním, že typ se nepodařilo určit. Rozhodně nesmí zůstat stav
      „Diskora neumí nic, Windows umí".
      OPRAVA: `DiskOptimizationService.GetCapabilities` si při mlčícím IOCTL vyžádá druhý
      názor z WMI (`MSFT_PhysicalDisk.MediaType`, 3=HDD/4=SSD). Záměrně se NEKOUKÁ na
      `SpindleSpeed` - u testovaného USB disku je 0, což by ho falešně prohlásilo za SSD.
      Když ani WMI typ nezná (u USB disku hlásí `Unknown`), UI už neschová všechno:
      nabídne obě akce plus upozornění, že typ není známý, TRIM na talířovém disku nic
      nezkazí a defragmentaci má uživatel spouštět jen když ví, že jde o talířový disk.
      5 testů. Živě ověřeno na třech reálných svazcích: H: (USB, neznámý typ) nabídne
      TRIM i defragmentaci s upozorněním, F: (HDD) jen defragmentaci, C: (SSD) jen TRIM.
- [x] **Okno kontroly integrity se samo posouvá na poslední řádek** - HOTOVO:
      `IntegrityWindow.OutputScroll_ScrollChanged` drží pohled na konci výpisu. Rozlišuje
      se podle `ExtentHeightChange`: nenulová = přibyl řádek (posunout, pokud sledujeme
      konec), nulová = roloval uživatel (podle toho se sledování zapne/vypne). Takže
      jakmile si uživatel odroluje nahoru číst starší řádek, výpis mu už neuteče, a po
      návratu na konec se sledování samo obnoví.
- [ ] **České výstupy z orchestrovaných nástrojů**: `chkdsk`/`defrag` píšou anglicky bez
      ohledu na jazyk Windows. `ChkdskOutputParser` už dnes mapuje fáze na české popisky
      pro progress bar; rozšířit stejný princip i na samotný výpis - překládat známé řádky
      (souhrny, „The operation completed successfully", tabulky Pre/Post reportu defragu)
      a neznámé nechávat v původním znění. Syrový anglický log ponechat jako přepínatelný
      detail, ať jde dohledat původní text. Souvisí s položkou „Lokalizace" ve Fázi 8,
      ale je použitelná i samostatně - appka je celá česky, takže anglický výpis uprostřed
      je nekonzistence, i když pochází z cizího nástroje.

## Nápady na budoucí odlišení (backlog, needvidí se hned)
- [x] "Disk Doctor" wizard (jedno tlačítko: SMART + chkdsk + TRIM/defrag rozhodnutí):
      tlačítko „Disk Doctor" u každého svazku v dashboardu + CLI `diskora doctor
      <písmeno>`. Rozhodovací logika je čistá funkce `DiskDoctorAdvisor.Diagnose`
      nad `DiskDoctorInputs` (S.M.A.R.T. + dirty bit + zjištěný typ disku +
      informace o elevaci), takže jde otestovat bez disků, bez elevace a bez
      čekání - 16 testů. `DiskDoctorService` jen posbírá data z už existujících
      ověřených služeb; žádná nová cesta k datům nevzniká.
      Doctor je vědomě POUZE diagnostický - sám nic nespouští. Akce jen nabízí a
      tlačítko otevře příslušné existující okno, kde má akce vlastní potvrzení
      (spotfix a defragmentace skutečně zapisují na disk, viz Fáze 3). Při
      nejistém typu disku nenabízí ani TRIM, ani defragmentaci - stejné pravidlo
      jako v okně Optimalizace.
      Odlišující prvek proti pouhému „disk je v pořádku": nález se vytáhne i tam,
      kde ho souhrnný verdikt skrývá. Živě potvrzeno na reálném 4TB HDD (F:) -
      S.M.A.R.T. hlásí „v pořádku", ale Doctor navíc upozorní na 2426 chyb
      přenosu (atribut 199), vysvětlí, že jde skoro vždy o vadný SATA kabel a ne
      o vadný disk, a dodá, že číslo je součet za celý život disku a nikdy
      neklesá. Atribut 199 totiž nezhoršuje normalizovanou hodnotu, takže by
      jinak zapadl.
      Živě ověřeno na 4 reálných svazcích, s elevací i bez: NVMe systémový C:
      (zdravý, čistý, SSD → TRIM), 4TB HDD F: (kabel + defragmentace), SATA SSD
      E: a USB disk H:. Bez elevace korektně hlásí „bez práv administrátora je
      tahle část kontroly slepá" a doporučí elevaci; s elevací u USB mostu
      elevaci UŽ nenavrhuje a místo toho vysvětlí, že disk data neposkytuje.
      Ověřeno CLI i skutečnou instancí `DiskDoctorWindow` v izolovaném harness
      (počty zjištění, závažnosti a viditelnost akčních tlačítek).
      Živé testování zároveň odhalilo chybu v novém CLI příkazu: `VolumeInfo.Name`
      má tvar `E:\`, ale `NormalizeDriveLetter` dává `E:`, takže porovnání
      nenašlo ŽÁDNÝ svazek - opraveno.
- [x] Portable mód (single-exe bez instalace) - viz Fáze 9, `app/publish-portable.ps1`
- [ ] **Test skutečné kapacity (falešné USB disky)**: zapsat po celém disku ověřitelný
      vzor a přečíst zpátky - odhalí přeznačené flashky, které hlásí 1 TB a fyzicky mají
      32 GB (dnes to řeší H2testw/FakeFlashTest, tedy anglické jednoúčelové nástroje bez
      údržby). Diskora na to má skoro všechno hotové: `PhysicalDiskSurfaceScanner` už umí
      sekvenčně projít celý disk po blocích s postupem a zrušením, stačí doplnit zápisovou
      větev. DESTRUKTIVNÍ (přepíše obsah), takže by to chtělo stejné potvrzení jako
      spotfix, plus výslovné vypsání, co se smaže. Nejsilnější kandidát na odlišení -
      je to reálný problém, který lidi hledají, a nikdo to nenabízí v češtině a jako
      součást normálního diskového nástroje.
- [ ] **„Kolik času disku zbývá"**: z SQLite historie (už se plní) spočítat tempo
      zhoršování a přeložit ho do věty, které rozumí i laik - u NVMe je to přímočaré
      (`PercentageUsed` v čase → odhad, kdy dosáhne 100 %), u SSD přes atribut 233,
      u HDD přes přírůstek přemapovaných/čekajících sektorů. Konkurence (CrystalDiskInfo
      apod.) ukáže číslo a mlčí; tohle je přesně to, co uživatel opravdu chce vědět.
      Musí být poctivé - u disku bez trendu nebo s krátkou historií raději říct „zatím
      nemám dost dat" než vymýšlet číslo.
- [ ] **Graf trendu zdraví v čase**: historie v SQLite existuje od Fáze 2, ale zobrazuje
      se jen jako tabulka. Graf udělá z „bylo Healthy, je Warning" viditelný příběh.
      Vlastní vykreslování do Canvasu stejným způsobem jako treemapa (žádná nová
      závislost), paleta dle skillu dataviz.
- [ ] **Report pro člověka (HTML/PDF), ne jen CSV/JSON**: jedna stránka „stav mých disků"
      se srozumitelným shrnutím a doporučeními, kterou jde poslat příbuznému nebo
      ITčkaři. Exporty dnes míří na skriptování; tohle míří na komunikaci. Navazuje
      přímo na Disk Doctora - ten už ta doporučení umí sestavit.
- [ ] Plugin architektura pro další filesystémy (ReFS, exFAT, ext4 přes WSL disky)
- [ ] Instalátor (Inno Setup/MSIX) - vědomě odsunuto z aktivních fází do backlogu
      (rozhodnutí uživatele): portable single-exe build (Fáze 9) pokrývá hlavní
      potřebu distribuce bez instalace, takže instalátor teď nepřináší dost
      navíc na to, aby byl prioritou. Šlo by k němu vrátit později, hlavně pokud
      by bylo žádoucí Start Menu zástupce, položku v "Přidat nebo odebrat
      programy" nebo automatickou registraci naplánované úlohy (`schedule
      install`) při instalaci - single-exe nic z toho neřeší, uživatel si
      spouští/registruje ručně.
