# Bezpečnostní politika

Diskora běží s vysokými oprávněními (typicky administrátor) a pracuje přímo
s uživatelovými daty na úrovni disků a souborů. Bezpečnost proto bereme jako
prioritu od první commitnuté řádky, ne jako dodatečnou vrstvu.

## Hlášení zranitelnosti

Pokud najdete bezpečnostní zranitelnost, **nevytvářejte prosím veřejný GitHub
issue**. Použijte místo toho [soukromé hlášení zranitelností](https://github.com/magorak/Diskora/security/advisories/new)
přímo na GitHubu — hlášení uvidí jen správce projektu a nikde se nezveřejní,
dokud nebude oprava k dispozici. Uveďte popis zranitelnosti, kroky k reprodukci
a očekávaný dopad. Snažíme se reagovat co nejdříve.

## Návrhové principy

- **Nejnižší nutná oprávnění.** Operace vyžadující elevaci (UAC) jsou izolované
  a jasně označené v UI; aplikace nežádá o admin práva pro funkce, které je
  nepotřebují (např. prohlížení velikostí složek). Platí to i uvnitř jedné
  funkce: čtení zdraví NVMe disku otevírá handle s `dwDesiredAccess = 0`, protože
  dotaz na vlastnost zařízení nepotřebuje právo číst data z disku - proto na
  rozdíl od ATA passthrough funguje bez elevace (`NvmeHealthReader`).
- **Žádné skládání shell příkazů ze stringů.** Všechna volání externích procesů
  (`chkdsk.exe`, `defrag.exe`) používají `ProcessStartInfo` s argument listy —
  nikdy interpolaci uživatelského vstupu do příkazové řádky. Prevence command
  injection.
- **Žádný vlastní zápis na raw filesystem struktury.** Opravy disku jdou vždy
  přes ověřené Windows mechanismy (chkdsk), nikdy přes vlastní low-level zápis —
  eliminuje celou třídu rizik ztráty dat z chyb ve vlastním kódu.
- **Validace vstupů na hranicích systému** — cesty k souborům, čísla disků a
  argumenty pro externí procesy se ověřují před použitím.
- **Žádná telemetrie ani síťová komunikace bez explicitního souhlasu.** Menší
  útočná plocha, žádná sbíraná data k úniku.
- **Podepsané release buildy** (Authenticode) a reprodukovatelné buildy v CI —
  zatím neimplementováno, viz [`ROADMAP.md`](../ROADMAP.md).
- **Statická analýza a dependency scanning v CI.** Roslyn analyzery běží už
  při každém buildu (`EnableNETAnalyzers` v `Directory.Build.props`). CodeQL
  (`.github/workflows/codeql.yml`) i Dependabot (`.github/dependabot.yml`)
  běží od zveřejnění repozitáře (2026-07-29); Dependabot hned po nasazení sám
  otevřel aktualizace zastaralých závislostí.

## Rozsah

Tato politika pokrývá `app/` (desktopová aplikace) i `web/` (produktový web).
Web nemá žádné přihlašování ani formuláře sbírající citlivá data, což omezuje
jeho útočnou plochu na standardní statická rizika (hlavičky, CSP, závislosti).

## Model hrozeb (threat model)

### Aktiva (co chráníme)
- **Integrita a dostupnost dat na discích uživatele.** Diskora čte i zapisuje
  (chkdsk, spotfix, TRIM/defrag) reálná uživatelská data - nejcennější aktivum
  je "nezpůsobit ztrátu dat, kterou by uživatel bez Diskory neměl".
- **Zvýšená oprávnění procesu.** Většina destruktivních operací běží jako
  administrátor - kompromitace Diskory za běhu s admin právy by útočníkovi
  dala plnou kontrolu nad systémem.
- **Historie SMART/integrity v SQLite** (`%LocalAppData%`) - technická
  metadata o discích, ne osobní dokumenty, ale mohla by prozradit sériová
  čísla disků nebo vzorec používání stroje.
- **Důvěryhodnost dodávaného binárního souboru** (dodavatelský řetězec builu
  a distribuce) - viz `SBOM`, `Code signing` níže.

### Důvěryhodné hranice a vstupy
- **Cesty k souborům/svazkům** zadané uživatelem (dialogy pro výběr složky/
  souboru, argumenty CLI) - jediný vstup, který by útočník s lokálním
  přístupem mohl ovlivnit, aby prošel do argumentů externího procesu.
- **Výstup externích nástrojů** (`chkdsk.exe`, `defrag.exe`, `schtasks.exe`,
  PowerShell cmdlety) - Diskora ho parsuje (regulární výrazy nad
  stage/percent hlášeními), ale nikdy needeserializuje/nevykonává jako kód.
- **Soubory obrazů disků** (ISO/VHD/VHDX/IMG) - čtou se jako binární data
  (MBR/GPT hlavičky, virtdisk.dll metadata), ne jako spustitelný obsah;
  poškozený/škodlivě sestavený obraz může nanejvýš způsobit chybu parsování
  (ošetřeno try/catch), ne spuštění kódu, protože Diskora žádný kód z
  obrazu nenačítá ani nespouští.
- **NuGet/npm závislosti** - viz Dependabot a SBOM níže; hranice důvěry je
  tady "věříme balíčku, dokud ho automatizovaně nesledujeme".

### Modelovaní útočníci (mimo rozsah pokud není řečeno jinak)
- **Lokální neprivilegovaný uživatel na stejném stroji**, který by chtěl
  Diskoru zneužít k eskalaci práv (např. přes command injection do
  spouštěných externích procesů) - **v rozsahu**, řeší ho princip
  "žádné skládání shell příkazů ze stringů" výše.
- **Útočník ovlivňující obsah disku/obrazu, který Diskora analyzuje**
  (např. připravený škodlivý `.img`/VHDX) - **v rozsahu pro parsování**
  (nesmí způsobit RCE), ale ne pro chkdsk/PowerShell samotné - to jsou
  důvěryhodné systémové komponenty Windows, jejich vlastní bezpečnost
  je mimo rozsah Diskory.
- **Vzdálený síťový útočník** - z velké části mimo rozsah, Diskora nemá
  žádný naslouchající síťový port ani nepřijímá vzdálený vstup; jediná
  síťová komunikace je volitelné stažení aktualizací (zatím
  neimplementováno) a produktový web.
- **Útočník s fyzickým přístupem k odpojenému/vypnutému stroji** - mimo
  rozsah, řeší BitLocker/OS, ne aplikace nad diskem.
- **Dodavatelský řetězec (supply chain)** - částečně v rozsahu: SBOM
  (Fáze 9) dává přehled o závislostech, Dependabot hlídá známé
  zranitelnosti, code signing (zatím neimplementováno) by ověřil
  integritu vydaného binárního souboru.

### Zmírnění podle Fáze
- Command injection → `ProcessStartInfo.ArgumentList` / proměnné prostředí
  místo interpolace (viz výše) - důsledně napříč `Diskora.Repair`.
- Neúmyslné zapsání destruktivní akce → potvrzovací dialogy s explicitním
  `MessageBoxResult.No` jako výchozím tlačítkem (spotfix ve Fázi 3).
- Tichý špatný výsledek maskovaný jako úspěch → `$ErrorActionPreference =
  'Stop'` + explicitní kontrola výsledku v PowerShell orchestraci (viz
  oprava v `ChkdskRunner.RunSpotFixAsync`, Fáze 3).
- Nadměrná oprávnění → `app.manifest` s `asInvoker` (ne `requireAdministrator`
  natvrdo), elevace se řeší až u konkrétní operace, která ji potřebuje.
- Zranitelné závislosti → Dependabot + SBOM (Fáze 9).
- Nedůvěryhodný binární soubor u koncového uživatele → code signing
  (Authenticode) - **zatím neimplementováno**, jediná zbývající mezera v
  téhle sekci; sledováno v `ROADMAP.md`.
