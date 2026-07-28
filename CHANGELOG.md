# Changelog

Formát vychází z [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
verzování dle [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Přidáno
- **Zpráva o stavu disku pro člověka (HTML)** - tlačítko „Uložit zprávu..." v okně
  Disk Doctor uloží jednu přehlednou stránku se závěry a doporučeními a rovnou ji
  otevře v prohlížeči. Na rozdíl od exportů CSV/JSON, které míří na skriptování,
  tenhle výstup míří na člověka: srozumitelné věty, barevné odlišení závažnosti,
  doporučení - něco, co se dá poslat příbuznému nebo ITčkaři.
  HTML je zcela soběstačné: vložené styly, žádné obrázky, písma ani skripty zvenčí.
  Odpovídá to zásadě „žádná síťová komunikace" - otevřená zpráva si nikam nesáhne,
  funguje offline a je to jediný soubor do přílohy e-mailu. Popisky svazků zadává
  uživatel, takže se do HTML vkládají ošetřené (název jako `<script>` stránku
  nerozbije) - pokryto testem. 9 testů.
  Živě ověřeno vygenerováním zprávy ze tří reálných svazků: 3 sekce, 0 externích
  zdrojů, 0 skriptů, správná čeština i doporučení.

### Přidáno
- **Odhad zbývající životnosti disku** - Disk Doctor nově místo pouhého čísla
  „spotřebováno 6 %" řekne, co to znamená: „Při stejném způsobu používání vydrží
  disk odhadem ještě 7 let." Přesně tohle uživatele zajímá, a konkurence
  (CrystalDiskInfo a spol.) u toho čísla mlčí.
  Počítá se z JEDNOHO čtení, ne z historie: opotřebení za dosavadní dobu provozu
  dá tempo, tempo dá odhad zbytku - není tedy potřeba čekat týdny na nasbíraná
  data. Cenou je předpoklad, že se disk bude používat jako dosud, což text říká
  nahlas („odhad z dosavadního tempa, ne záruka").
  Zásada „raději mlčet než hádat": když disk ukazatel opotřebení nemá, když je
  opotřebení ještě neměřitelné (dělení nulou), nebo když je doba provozu pod
  100 h, vrací se místo čísla vysvětlení proč. 12 testů.
  Živě ověřeno na třech typech disků: NVMe (6 % za 3948 h → 7 let), 4TB talířový
  disk (správně odmítne s vysvětlením, že se u něj životnost takhle neměří)
  a SATA SSD (odmítne - nehlásí atribut 233).
  Známé omezení: pokrytý je NVMe a SATA SSD s atributem 233. Disky, které
  opotřebení hlásí jinými atributy (177, 202, 231), zatím spadnou do „nelze
  odhadnout" - vědomě, protože špatně přečtený vendor-specifický atribut by dal
  smyšlené číslo, a to je u tvrzení „disk vydrží ještě X let" horší než mlčení.

### Přidáno
- **Test skutečné kapacity disku** - odhalí přeznačené flash disky, které hlásí
  víc, než fyzicky mají. Tlačítko „Test kapacity" u každého svazku v dashboardu
  a `diskora capacity <písmeno>` v CLI. Dnes na to lidé sahají po H2testw nebo
  FakeFlashTest - anglických jednoúčelových nástrojích bez údržby; tohle je
  česky a jako součást normálního diskového nástroje.
  Hodnota každého bajtu se počítá z jeho pozice (SplitMix64), takže se nemusí
  nic ukládat stranou a ověření si ji spočítá znovu. To je proti přeznačeným
  diskům klíčové: ty adresy nad svou skutečnou kapacitou „zabalí" zpátky na
  začátek, takže se na dané pozici čtou data patřící jinam. U konstantního vzoru
  nebo samých nul by takový disk prošel jako zdravý.
  Pracuje se soubory na svazku, ne se syrovým diskem: nepotřebuje práva
  administrátora, nezničí oddíly ani data, která na disku už jsou, a po sobě
  uklidí i po zrušení nebo chybě. Zápis se povinně vyprazdňuje na médium -
  bez toho by se četlo z vyrovnávací paměti systému a přeznačený disk by prošel.
  8 testů na vzor (včetně případů „data z jiné pozice" a „samé nuly").
  Živě ověřeno na reálné 14,7GB USB flashce: celých 14,61 GB zapsáno i přečteno
  beze změny za 11,5 minuty, disk je tedy pravý; testovací data se po sobě
  smazala. Ověřeno i přes GUI okno včetně zrušení uprostřed (0,2 s) a úklidu.
  Cesta „přeznačený disk" živě ověřená není - takový disk k dispozici nebyl,
  pokrytá je jen testy nad vzorem.

### Přidáno
- Výstupy `chkdsk` a `defrag` se zobrazují česky (nahlásil uživatel): nový
  `Diskora.Core.Output.ToolOutputTranslator` překládá rozpoznané řádky, neznámé
  nechává beze změny (nová verze Windows tak nikdy nezpůsobí ztrátu informace)
  a nikdy nepřepisuje čísla, jednotky ani názvy svazků. Zachovává odsazení, aby
  zůstala čitelná struktura reportů defragu. Obě okna mají nově přepínač
  „Zobrazit původní anglický výstup nástroje" - překlad je výchozí zobrazení,
  ne náhrada. 31 testů, jejichž vstupy jsou doslovné řádky ze skutečných běhů.
  Živě ověřeno na reálném `chkdsk H: /scan`: ze 220 řádků výstupu zůstane
  54 čitelných, zbytek je odfiltrovaná záplava průběžných hlášení.

### Opraveno
- Průběh kontroly integrity se hýbal jen po fázích: `ChkdskOutputParser` hledal
  formát „N percent complete", jenže chkdsk na aktuálních Windows vypisuje
  „Progress: X of Y done; Stage: N%; Total: M%". Procenta se tedy nikdy netrefila
  a ukazatel skákal po 0/33/66 %. Nově se čte přímo hlášená celková hodnota
  „Total" - živě ověřeno, ukazatel má přes 40 různých hodnot místo tří.
- Diakritika ve výstupu `chkdsk`: na rozdíl od `defrag` se mu nepředávalo kódování
  konzolových nástrojů, takže z názvu svazku „Nový svazek" zbylo „Nov? svazek".
  Kódování je nově společné pro všechny orchestrované nástroje. Ověřeno
  porovnáním kódů znaků, ne podle vzhledu v konzoli - ta si text sama překóduje
  a při hledání příčiny dvakrát svedla na špatnou stopu (`ý` dorazí jako bajt
  0xEC, což odpovídá právě OEM stránce; ANSI z toho dělá „ě" a UTF-8 náhradní znak).
- Záplava průběžných hlášení ve výpisu: chkdsk vypíše stovky řádků „Progress: ..."
  a řádků samých mezer, kterými maže předchozí řádek. Do výpisu se už nedostanou,
  ale pořád se z nich čte postup.

### Změněno
- Optimalizace disku u disků s nezjištěným typem (nahlásil uživatel): u disku za USB
  mostem vrací `DeviceSeekPenaltyProperty` null, takže se podle pravidla „při nejistotě
  nenabízet nic" schoval TRIM i defragmentace - a to i s právy administrátora, přestože
  Windows s tím diskem ve svém „Optimalizovat jednotky" pracovat umí. Nově si
  `DiskOptimizationService` při mlčícím IOCTL vyžádá druhý názor z WMI
  (`MSFT_PhysicalDisk.MediaType`); záměrně nepoužívá `SpindleSpeed`, protože u testovaného
  USB disku je 0, což by ho falešně prohlásilo za SSD. Když typ nezná ani WMI, UI už
  neschová všechno: nabídne obě akce a přidá upozornění, že typ není známý - TRIM na
  talířovém disku nic nezkazí, defragmentaci má uživatel spouštět jen když ví, že o
  talířový disk jde. Slepá ulička „Diskora neumí nic, Windows umí" je horší než nabídka
  s upozorněním. Živě ověřeno na třech reálných svazcích (USB s neznámým typem, HDD, SSD).
- Výpis v okně Kontrola integrity se sám posouvá na poslední řádek (nahlásil uživatel).
  Jakmile si uživatel odroluje nahoru číst starší řádek, sledování se vypne a výpis mu
  neuteče; po návratu na konec se zase zapne.

### Ověřeno (živý test na uvolněném svazku H:)
- Úspěšný běh opravy integrity (`Repair-Volume -SpotFix`) s právy administrátora -
  do teď byla ověřená jen cesta selhání bez elevace. Rovnou odhalil chybu popsanou
  níže v „Opraveno".
- Defragmentace na skutečném mechanickém disku (`defrag.exe /D`, USB WDC WD32,
  298 GB) - `ExitCode=0`, kompletní Pre-Optimization i Post Defragmentation report
  (296 978 přesouvatelných souborů, MFT 393,75 MB), ~3 s. Kód sice sdílel ověřenou
  cestu s TRIM, ale na talířovém disku do teď nikdy neběžel.
- Zjištěno přitom, že přes USB most nejde určit `DeviceSeekPenaltyProperty` (vrací
  null), takže Diskora u takového disku defragmentaci vůbec nenabídne - správné
  chování podle pravidla „při nejistotě nenabízet nic". Interní 4TB SATA HDD se
  naproti tomu rozpozná správně.

### Přidáno
- Disk Doctor: jedno tlačítko u každého svazku v dashboardu (a `diskora doctor
  <písmeno>` v CLI), které projde S.M.A.R.T., stav souborového systému i typ
  disku a dá jeden srozumitelný verdikt s doporučeními. Rozhodování je čistá
  funkce `DiskDoctorAdvisor.Diagnose` nad `DiskDoctorInputs`, takže se dá
  otestovat bez disků, bez elevace a bez čekání (16 testů); `DiskDoctorService`
  jen posbírá data z už existujících ověřených služeb a žádnou novou cestu
  k datům nevytváří.
  Doctor je vědomě pouze diagnostický - sám nic nespouští. Akce jen nabízí a
  tlačítko otevře příslušné existující okno, kde má akce vlastní potvrzení
  (spotfix a defragmentace na disk skutečně zapisují). Při nejistém typu disku
  nenabídne ani TRIM, ani defragmentaci - stejné pravidlo jako v okně Optimalizace,
  protože doporučit defragmentaci SSD je horší než mlčet.
  Přínos proti pouhému „disk je v pořádku" se ukázal hned při živém testu na
  reálném 4TB HDD: S.M.A.R.T. hlásí zdravý disk, ale Doctor navíc vytáhne
  2426 chyb přenosu (atribut 199) a vysvětlí, že jde skoro vždy o vadný SATA
  kabel, ne o vadný disk. Tenhle atribut nezhoršuje normalizovanou hodnotu,
  takže by v souhrnném verdiktu zapadl.
  Živě ověřeno na 4 reálných svazcích s elevací i bez (NVMe systémový, 4TB HDD,
  SATA SSD, USB disk) - přes CLI i přes skutečnou instanci `DiskDoctorWindow`.
  Bez elevace hlásí, že je tahle část kontroly slepá, a doporučí elevaci;
  s elevací u USB mostu elevaci už nenavrhuje a vysvětlí, že disk data
  neposkytuje.

### Opraveno
- **Oprava integrity (spotfix) hlásila selhání i po úspěšném průběhu** (Fáze 3):
  PowerShell skript kontroloval `$result.HealthStatus`, jenže `Repair-Volume`
  vrací PŘÍMO hodnotu typu `RepairStatus` (např. `NoErrorsFound`) a žádnou
  vlastnost `HealthStatus` nemá. Kontrola je uvnitř `try` bloku, takže i naprosto
  úspěšná oprava skončila vyhozenou výjimkou a `ExitCode=1`. Chyba přežila proto,
  že tahle cesta do teď nikdy neběžela s právy administrátora - bez elevace
  selhala dřív z jiného důvodu. Odhaleno prvním živým spuštěním na svazku, který
  uživatel k destruktivním testům výslovně uvolnil; tvar výsledku ověřen
  samostatnou sondou přímo nad `Repair-Volume`. Po opravě: `ExitCode=0`,
  výstup „Stav opravy: NoErrorsFound", dirty bit před i po `Clean`.
- `ProcessOutputRunner`: `OutputDataReceived` a `ErrorDataReceived` se vyvolávají
  každý ze svého vlákna, ale oba přidávaly do sdíleného `List<string>` bez
  synchronizace - souběžný `List.Add` může ztratit nebo zdvojit řádky, případně
  trefit zvětšování pole. Přidán zámek (hlášení průběhu zůstává mimo něj, ať se
  nedrží zámek přes marshalling na UI vlákno). Latentní chyba nalezená při
  čtení kódu; projevila by se jen u procesů, které skutečně píšou do obou
  streamů zároveň.
- `diskora doctor` nenacházel žádný svazek: `VolumeInfo.Name` má tvar `E:\`,
  kdežto `NormalizeDriveLetter` vrací `E:`, takže porovnání selhalo pro všechna
  písmena. Odhaleno prvním živým spuštěním nového příkazu.

## [0.2.0] - 2026-07-27

### Opraveno
- Sázení changelogu (aplikace i web): `**tučný text**` se zobrazoval doslova
  včetně hvězdiček a jedna verze měla tolik nadpisů kategorií, kolik commitů do
  ní přispělo (sekce „Nevydáno" jich měla 10 místo 2), protože si každý commit
  přidával vlastní blok „### Přidáno"/„### Opraveno". Položky téže kategorie se
  teď v rámci jedné verze slévají do jejího prvního výskytu a zvýraznění se sází
  jako zvýraznění. Opraveno v obou parserech naráz, ať se aplikace a web chovají
  shodně - odhaleno při živém testování nového okna „Co je nového".
- **S.M.A.R.T. nefungoval na žádném ATA/SATA disku** (Fáze 2): legacy cesta
  `IOCTL_SMART_RCV_DRIVE_DATA` počítala hlavičku `SENDCMDOUTPARAMS` na 8 bajtů,
  ale skutečná je 16 (`cBufferSize` 4 + `DRIVERSTATUS` 12, tedy `bDriverError` 1
  + `bIDEError` 1 + `bReserved[2]` + `dwReserved[2]` 8). Výstupní buffer byl proto
  o 8 bajtů kratší, než ovladač vyžaduje, a `DeviceIoControl` vždy skončil na
  `ERROR_INSUFFICIENT_BUFFER` (122). Chyba přežila od Fáze 2 nepovšimnutá, protože
  bez elevace selže dřív už `CreateFile` (chyba 5) - navenek to vypadalo jako
  chybějící admin práva, ne jako vada v kódu. Odhaleno až prvním živým testem
  s elevací. Po opravě vrací legacy cesta na všech 4 SATA discích korektní data.
- Sloupce seznamu svazků na dashboardu (kosmetika): všechny sloupce měly pevnou
  šířku, takže se při běžné velikosti okna zobrazoval zbytečný spodní vodorovný
  posuvník místo přirozeného přizpůsobení. Sloupec Název je nově `Width="*"`,
  ostatní zúženy na skutečnou potřebu textu. Živě ověřeno screenshoty (výchozí
  šířka 1100px i zúžení na ~830px, blízko `MinWidth` okna) - bez posuvníku.
- Web pod podcestou (Fáze 10): web se bude nasazovat na
  `https://www.magorak.cz/diskora`, ne na kořen domény, ale všechny odkazy a
  obrázky byly napsané jako root-relative (`/logo.png`, `/docs/...`) - na
  reálném hostingu by proto byly rozbité styly, obrázky i veškerá interní
  navigace. Opraveno `site`/`base: '/diskora/'` v `astro.config.mjs` a ručním
  přepsáním všech pevně zapsaných cest na `${import.meta.env.BASE_URL}...`
  napříč `BaseLayout`, `DocsLayout` a všemi stránkami - Astro totiž base
  přiznává jen svým vlastním generovaným assetům, ne ručně psaným
  href/src atributům. Živě ověřeno simulací produkčního nasazení (`dist/`
  obsloužená obyčejným HTTP serverem pod cestou `/diskora/`) - HTML, CSS,
  obrázky i interní odkazy vracely HTTP 200 správně, včetně zvýraznění
  aktivní položky v menu nápovědy.
- `ChkdskRunner.RunSpotFixAsync`: bez admin práv `Repair-Volume` selhával jen
  jako NEterminating chyba, takže skript to tiše prohlásil za úspěch (prázdný
  `HealthStatus`, exit 0) - opraveno explicitní kontrolou výsledku. Živě
  odhaleno při testování - jeden běh nechtěně skutečně proběhl (dopad ověřen
  jako nulový, bez admin práv Windows operaci zablokoval dřív, než mohla
  cokoliv zapsat), což zároveň odhalilo, že „Ano" bylo výchozí tlačítko
  potvrzovacího dialogu - přepnuto na „Ne" jako výchozí.
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
- `TabControl`/`TabItem` (okno Analýza zaplněnosti): stejná třída bugu jako
  dřív u Menu/DataGrid - výchozí šablona nebere Background/Foreground z
  DynamicResource, takže hlavičky záložek ("Složky"/"Největší soubory"/
  "Nejstarší soubory") zůstávaly bílé a nečitelné i v tmavém tématu. Nahlásil
  uživatel po vyzkoušení přebuildované aplikace. Opraveno vlastní "underline"
  šablonou (vybraná záložka má spodní pruh v AccentBrush) - živě ověřeno
  v tmavém i světlém režimu.
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

### Přidáno
- Okno „Co je nového" (Fáze 11): menu Nápověda → „Co je nového...". Čte kořenový
  `CHANGELOG.md` zabalený jako embedded resource, takže funguje i v portable
  single-file buildu a bez připojení k internetu. `Diskora.Core.Changelog.
  ChangelogParser` používá záměrně stejná pravidla jako parser ve
  `web/src/pages/docs/changelog.astro` - obě verze changelogu (v aplikaci i na
  webu) čtou týž soubor, takže se nemůžou rozejít s realitou.
  Po aktualizaci na novou verzi se okno ukáže jednou samo
  (`AppSettings.LastSeenVersion`; porovnává se verze sestavení, ne datum, takže
  přeinstalace téže verze uživatele znovu neobtěžuje). Hodnota se ukládá PŘED
  zobrazením - kdyby appka skončila zavřením křížkem, okno se nemá otvírat dokola.
  Tlačítko „Otevřít na webu..." předá adresu výchozímu prohlížeči; Diskora sama
  žádné spojení nenavazuje (stránka o soukromí na webu na tuhle jedinou výjimku
  nově výslovně upozorňuje). 13 nových testů.
  Živě ověřeno izolovaným harness nad skutečnou instancí `WhatsNewWindow` nad
  reálným `CHANGELOG.md`: 2 verze, 48 + 19 položek, 282 úseků kódu vysázených
  monospace, zvýraznění správně, žádné doslovné `**` v textu.
- ATA pass-through jako hlavní cesta k S.M.A.R.T. (Fáze 2): nový
  `AtaPassThroughSmartReader` posílá SMART příkazy přes `IOCTL_ATA_PASS_THROUGH`
  (`ATA_PASS_THROUGH_EX` s datovým bufferem hned za strukturou). Na rozdíl od
  legacy IOCTL nejde o obálku nad pevnou dvojicí příkazů, ale o obecný kanál pro
  libovolný ATA příkaz, který podporuje širší škála řadičů. `AtaSmartReader` je
  nově jen rozcestník: zkusí pass-through, při neúspěchu spadne na legacy cestu,
  a když selžou obě, spojí důvody do jedné hlášky (shodné důvody neopakuje).
  Úspěch s prázdnou tabulkou atributů se záměrně bere jako neúspěch - prázdný
  seznam nahlášený jako „v pořádku" by uživateli tvrdil, že disk je bez problémů,
  aniž by se cokoli změřilo.
  Parsování tabulky vytaženo do sdíleného `SmartAttributeTableParser`; do teď bylo
  privátní uvnitř readeru, takže netestované - přibylo 8 testů včetně 48bitových
  syrových hodnot (doba provozu a „celkem zapsáno" u SSD běžně přerostou 32 bitů)
  a hraničních případů. Prahy selhání jsou nově best-effort: příkaz 0xD1 je
  v novějších revizích ATA zastaralý a disk ho smí odmítnout, aniž by to mělo
  shodit celé čtení. Živé testování na reálném HDD zároveň ukázalo atributy
  ID 3/4/11/200, které katalog neznal a zobrazoval jako „Neznámý atribut" - doplněny.
  Živě ověřeno s admin právy na 6 fyzických discích: obě cesty vrací na všech
  4 SATA discích bajt po bajtu identická data (nezávislé křížové ověření), NVMe
  a USB most obě ATA cesty korektně odmítnou. End-to-end `diskora healthcheck`
  hlásí nově `Healthy` u 5 z 6 disků (dřív „nedostupné" u všech) a okno S.M.A.R.T.
  poprvé skutečně zobrazuje ATA atributy (17 řádků reálného 4TB HDD).
- Podpora S.M.A.R.T. u NVMe disků (Fáze 2): nový `Diskora.Native.Smart.NvmeHealthReader`
  čte NVMe log stránku 0x02 („SMART / Health Information") přes
  `IOCTL_STORAGE_QUERY_PROPERTY` s `StorageDeviceProtocolSpecificProperty`. Do teď
  Diskora uměla jen ATA passthrough (`IOCTL_SMART_RCV_DRIVE_DATA`), který u NVMe
  disků principiálně selhává - na stroji s NVMe systémovým diskem tak zdraví
  disku nešlo zjistit vůbec. `SmartService` nově zkouší nejdřív NVMe cestu a
  teprve při neúspěchu ATA. Vedlejší efekt, který stojí za zmínku:
  **NVMe health se čte bez práv administrátora** (handle se otevírá s
  `dwDesiredAccess = 0`, dotaz na vlastnost zařízení nepotřebuje právo číst data),
  zatímco ATA cesta elevaci vyžaduje vždy - i vlastní `Get-StorageReliabilityCounter`
  ve Windows bez elevace odmítne přístup k CIM prostředku.
  NVMe nemá ATA atributy (ID/aktuální/nejhorší/práh), ale pevnou sadu pojmenovaných
  polí, takže se model netváří jako ATA: nový `NvmeHealthInfo` +
  `NvmeHealthCatalog` (11 řádků s českým názvem, hodnotou a vysvětlením rizika -
  stejný odlišující prvek jako `SmartAttributeCatalog` u ATA) a
  `NvmeHealthEvaluator` s explicitními pravidly (critical warning bity od řadiče,
  rezervní kapacita vůči výrobcem hlášenému prahu, spotřebovaná životnost,
  neopravitelné chyby média, teplota). Okno S.M.A.R.T. i `diskora smart`/`healthcheck`
  přepínají mezi ATA tabulkou a NVMe přehledem podle toho, co disk skutečně
  nabízí; export CSV/JSON pokrývá obě varianty. 15 nových testů.
  Živě ověřeno na reálném Samsung SSD 980 PRO 1TB (PhysicalDrive4) **bez elevace**:
  CLI `smart 4` i `healthcheck` (dřív hlásil „nedostupné" pro všech 6 disků, teď
  u NVMe `Healthy`), `--json` výstup i okno S.M.A.R.T. přes izolovaný harness nad
  skutečnou instancí `SmartWindow` (NVMe mřížka viditelná s 11 řádky, ATA mřížka
  správně skrytá; u SATA disku přesně naopak + hláška o nutné elevaci).
  Správnost dekódování ověřena vnitřní konzistencí dat: 36,15 TB zapsaných na disku
  s výrobcem udávanou životností 600 TBW odpovídá hlášeným 6 % spotřebované
  životnosti, teplota 42-43 °C a rezerva 100 % / práh 10 % sedí s očekáváním.
  Ne-NVMe disky (4× SATA, 1× USB) korektně vracejí `ERROR_INVALID_FUNCTION`
  a spadnou zpět na ATA cestu. Cestu „poškozený NVMe disk" (nenulové critical
  warning bity, vyčerpaná rezerva) se živě ověřit nedalo - žádný takový disk
  v prostředí není, pokrytá je jen testy nad `NvmeHealthEvaluator`.
- Changelog a Ochrana soukromí na webu (Fáze 10): `web/src/pages/docs/changelog.astro`
  generuje stránku přímo z kořenového `CHANGELOG.md` při buildu (vlastní malý
  parser šitý na tvar tohoto souboru, žádná nová npm závislost) - stránka je tak
  vždy doslova ve shodě s realitou, ne ruční kopie. `privacy.astro` fakticky
  popisuje, co se ukládá lokálně (SQLite historie, JSON předvolby) a potvrzuje
  (grepem přes `app/src`), že v appce není žádný `HttpClient`/síťový kód. Živě
  ověřeno `astro build` (7 stránek) + `astro preview` (HTTP 200, správně
  vyrenderované nadpisy verzí i vnořené `<code>` značky).
- Portable self-contained build (Fáze 9): nový `app/publish-portable.ps1`
  (`dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true`) -
  jediný přenositelný `Diskora.exe` (~176 MB) bez závislosti na .NET nainstalovaném
  v cíli. Instalátor (Inno Setup/MSIX) zůstává otevřený - potřebný nástroj není
  v tomto prostředí k dispozici. Živě ověřeno: publikovaný exe zkopírovaný do
  vlastní složky se spustil a otevřel okno (UI Automation).
- Model hrozeb v `docs/SECURITY.md` (Fáze 9): nová sekce shrnující aktiva,
  důvěryhodné hranice/vstupy, modelované útočníky (vč. explicitně mimo rozsah)
  a zmírnění podle fáze s odkazy na konkrétní opravy zdokumentované jinde v
  tomto souboru. Čistě dokumentační práce, nic k živému ověření.
- Okno Nastavení (Fáze 8): práh upozornění na zhoršení zdraví disku (Varování
  a horší / Jen kritické) a volba nabídnout při startu bez admin práv restart
  s elevací (výchozí dialogové tlačítko „Ne", stejný vzor jako spotfix ve Fázi 3).
  Menu-klik nebylo možné živě ověřit přes UI Automation (obecné omezení
  automatizace Menu v tomto prostředí, postihuje i starší položky menu), ale
  samotné okno je živě ověřené izolovaným harness (uložení/znovunačtení JSON
  nastavení funguje správně).
- Audit klávesové navigace (Fáze 8): žádné okno nepoužívá explicitní `TabIndex`,
  pořadí Tabu tak všude odpovídá pořadí deklarace v XAML, které ve všech oknech
  odpovídá vizuálnímu pořadí čtení. Živě ověřeno na novém okně Nastavení
  (skutečné `MoveFocus` přes izolovaný harness).
- Generování SBOM v CI (Fáze 9): `dotnet-CycloneDX` jako lokální .NET nástroj
  (`app/.config/dotnet-tools.json`, `dotnet tool restore` - žádná trvalá
  systémová změna). Krok v `.github/workflows/build.yml` generuje `bom.json`
  (CycloneDX 1.7) při každém buildu a nahrává ho jako CI artefakt - soubor se
  necommituje (`app/sbom/` v `.gitignore`), generuje se vždy čerstvě. Na
  rozdíl od CodeQL/Dependabot šlo tohle ověřit živě lokálně - reálně
  vygenerovaný platný JSON s 30 komponentami, hashi a licencemi NuGet
  balíčků.
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
