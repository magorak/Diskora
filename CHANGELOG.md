# Changelog

Formát vychází z [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
verzování dle [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Přidáno
- Přístupnost - popisky pro čtečku obrazovky u průběhových pruhů (Fáze 8,
  částečně): všech 5 `ProgressBar` (zaplněnost svazku, podíl složky, průběh
  kontroly integrity/opravy, průběh povrchového skenu) dostalo
  `AutomationProperties.Name` s aktuální hodnotou - dřív byly bez textového
  obsahu pro čtečku obrazovky neviditelné. Živě ověřeno přes UI Automation na
  dashboardu (před/po). Vlastní buňky treemapy zaplněnosti zůstávají zatím bez
  automation peer (potřebovaly by přepsat na `Button` s vlastní šablonou) -
  vedlejší záložka „Složky" (DataGrid) se stejnými daty je ale plně přístupná.
- CodeQL a Dependabot v CI (Fáze 9): `.github/workflows/codeql.yml` (analýza C#
  na push/PR do master i týdně navíc) a `.github/dependabot.yml` (nuget pro
  `/app`, npm pro `/web`, github-actions pro workflow soubory, týdenní interval).
  Roslyn analyzery už běžely od Fáze 0. Stejné omezení jako CI pipeline z Fáze 0 -
  repozitář nemá GitHub remote, takže se nemohly spustit naživo, jen YAML syntax
  ověřena lokálně přes `js-yaml`.
- Integrace s Windows Plánovačem úloh (Fáze 7): nové CLI příkazy `diskora
  healthcheck` (S.M.A.R.T. přes všechny fyzické disky najednou) a `diskora
  schedule install/remove/status` (`Diskora.Repair.ScheduledTaskManager`,
  orchestrace `schtasks.exe`). Bez `/RU`/`/RP` běží úloha pod aktuálním
  uživatelem bez admin práv, i když GUI vůbec neběží - doplňuje `DiskHealthNotifier`
  z Fáze 2, který kontroluje jen za běhu GUI. Živě ověřeno celým cyklem
  install → status (i nezávisle přes `Get-ScheduledTask`) → remove → potvrzeno
  smazáno.
- Skutečná oprava integrity - spotfix (Fáze 3): `ChkdskRunner.RunSpotFixAsync`
  (`Repair-Volume -SpotFix` orchestrace, stejný vzor jako `IsoMounter`). Vědomě
  jen spotfix (Windows 8+ online self-healing), ne `/f`/`/r` - ty by na
  systémovém/uzamčeném svazku potřebovaly naplánovaný restart, samostatný a
  složitější UX problém ponechaný na příště. Nové tlačítko „Opravit (spotfix)"
  v okně Kontrola integrity s vlastním potvrzovacím dialogem PŘED zápisem -
  `MessageBoxResult.No` je záměrně výchozí, aby náhodný Enter/mezerník
  nemohl omylem potvrdit akci, která skutečně zapisuje na disk.

### Opraveno
- `ChkdskRunner.RunSpotFixAsync`: bez admin práv `Repair-Volume` selhával jen
  jako NEterminating chyba, takže skript to tiše prohlásil za úspěch (prázdný
  `HealthStatus`, exit 0) - opraveno explicitní kontrolou výsledku. Živě
  odhaleno při testování - jeden běh nechtěně skutečně proběhl (dopad ověřen
  jako nulový, bez admin práv Windows operaci zablokoval dřív, než mohla
  cokoliv zapsat), což zároveň odhalilo, že „Ano" bylo výchozí tlačítko
  potvrzovacího dialogu - přepnuto na „Ne" jako výchozí.

### Přidáno
- Export do CSV/JSON napříč okny (Fáze 8): dřív mělo export jen okno Analýza
  zaplněnosti. Přidáno do S.M.A.R.T., Kontrola integrity, Povrchový sken a
  Systémový protokol - sdílené přes nový `Diskora.App.Export.ExportHelper`
  místo duplikování dialogu/zápisu/ošetření chyby v každém okně zvlášť.
  `SurfaceScanViewModel` dostal veřejnou `BadRanges` property (syrová
  offset/délka data) pro strukturovaný JSON export. Živě ověřeno (UI
  Automation): Systémový protokol export CSV i JSON nad reálnými daty tohoto
  stroje, S.M.A.R.T. export obou formátů i nad prázdnými daty bez pádu.
- Perzistence volby tématu (Fáze 8): `Diskora.App.Settings.JsonAppSettingsStore`
  (JSON v `%LocalAppData%\Diskora\settings.json`). `ThemeService.Apply` volbu při
  každém přepnutí uloží, `App.OnStartup` ji při startu načte zpátky - dřív se
  vždy startovalo na "Podle systému" bez ohledu na poslední volbu. 9 testů.
  Živě ověřeno: přepnutí na Světlé → restart appky → naběhne světlé i na stroji
  se systémovým tmavým tématem.
- Analýza fragmentace souborů (Fáze 5): `Diskora.Native.Storage.
  FileFragmentationReader` (`FSCTL_GET_RETRIEVAL_POINTERS`, bez admin práv -
  stačí právo číst soubor) + `Diskora.Core.Services.FragmentationAnalysisService`
  (jednovláknový průchod stromem, paralelní čtení jednotlivých souborů, stejný
  vzor jako `DuplicateFileFinder`). Nové tlačítko „Analyzovat fragmentaci" a
  záložka „Fragmentace" v okně Optimalizace disku, viditelné jen pro HDD. 5
  testů nad reálnými soubory a skutečným IOCTL voláním. Průchod UI na
  skutečném HDD nebyl živě ověřen - v tomto prostředí jsou k dispozici jen
  SSD disky.
- Upozornění na zhoršení zdraví disku (Fáze 2): `DiskHealthChangeDetector` +
  `DiskHealthMonitor` (porovnají nový SMART výsledek s posledním záznamem
  historie, 19 testů) a `DiskHealthNotifier`, který v `MainWindow` periodicky
  (30 min, hned i po startu) kontroluje disky na pozadí (`Task.Run`, neblokuje
  UI) a při zhoršení zobrazí balónek přes tray ikonu. Živě ověřeno jen
  zapojení - v tomto vývojovém prostředí SMART bez admin práv vůbec nejde
  číst, takže samotné zobrazení balónku při reálné degradaci ověřit nešlo.
- Tray ikona (Fáze 7): `Diskora.App.Tray.TrayIconService` (`System.Windows.Forms.
  NotifyIcon` přes `UseWindowsForms`, bez externí závislosti). Ikona vidět po celou
  dobu běhu, kontextové menu „Zobrazit Diskoru"/„Konec", dvojklik obnoví okno;
  minimalizace okno schová i z hlavního panelu, zavření (×) aplikaci normálně
  ukončí. Živě ověřeno (UI Automation + screenshoty). Cestou odstraněny
  auto-přidané global usingy `System.Windows.Forms`/`System.Drawing` z
  `Diskora.App.csproj` - kolidovaly s WPF `Application`/`Color` napříč celým
  projektem.
- Treemapa zaplněnosti (Fáze 4): nová záložka „Mapa" v okně Analýza zaplněnosti
  vedle Složek/Největší souborů/Nejstarší souborů/Duplicit -
  `Diskora.Core.Layout.SquarifiedTreemapLayout` (čistě geometrický port
  algoritmu Bruls/Huizing/van Wijk 2000, 11 testů) vykreslený jako Canvas v
  `DiskUsageWindow`. Barva buňky kóduje velikost sekvenčně (jedna barva,
  světlá→tmavá dle podílu - viz skill dataviz), klik na buňku = drill-down.
  Živě ověřeno (UI Automation + screenshoty, `app/src` → `Diskora.App` →
  `bin`) včetně resize okna. Live testování odhalilo skutečný bug sdílený s
  existujícím kompozičním pruhem - obě vizualizace počítaly barvu jako
  statický `SolidColorBrush` místo `DynamicResource`, takže při přepnutí
  světlé/tmavé téma za běhu zůstávaly zamrzlé ve starém tématu; opraveno
  novým `ThemeService.ThemeChanged` eventem.
- Upozornění na osiřelé připojené virtuální disky (Fáze 6): Diskora připojuje
  VHD/VHDX s `ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME` (viz oprava níže),
  takže disk zůstává připojený i po pádu aplikace nebo zavření bez ručního
  odpojení. Nový `IVirtualDiskAttachmentRegistry` /
  `Diskora.Data.SqliteVirtualDiskAttachmentRegistry` (vlastní tabulka ve
  stejné `diskora.db`) sleduje, co `VirtualDiskService.Attach`/`MountIsoAsync`
  úspěšně připojily a co `Detach`/`DismountIsoAsync` zase odpojily. Při
  příštím startu aplikace se zbylé záznamy (soubor, který podle registru
  zůstal připojený z minula) ukážou v informačním dialogu s odkazem na menu
  „Otevřít virtuální disk / ISO..."; záznamy, jejichž soubor mezitím zmizel,
  se tiše promažou. 8 nových testů (`Diskora.Core.Tests`,
  `Diskora.Data.Tests`) - samotné připojení/odpojení vyžaduje admin práva,
  které v tomto vývojovém prostředí nejsou k dispozici, takže reálný scénář
  "zavřít Diskoru s připojeným diskem a znovu ji spustit" nebyl živě ověřen;
  ověřeno bylo, že neúspěšné připojení/odpojení (chybějící soubor) registr
  nijak nezasáhne a že aplikace se startovní kontrolou stále čistě nastartuje
  a ukončí.
- CI pipeline (Fáze 0): `.github/workflows/build.yml` - `windows-latest`,
  `actions/setup-dotnet` na `10.0.x`, `dotnet restore/build/test` nad
  `app/Diskora.slnx` v konfiguraci Release. Repozitář zatím nemá GitHub
  remote, takže se sama pipeline nemůže spustit na GitHubu, ale přesně tahle
  posloupnost příkazů byla živě ověřena lokálně (74 testů, 0 chyb).
- IMG/raw obrazy - read-only inspekce rozvržení (Fáze 6): Windows raw `.img`
  neumí připojit jako jednotku (živě ověřeno - `Mount-DiskImage` je odmítne,
  `virtdisk.dll` pro ně nemá kontejner k rozpoznání), proto místo mountu nový
  `Diskora.VirtualDisks.RawImageInspector` čte MBR/GPT tabulku oddílů přímo
  ze souboru (bez admin práv). `VirtualDiskWindow` dostal třetí větev vedle
  VHD/VHDX a ISO - tlačítko „Prozkoumat rozvržení" ukáže schéma (MBR/GPT) a
  počet oddílů. Živě ověřeno na reálných discích s MBR i GPT rozvržením
  (2 oddíly, obojí správně rozpoznáno) včetně celé cesty přes
  `VirtualDiskService` s příponou `.img`.
- Read-only povrchový sken vadných sektorů (Fáze 3): nový
  `Diskora.Native.Storage.PhysicalDiskSurfaceScanner` čte celý fyzický disk
  sekvenčně po 4MiB blocích (`\\.\PhysicalDriveN`, vyžaduje admin práva) a
  hlásí bajtové rozsahy, které se nepodařilo přečíst (upřesněné na 64KiB
  granularitu) - needestruktivní, nic nezapisuje ani neopravuje.
  `Diskora.Core.Services.SurfaceScanService` a nové okno „Povrchový sken
  disku" (tlačítko u řádku fyzického disku v dashboardu) s průběžným
  procentuálním postupem a možností zrušení. Živě ověřeno s admin právy:
  4GB testovací disk proskenován za ~1 s beze zjištěných vadných oblastí,
  zrušení uprostřed skenu (přes `CancellationToken`) na reálném 238GB disku
  funguje čistě. Cestu se skutečně nalezenou vadnou oblastí se nepodařilo
  živě ověřit - žádný disk s reálně vadnými sektory není v tomto prostředí
  k dispozici.

### Opraveno
- `Diskora.VirtualDisks.VirtualDiskAttacher`: `Attach` neposílal
  `ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME`, takže se připojený VHD/VHDX
  tiše zase odpojil hned po volání, jakmile se zavřel handle použitý pro
  samotný `AttachVirtualDisk` (`Success=True`, ale svazek/disk se ve
  skutečnosti nikdy nezpřístupnil). Dřív se to nedalo odhalit - živě ověřená
  byla jen cesta selhání bez admin práv (Win32 chyba 1314), ne skutečné
  připojení. Odhaleno a opraveno až s elevovaným živým testem: po přidání
  flagu disk zůstává připojený, dostane písmeno a jde znovu odpojit. Zároveň
  živě ověřeno, že nad takto připojeným virtuálním diskem beze změny fungují
  `IntegrityCheckService` (dirty bit i celý `chkdsk /scan`), `DiskUsageScanner`
  a `SmartService` (korektně a srozumitelně hlásí, že SMART na virtuálním
  disku není k dispozici).
- `Diskora.VirtualDisks.VirtualDiskAttacher`: opětovné připojení již připojeného
  VHD/VHDX (typicky po pádu/zavření Diskory bez explicitního odpojení) vracelo
  jen syrové "Win32 chyba 32" - přidán srozumitelný český popis
  (ERROR_SHARING_VIOLATION → disk je už otevřený/připojený jinde, nejdřív ho
  odpojte). Živě ověřeno dvojím připojením stejného testovacího VHDX.

### Přidáno
- CLI společník `diskora.exe` (Fáze 7): nový projekt `Diskora.Cli`, headless
  doplněk ke GUI pro skriptování a automatizaci. Příkazy `list`, `smart
  <index>`, `integrity <písmeno> [--scan]`, `usage <cesta> [--top N]`,
  `duplicates <cesta>` - všechny skládají už existující a otestované
  `Diskora.Core` služby (žádná nová byznys logika, jen nová prezentační
  vrstva). Globální `--json` přepínač pro strojově čitelný výstup (čitelná
  diakritika, enumy jako řetězce). `smart`/`integrity` zapisují do stejné
  sdílené SQLite historie jako GUI. Smysluplné exit kódy pro skriptování
  (0/1/2/130). Živě ověřeno - všechny příkazy, člověku čitelný i JSON výstup,
  reálná data ze skutečných disků/svazků včetně skutečně vytvořené duplicity.
- Export do JSON (Fáze 4): tlačítko „Exportovat JSON..." vedle „Exportovat CSV..."
  v okně Analýza zaplněnosti, exportuje aktuálně zobrazenou záložku (Složky/
  Největší soubory/Nejstarší soubory/Duplicity) přes `System.Text.Json` se
  `JavaScriptEncoder` omezeným na Basic Latin + Latin-1 Supplement + Latin
  Extended-A, aby česká diakritika zůstala v souboru čitelná místo `\uXXXX`
  escapů. Živě ověřeno reálným exportem se skutečnými daty.

### Opraveno
- `TabControl`/`TabItem` (okno Analýza zaplněnosti): stejná třída bugu jako
  dřív u Menu/DataGrid - výchozí šablona nebere Background/Foreground z
  DynamicResource, takže hlavičky záložek ("Složky"/"Největší soubory"/
  "Nejstarší soubory") zůstávaly bílé a nečitelné i v tmavém tématu. Nahlásil
  uživatel po vyzkoušení přebuildované aplikace. Opraveno vlastní "underline"
  šablonou (vybraná záložka má spodní pruh v AccentBrush) - živě ověřeno
  v tmavém i světlém režimu.

### Přidáno
- Hledač duplicit (Fáze 4): `Diskora.Core.Services.DuplicateFileFinder` -
  dvoufázový hash-based přístup (nejdřív zdarma seskupí podle velikosti
  souboru, teprve kandidáty se shodnou velikostí hashuje SHA-256 paralelně).
  Procházení stromu je záměrně jednovláknové - na rozdíl od `DiskUsageScanner`
  zde paralelní rekurze nestojí za riziko (viz předchozí dvě opravy
  souběžnosti), skutečné těžiště (hashování) je paralelizované samostatně
  a bezpečně (plochý seznam, žádná rekurze). Nová záložka „Duplicity" v okně
  Analýza zaplněnosti - read-only, nic se nemaže (skutečné čištění by
  potřebovalo vlastní potvrzovací UI, stejně jako `chkdsk /f`). 6 nových
  testů + živě ověřeno reálným duplicitním souborem na testovacím svazku.
- Výběr libovolné složky ke skenování (Fáze 4): menu Soubor → „Analyzovat
  složku..." otevírá `Microsoft.Win32.OpenFolderDialog` a spustí Analýzu
  zaplněnosti nad libovolnou složkou, ne jen kořenem svazku. Mimochodem
  opravena kolidující mnemonika v menu Soubor („Obnovit"/„Otevřít" obě O).
  Živě ověřeno na `C:\Projekt\Diskora\docs`.
- Export do CSV (Fáze 4): `Diskora.Core.Export.CsvWriter` (RFC 4180 escapování
  čárek/uvozovek/nových řádků, 5 testů) a tlačítko „Exportovat CSV..." v okně
  Analýza zaplněnosti - exportuje aktuálně vybranou záložku (Složky/Největší
  soubory/Nejstarší soubory) přes standardní `SaveFileDialog`. Živě ověřeno:
  skutečný export z reálného svazku, správné escapování (česká lokalizace
  velikosti obsahuje čárku, correctně obalena uvozovkami v CSV).
- Vícevláknový sken zaplněnosti disku (Fáze 4): `DiskUsageScanner` teď skenuje
  sourozenecké podsložky souběžně přes `Task.Run` + `SemaphoreSlim`
  (`Environment.ProcessorCount * 2` souběžných I/O operací). Sken celého `C:\`
  (437 311 souborů, 150 579 složek, 101 GB) teď doběhne za ~48-76 s - dřív se
  jednovláknová verze na tomto svazku nedokončila ani po 4 minutách.

### Opraveno
- `DiskUsageScanner`: první verze paralelizace držela permit ze semaforu i během
  čekání na potomky, což je prioritní inverze (rodič čeká na potomka, který ale
  potřebuje permit ze stejné fronty) - hluboké větve stromu se tak uměly zaseknout
  na řádově minuty navíc, hůř než bez jakékoli paralelizace. Opraveno: permit se
  drží jen po dobu synchronního I/O jedné složky, uvolní se PŘED rekurzí do potomků.
- `DiskUsageScanner`: i po výše uvedené opravě GUI sken velkého svazku (na rozdíl
  od izolovaného testu přímo nad Diskora.Core) trval přes 3 minuty navíc PO
  dokončení skutečné I/O práce - hlášení postupu pro každou z 150 tisíc+ navštívených
  složek zaplavovalo UI vlákno (každé volání skrz binding spouští drahé globální
  `CommandManager.InvalidateRequerySuggested()`). Opraveno prahováním hlášení na
  max. 10x/s (`ThrottledProgressReporter`).
- `ThrottledProgressReporter`: throttler inicializovaný na `long.MinValue` přetékal
  při prvním porovnání (`Environment.TickCount64 - long.MinValue` přesahuje
  `long.MaxValue` a wrapne se na zápornou hodnotu), takže by tiše zahodil úplně
  všechna hlášení postupu navždy - odhaleno existujícím regresním testem
  (`ScanAsync_ReportsProgressForEachDirectoryVisited`), opraveno inicializací na
  bezpečnou hodnotu odvozenou od aktuálního tick countu.
- Hledač velkých a starých souborů (Fáze 4): `DiskUsageScanner` nyní během skenu
  zároveň sleduje 20 největších a 20 nejstarších souborů v celém stromu přes nový
  `BoundedTopTracker` (udržuje jen N položek seřazených podle komparátoru, žádná
  alokace na soubor navíc - paměť neškáluje s počtem souborů na disku). Okno
  Analýza zaplněnosti dostalo záložky „Největší soubory“ a „Nejstarší soubory“
  vedle stávající „Složky“ (`FileUsageRowViewModel`, nové modely `FileUsageEntry`/
  `DiskUsageScanResult` v Diskora.Core). Živě ověřeno na reálném svazku E:\ -
  správné řazení (100 MB → 129 B sestupně; nejstarší → nejnovější vzestupně).
  Zjištěno i empiricky potvrzeno, že jednovláknový sken neškáluje na velké svazky
  (sken celého C:\ v tomto prostředí neskončil ani po 4 minutách) - zapsáno do
  TODO.md jako prioritní položka pro vícevláknové zrychlení.
- Čtení Event Logu (Fáze 3): nový `Diskora.Native.EventLog.DiskEventLogReader`
  (`System.Diagnostics.Eventing.Reader.EventLogReader`, dotazuje protokoly System
  a Application XPath filtrem na providery Ntfs/Disk/Volsnap/Virtual Disk Service/
  FilterManager/Wininit) a `Diskora.Core.Services.DiskEventLogService`, které
  namapuje `EventRecord.Level` na doménový `DiskEventLevel` (7 nových testů
  `DiskEventLevelMapperTests`). Nové okno „Systémový protokol" (menu Nástroje →
  Systémový protokol (disky)...) zobrazuje posledních 50 relevantních událostí
  s barevným odznakem úrovně. Read-only, žádná elevace potřeba - živě ověřeno:
  otevřené okno na tomto stroji skutečně ukázalo reálné události (Ntfs event 98
  "Svazek E: je v pořádku" vzniklý přímo z vlastní dirty-bit kontroly Diskory,
  Volsnap 33, Virtual Disk Service 3/4), správně česky lokalizované díky cs-CZ
  systémové locale (na rozdíl od chkdsk, který je natvrdo anglicky).
- Oprava kolidující přístupové klávesy v horním menu - „Nástroje" a „Nápověda"
  obě používaly podtržené N, což způsobovalo nejednoznačnou mnemoniku (Alt+N
  nešlo spolehlivě otevřít přes klávesnici); opraveno na „Nás_troje" (T).
- Produktový web (Fáze 10, první řez): nový podprojekt `web/` (Astro 7 + Tailwind v4),
  bez telemetrie/trackerů, `noindex` dokud nebude veřejné vydání. Landing page se
  čtyřmi pilíři (skutečné screenshoty ze živého testování, ne makety), upřímným
  hero (verze 0.1.0, žádné falešné tlačítko ke stažení) a sekcí o bezpečnostní
  filozofii. Nápověda (`/docs/`) jako vlastní lehké Astro stránky se sdíleným
  `DocsLayout` (sidebar navigace) místo plného Starlight - pro aktuální rozsah
  obsahu jednodušší a vizuálně konzistentní s landing page; tři reálné podstránky
  (Kontrola integrity, Analýza zaplněnosti, Bezpečnost a oprávnění), každá se
  screenshotem a odkazem na živě ověřené chování z appky. Favicon/logo sdílené
  s ikonou `Diskora.App`. Živě ověřeno: `npm run build` (5 stránek, bez chyb) a
  `astro preview` (všech 5 cest vrací HTTP 200).
- ISO podpora (Fáze 6): `Diskora.Repair.IsoMounter` orchestruje `Mount-DiskImage`/
  `Dismount-DiskImage` (cesta k souboru jde přes proměnnou prostředí, ne interpolaci
  do příkazu). Okno Virtuální disk teď rozpozná i `.iso` a nabídne Připojit/Odpojit
  obraz - live ověřeno, funguje bez admin práv (na rozdíl od VHD/VHDX). Přímé
  `AttachVirtualDisk` s VIRTUAL_STORAGE_TYPE_DEVICE_ISO bylo živě otestováno a
  zdokumentováno jako nefunkční (vrátí úspěch, ale bez souborového systému), proto
  orchestrace přes ověřený cmdlet.
- Dokončení Fáze 1: mapování svazek → fyzický disk (WMI asociátorový řetězec
  Win32_LogicalDisk → ... → Win32_DiskDrive, živě ověřeno) a barevné odznaky
  typu disku (SSD/HDD/vyměnitelný/virtuální) u fyzických disků i svazků
  v dashboardu - nový sloupec "Typ disku" u svazků.
- Lokální historie (Fáze 2 a 3 - dokončení): nový projekt `Diskora.Data` se
  `SqliteDiskHistoryStore` (SQLite v `%LocalAppData%\Diskora\diskora.db`, žádný
  cloud/účet). `SmartService` a `IntegrityCheckService` teď volitelně zapisují
  každé čtení/kontrolu do historie; okna S.M.A.R.T. a Kontrola integrity zobrazují
  posledních 20 záznamů (barevně odlišený stav, u kontrol i výsledek skenu).
  10 nových testů (`Diskora.Data.Tests`), živě ověřeno na reálném svazku E: i
  fyzickém disku - historie se persistentně ukládá a znovu načítá napříč spuštěními.
- UI vylepšení (drobnosti na žádost uživatele):
  - `ChkdskOutputParser` (Diskora.App) rozpoznává "Stage N:"/"N percent complete"
    v anglickém výstupu chkdsk (ten je pevně anglický bez ohledu na jazyk Windows -
    ověřeno živě) a pohání český popisek fáze + grafický progress bar v okně
    Kontrola integrity; syrový log zůstává jako doplňkový detail. 15 nových testů
    (`Diskora.App.Tests`, nový projekt).
  - Kompoziční pruh + legenda v okně Analýza zaplněnosti - vodorovný segmentovaný
    pruh s ověřenou kategoriální paletou (skill dataviz), místo koláčového grafu
    (part-to-whole se u mnoha/dlouhých názvů složek lépe čte jako pruh než koláč);
    top 5 podílů + souhrnná položka "Ostatní", barevné dlaždice v legendě, tooltip
    s přesnou velikostí. Živě ověřeno na reálných datech.

### Opraveno
- Tlačítka navázaná přes `RelayCommand` chvíli po dokončení async operace (mount,
  scan, TRIM...) zůstávala zdánlivě needostupná - `CommandManager.RequerySuggested`
  se spoléhá na běžné vstupní události, ne na změny vlastností z async pokračování.
  Opraveno voláním `CommandManager.InvalidateRequerySuggested()` v `ViewModelBase.
  SetField`, živě ověřeno (mount ISO → okamžité odpojení).
- `Diskora.Data`: výchozí verze `Microsoft.Data.Sqlite` 9.0.0 táhla transitivní
  závislost `SQLitePCLRaw.lib.e_sqlite3` 2.1.10/2.1.11 se známou bezpečnostní
  chybou (GHSA-2m69-gcr7-jv3q, paměťová korupce v SQLite < 3.50.2) - opraveno
  explicitním přepisem na `SQLitePCLRaw.bundle_e_sqlite3` 3.0.4, ověřeno běžícími
  testy (žádné varování při buildu).

## [0.1.0] - 2026-07-25

První sestavitelná verze. Základy repozitáře (Fáze 0), dashboard disků (Fáze 1)
a první funkční řezy zbylých čtyř hlavních pilířů zadání - S.M.A.R.T. (Fáze 2),
kontrola integrity (Fáze 3), analýza zaplněnosti (Fáze 4) a TRIM/defragmentace
(Fáze 5) - plus začátek podpory virtuálních disků (Fáze 6). Vše živě ověřeno
proti reálným diskům, viz sekce Ověřeno níže.

### Přidáno
- Základní struktura repozitáře, licence GPLv3, vývojářská dokumentace.
- Diskora.Core: modely disků/svazků, `DiskEnumerationService` (WMI), `ByteSizeFormatter`.
- Diskora.Native: `ElevationHelper` (detekce běhu s právy administrátora).
- Diskora.App: základní WPF shell s dashboardem fyzických disků a svazků.
- Diskora.App: ikona aplikace (vlastní design), horní menu (Soubor/Zobrazit/Nápověda)
  s přepínáním světlé/tmavé/systémového tématu za běhu, klávesová zkratka Ctrl+R.

- S.M.A.R.T. monitoring (Fáze 2, první část): `Diskora.Native.Smart.AtaSmartReader`
  (čtení ATA SMART přes legacy `IOCTL_SMART_RCV_DRIVE_DATA`), doménové modely a
  `SmartHealthEvaluator`/`SmartAttributeCatalog` v Diskora.Core (s testy), a okno
  S.M.A.R.T. v Diskora.App otevíratelné z dashboardu s graceful degradací, když
  čtení není podporováno (USB most, RAID, NVMe, chybějící oprávnění).
- Kontrola integrity disku (Fáze 3, needestruktivní část): `Diskora.Native.Fsctl.VolumeDirtyChecker`
  (FSCTL_IS_VOLUME_DIRTY), nový projekt `Diskora.Repair` s `ChkdskRunner` spouštějícím
  `chkdsk /scan` (read-only) přes bezpečný `ProcessStartInfo.ArgumentList`, a okno
  Kontrola integrity v Diskora.App se živě streamovaným výstupem, otevíratelné z
  tabulky svazků. Skutečná oprava (`/f`, `/spotfix`) je vědomě zatím nepropojená.
- Analýza zaplněnosti disku (Fáze 4, styl TreeSize): `Diskora.Core.Services.DiskUsageScanner`
  (rekurzivní výpočet velikosti složek, bezpečné vůči reparse pointům/nedostupným
  složkám, testováno na reálných dočasných adresářích), a okno Analýza zaplněnosti
  v Diskora.App s drill-down navigací, průběžným hlášením postupu a ukazatelem podílu
  velikosti na rodičovské složce.
- Virtuální disky (Fáze 6, první část): nový projekt `Diskora.VirtualDisks` s
  `VirtualDiskReader` (čtení metadat VHD/VHDX přes `virtdisk.dll`, funguje bez admin
  práv) a `VirtualDiskAttacher` (připojení/odpojení, vyžaduje admin práva). Menu
  Soubor → „Otevřít virtuální disk..." otevírá okno se skutečnými metadaty a tlačítky
  pro připojení/odpojení. Ověřeno proti reálnému 4GB VHDX souboru.
- TRIM a defragmentace (Fáze 5 - dokončuje čtvrtý ze čtyř hlavních pilířů aplikace):
  `Diskora.Native.Storage.StoragePropertyReader` (detekce SSD/TRIM podpory přes
  IOCTL_STORAGE_QUERY_PROPERTY na svazku), `Diskora.Repair.DefragRunner` (orchestrace
  `defrag /L` pro TRIM a `defrag /D` pro defragmentaci, sdílí proces-streamovací
  logiku s `ChkdskRunner` přes nový `ProcessOutputRunner`), a okno Optimalizace disku
  v Diskora.App, které automaticky nabídne jen akci vhodnou pro zjištěný typ disku.

### Opraveno
- Kontrastní bug, kdy výchozí (nestylované) části DataGrid/Menu ignorovaly zvolené
  téma aplikace a přebíraly tmavé systémové barvy Windows nezávisle na tématu -
  opraveno přepisem SystemColors resources a vlastními šablonami pro Menu/MenuItem.
- `Diskora.VirtualDisks`: `AttachVirtualDisk`/`DetachVirtualDisk` vracely matoucí
  Win32 chybu 87 (neplatný parametr) kvůli nesprávné verzi struktury
  `OPEN_VIRTUAL_DISK_PARAMETERS` při otevírání s nenulovou přístupovou maskou -
  opraveno přechodem na verzi 1, nyní správně hlásí chybu 1314 (chybí oprávnění).
- `Diskora.Repair.DefragRunner`: výstup `defrag.exe` se při přesměrování dekódoval
  ve špatném kódování (mojibake místo diakritiky) - `defrag.exe` na rozdíl od
  `chkdsk.exe` píše do přesměrovaného streamu v OEM kódové stránce konzole (CP852 na
  české lokalizaci), ne v UTF-8/ASCII bezpečném textu. Opraveno explicitním nastavením
  `StandardOutputEncoding`/`StandardErrorEncoding` na OEM kódovou stránku.

### Ověřeno (živý test s reálným připojeným VHDX)
- Uživatel ručně připojil/naformátoval/naplnil testovací VHDX v elevovaném okně
  (`Mount-DiskImage` + `Initialize-Disk` + `Format-Volume`, mimo Diskoru) - výsledný
  svazek posloužil jako reálný, zahoditelný cíl pro ověření funkcí bez nutnosti
  spouštět Diskoru samotnou jako administrátor.
- Fyzické disky: virtuální disk se v dashboardu správně zobrazil jako
  BusType "Virtuální (soubor)", MediaType SSD.
- Analýza zaplněnosti: přesné výsledky (165 MB, správný rozpad po složkách,
  `System Volume Information` korektně nahlášena jako nedostupná).
- Kontrola integrity: dirty-bit funguje bez admin práv na běžném svazku (na rozdíl
  od systémového/boot svazku C:); `chkdsk /scan` vždy vyžaduje admin práva
  (potřebuje VSS snapshot) bez ohledu na svazek - obojí zjištěno živým testem,
  ne jen odvozeno z dokumentace.
- S.M.A.R.T.: graceful selhání (Win32 chyba 5) potvrzeno i na tomto druhém,
  odlišném reálném fyzickém disku.
- TRIM/defragmentace: svazek správně rozpoznán jako SSD s podporou TRIM (bez admin
  práv), okno Optimalizace disku správně nabídlo jen TRIM (ne defragmentaci); spuštění
  TRIM bez admin práv vrátilo srozumitelnou zprávu o chybějícím oprávnění (0x89000024) -
  právě při tomto testu se odhalil a opravil bug s kódováním výstupu defrag.exe.
