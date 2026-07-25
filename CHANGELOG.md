# Changelog

Formát vychází z [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
verzování dle [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Přidáno
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
