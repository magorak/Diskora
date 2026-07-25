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
- [ ] CI pipeline (build/test na Windows runneru) — `.github/workflows/`

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
      chkdsk vypisuje pevně anglicky bez ohledu na jazyk Windows) - 15 testů, UI ověřeno
      živě (bez adminu chkdsk padne před fází 1, takže reálné plnění baru přes fáze
      nebylo možné vizuálně ověřit v tomto prostředí)
- [ ] Skutečná oprava (`/f`, `/spotfix`) - záměrně zatím nepropojeno, potřebuje vlastní
      potvrzovací UI (riziko naplánovaného restartu na systémovém svazku)
- [x] Čtení Event Logu (`Diskora.Native.EventLog.DiskEventLogReader` přes
      `System.Diagnostics.Eventing.Reader.EventLogReader`, filtrováno na providery
      Ntfs/Disk/Volsnap/Virtual Disk Service/FilterManager/Wininit v protokolech
      System i Application) - nové okno „Systémový protokol" (menu Nástroje),
      read-only, funguje bez admin práv - živě ověřeno na reálném protokolu
      tohoto stroje (dirty-bit kontrola svazku E: se skutečně propsala jako
      Ntfs event 98, korektně česky lokalizovaná zpráva díky cs-CZ locale)
- [ ] Read-only povrchový sken vadných sektorů + report
- [x] Historie výsledků kontrol (`SqliteDiskHistoryStore`, tabulka historie v okně Kontrola
      integrity - dirty-bit i výsledky skenů) - živě ověřeno

## Fáze 4 — Analýza zaplněnosti (styl TreeSize)
- [x] Rekurzivní scanner složek/souborů (`DiskUsageScanner`, zatím jednovláknový - viz níže) -
      živě ověřeno na reálném naplněném svazku, výsledky (velikosti/počty souborů) sedí přesně
      a `System Volume Information` je správně nahlášena jako nedostupná
- [ ] Zrychlení: vícevláknový sken s omezenou paralelitou - živě potvrzeno jako reálná
      potřeba: sken celého `C:\` (systémový svazek, statisíce souborů) v tomto prostředí
      neskončil ani po 4 minutách, zatímco malý testovací svazek `E:\` (165 MB) je hotový
      za sekundu - jednovláknový rekurzivní sken neškáluje na velké svazky
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
- [ ] Vlastní treemap control (squarified algoritmus) / sunburst jako alternativní zobrazení
- [ ] Hledač duplicit (hash-based)
- [ ] Export reportu (CSV/JSON)
- [ ] Výběr libovolné složky ke skenování (zatím jen kořen svazku z dashboardu)

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
      práva (ověřeno: Win32 chyba 1314 bez elevace, srozumitelně zobrazeno v UI)
- [x] ISO: mount/dismount přes orchestraci `Mount-DiskImage`/`Dismount-DiskImage`
      (`Diskora.Repair.IsoMounter`) - živě ověřeno, funguje bez admin práv (na rozdíl od
      VHD/VHDX). Přímé `AttachVirtualDisk` s VIRTUAL_STORAGE_TYPE_DEVICE_ISO sice vrátí
      úspěch i bez elevace, ale výsledná jednotka zůstane bez souborového systému -
      zdokumentováno v kódu, proto orchestrace přes ověřený cmdlet místo P/Invoke
- [ ] IMG/raw: mount jako virtuální disk nebo read-only sektorová inspekce
- [ ] Bezpečný unmount/cleanup (i při pádu aplikace)
- [ ] Znovupoužití integrity/SMART/TreeSize logiky nad připojenými virtuálními disky
      (až bude k dispozici elevovaný test)

## Fáze 7 — Plánování a CLI companion
- [ ] Integrace s Windows Task Scheduler (periodické kontroly zdraví)
- [ ] `Diskora.Cli`: headless mód s JSON výstupem pro skriptování
- [ ] Tray ikona a notifikace při upozorněních

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
