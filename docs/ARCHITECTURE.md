# Architektura

## Přehled

Diskora je monorepo se dvěma hlavními částmi:

- **`app/`** — desktopová Windows aplikace v C# / .NET, WPF UI
- **`web/`** — produktový web a podrobná dokumentace/nápověda (Astro + Starlight)

## Proč tento stack

- **C# / .NET (WPF), ne WinUI3/C++**: rychlejší a bezpečnější vývoj (managed kód,
  žádné manuální memory management chyby při práci s nízkoúrovňovým I/O), zralý
  ekosystém pro desktop UI a grafy, žádné packaging tření MSIX pro v1. Nízkoúrovňový
  přístup k hardwaru je přes P/Invoke stejně dostupný jako z C++.
- **Oprava přes orchestraci, ne vlastní filesystem engine**: `chkdsk.exe` je
  desítky let ověřený nástroj vyvíjený Microsoftem; psát vlastní NTFS/FAT/exFAT
  repair engine od nuly by bylo obrovské riziko ztráty dat uživatelů a měsíce až
  roky práce. Diskora přidává hodnotu v diagnostice, vizualizaci, plánování
  a srozumitelném reportování nad ověřenými mechanismy, ne v přepisování zápisu
  na disk.
- **SQLite** (`Microsoft.Data.Sqlite`, MIT) pro lokální historii — žádný cloud,
  žádný účet, v souladu s principem "žádná telemetrie". Databáze žije v
  `%LocalAppData%\Diskora\diskora.db`, nevyžaduje admin práva.

## Struktura projektů (`app/src/`)

| Projekt | Zodpovědnost |
|---|---|
| `Diskora.App` | WPF UI — views, viewmodely (MVVM), theming, DI bootstrap |
| `Diskora.Core` | Doménové modely a služby nezávislé na UI (enumerace disků, health skóre, treemap výpočty). Podsložky: `Diagnostics` (Disk Doctor, odhad životnosti, vzor pro test kapacity), `Output` (překlad výstupů chkdsk/defrag), `Export` (CSV a HTML zpráva), `Changelog` |
| `Diskora.Native` | P/Invoke wrappery pro Win32/IOCTL API (ATA SMART i NVMe health log, TRIM, geometrie disku, elevace) |
| `Diskora.Repair` | Orchestrace `chkdsk`/`defrag`, parsování výstupu, čtení Event Logu |
| `Diskora.VirtualDisks` | VHD/VHDX (`virtdisk.dll`), ISO (IMAPI2), IMG mount/inspekce |
| `Diskora.Data` | SQLite historie (SMART trendy, výsledky scanů, nastavení) |
| `Diskora.Cli` | Headless/scriptovatelný companion s JSON výstupem |

Projekty vznikají postupně podle fáze v [`TODO.md`](../TODO.md) — nevytváří se
prázdné/neúplné stub projekty předem.

## Bezpečnostní principy (viz i [SECURITY.md](SECURITY.md))

- Žádné skládání shell příkazů ze stringů — vždy `ProcessStartInfo` s argument listy.
- Žádný vlastní zápis na raw filesystem struktury — jen čtení pro diagnostiku a
  orchestrace ověřených nástrojů pro zápis/opravu.
- Nejnižší nutná oprávnění; elevace (UAC) se vyžaduje jen pro konkrétní operace,
  které ji potřebují, a je uživateli viditelně vysvětlena.
- Žádná telemetrie, žádné síťové volání bez explicitního souhlasu uživatele.
- Release buildy podepsané (Authenticode), CI se statickou analýzou a CodeQL.

## Testování

Každý projekt v `Diskora.Core`/`Diskora.Native`/atd. má odpovídající `*.Tests`
projekt v `app/tests/` (xUnit). Logika testovatelná bez reálného hardwaru
(formátování, parsování, health skóre výpočty) má jednotkové testy; kód závislý
na reálném I/O (WMI, IOCTL) je za rozhraním (`IDiskEnumerationService` apod.),
aby šel mockovat.
