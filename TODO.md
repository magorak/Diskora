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
- [ ] Přechod/doplnění na `IOCTL_ATA_PASS_THROUGH` (spolehlivější u některých řadičů)
- [ ] NVMe health log (`IOCTL_STORAGE_QUERY_PROPERTY` / protocol-specific)
- [x] Dekódování atributů + srozumitelná vysvětlení rizika (odlišující prvek) - `SmartAttributeCatalog`
- [x] Health skóre (`SmartHealthEvaluator`, testováno) a graceful degradace, když SMART není dostupné
- [x] Minimální SMART UI (tlačítko u disku → okno s atributy, rizikem a celkovým verdiktem)
- [x] `Diskora.Data`: SQLite historie SMART hodnot (`SqliteDiskHistoryStore`, tabulka historie
      v okně S.M.A.R.T.) - živě ověřeno; grafické trendy (graf v čase) zatím ne, jen tabulka
- [ ] Upozornění/notifikace při zhoršení zdraví (návaznost na Fázi 7 - tray)

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
- [ ] Skutečná oprava (`/f`, `/spotfix`) - záměrně zatím nepropojeno, potřebuje vlastní
      potvrzovací UI (riziko naplánovaného restartu na systémovém svazku)
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
      sdílí ověřenou cestu s TRIM, nebylo živě testováno na skutečném HDD (žádný
      k dispozici v testovacím prostředí)
- [ ] Vlastní analýza fragmentace (`FSCTL_GET_RETRIEVAL_POINTERS`) pro report před spuštěním
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
- [ ] Integrace s Windows Task Scheduler (periodické kontroly zdraví)
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
- [ ] Export kompletních reportů (PDF/CSV/JSON)
- [ ] Nastavení (téma, jazyk, chování elevace, prahy notifikací)
- [ ] Lokalizace (cs-CZ, en-US)
- [ ] Přístupnost (screen reader labels, klávesová navigace, vysoký kontrast)

## Fáze 9 — Bezpečnost a release engineering
- [ ] `SECURITY.md` — threat model, responsible disclosure
- [ ] Statická analýza (Roslyn analyzery) + CodeQL v CI
- [ ] Dependency scanning (Dependabot/Renovate)
- [ ] Code signing pipeline (Authenticode) pro release buildy
- [ ] Instalátor (Inno Setup/MSIX) + portable build
- [ ] SBOM generování

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
- [ ] Changelog page (synchronizace s `CHANGELOG.md`)
- [ ] Stránka ochrana soukromí (rozšíření sekce Bezpečnost v nápovědě)
- [ ] Migrace nápovědy na Starlight, pokud obsah naroste natolik, že se vyplatí
      full-text search a auto-generovaný sidebar
- [ ] CI: build + deploy, kontrola bezpečnostních hlaviček/CSP

## Fáze 11 — Průběžná dokumentace a verzování
- [ ] Udržovat `docs/ARCHITECTURE.md`, `docs/CONTRIBUTING.md` aktuální
- [ ] SemVer tagy + `CHANGELOG.md` per release
- [ ] In-app "Co je nového" propojené s webovou dokumentací per verze

## Nápady na budoucí odlišení (backlog, needvidí se hned)
- [ ] "Disk Doctor" wizard (jedno tlačítko: SMART + chkdsk + TRIM/defrag rozhodnutí)
- [ ] Portable mód (single-exe bez instalace)
- [ ] Plugin architektura pro další filesystémy (ReFS, exFAT, ext4 přes WSL disky)
