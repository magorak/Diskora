# Diskora — co je před námi

Otevřené věci a známá omezení. Co je hotové, najdete v [CHANGELOG.md](CHANGELOG.md) —
včetně toho, co se při vývoji ukázalo jako slepá ulička a proč.

Zásada projektu: **raději přiznat, co nefunguje nebo neověřené, než to zamlčet.**
Proto je tenhle soubor o tom, co chybí, a ne o tom, co se povedlo.

## Známé chyby

- **Zamrznutí po zrušení kontroly integrity** — od 2026-07-29 se už neopakuje.
  Nikdy se ho nepodařilo reprodukovat měřením (po zrušení zámrz 15–16 ms na USB
  i systémovém svazku), takže **není jisté, co ho odstranilo**. Nejpravděpodobnější
  příčinou je oprava souběhu v `ProcessOutputRunner`: výstup procesu se přidával
  do sdíleného `List<string>` ze dvou vláken bez zámku, což umí ztrácet i duplikovat
  položky a chovat se nepravidelně. Kdyby se to vrátilo, tady jsou vyvrácené
  hypotézy, ať se k nim nikdo nevrací: sken na UI vlákně (79 % vs. 80 % odezvy,
  tedy beze změny), zahlcení hlášením postupu (~940 hlášení/s drží odezvu na 80 %),
  zabíjení chkdsk na UI vlákně (62–78 ms včetně VSS snapshotu).
- **Tři vady rozvržení hlavního okna**, viditelné až na skutečném snímku:
  uříznutá záhlaví sloupců („Systém s“ místo „Systém souborů“), přetékající
  sloupec s tlačítky překrývající posuvník a dole uříznutá tabulka svazků.

## Vzhled

- Okna jsou funkční, ale strohá. Pořadí, které dává smysl: nejdřív opravit vady
  rozvržení výše, pak ikony typů disků a stavů, zdraví jako grafický prvek místo
  textového odznaku, kultivace odsazení a typografie. Animacím se vyhýbáme —
  u diskového nástroje působí nevážně a berou to, v čem je Diskora dobrá:
  okamžitou odezvu.
- **Okno „Co je nového“ ukazovat zkráceně.** Teď vypisuje celý changelog včetně
  vývojářských detailů. Návrh: brát tučný úvod položky jako značku „hlavní bod“
  a výchozí zobrazovat jen ty, s možností rozbalit vše.

## Funkce

- **Graf trendu zdraví v čase.** Historie v SQLite existuje, ale ukládá jen stav
  (v pořádku / varování / kritické), ne číselné hodnoty — graf by byl vodorovná
  čára. Nejdřív je potřeba rozšířit záznam o čísla (opotřebení, teplota,
  přemapované sektory) a pak počkat, až se data nasbírají.
- **Odhad životnosti u dalších SSD.** Pokryté je NVMe a atributy 233, 177 a
  (po ověření významu) 202/231. Disky hlásící opotřebení jinak spadnou do
  „nelze odhadnout“ — vědomě, protože špatně přečtený vendor-specifický atribut
  by dal smyšlený počet let.
- **Test kapacity: cesta „přeznačený disk“ není živě ověřená.** Takový disk
  nebyl k dispozici; pokrývají ji jen testy nad vzorem.
- Plugin architektura pro další souborové systémy (ReFS, exFAT, ext4 přes WSL).
- Instalátor (Inno Setup/MSIX) — odsunuto, protože přenosný single-exe pokrývá
  hlavní potřebu. Vrátit se k tomu, až bude žádoucí zástupce v nabídce Start,
  položka v „Přidat nebo odebrat programy“ nebo automatická registrace
  naplánované úlohy.

## Jazyk

- **Lokalizace (cs-CZ, en-US)** — čeká na rozhodnutí, jestli má být dvojjazyčný
  i web a dokumentace. Jde o zhruba 380 řetězců napříč sedmi projekty; anglické
  UI u českého webu by dalo nekonzistentní produkt.

## Vydávání a provoz

- **Nahrát binárku k vydání** na GitHub Releases (tagy `v0.2.0` a `v0.3.0` už
  existují). Odkaz „Stáhnout aplikaci“ na webu na ně míří.
- **Code signing (Authenticode)** pro vydávané buildy — potřebuje certifikát.
  Bez podpisu Windows u staženého souboru zobrazí varování SmartScreen.
- **CI: nasazení webu a kontrola bezpečnostních hlaviček/CSP.** Build, testy,
  CodeQL a Dependabot běží; zbývá deploy.
- **Nové snímky obrazovky pro web** — stávající jsou starší než Disk Doctor.
  Skript na jejich pořízení je v [`tools/capture-window.ps1`](tools/capture-window.ps1).
- **Stránky nápovědy** pro odhad životnosti a zprávu pro člověka — zatím jsou
  popsané jen uvnitř stránky Disk Doctor.
- Udržovat [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) a
  [`docs/CONTRIBUTING.md`](docs/CONTRIBUTING.md) aktuální.
- Migrace nápovědy na Starlight, pokud obsah naroste natolik, že se vyplatí
  full-text hledání a automatický sidebar.
