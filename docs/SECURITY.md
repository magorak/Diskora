# Bezpečnostní politika

Diskora běží s vysokými oprávněními (typicky administrátor) a pracuje přímo
s uživatelovými daty na úrovni disků a souborů. Bezpečnost proto bereme jako
prioritu od první commitnuté řádky, ne jako dodatečnou vrstvu.

## Hlášení zranitelnosti

Pokud najdete bezpečnostní zranitelnost, **nevytvářejte prosím veřejný GitHub
issue**. Místo toho kontaktujte správce projektu přímo (kontakt bude doplněn
při zveřejnění repozitáře) s popisem zranitelnosti, kroky k reprodukci a
očekávaným dopadem. Snažíme se reagovat co nejdříve.

## Návrhové principy

- **Nejnižší nutná oprávnění.** Operace vyžadující elevaci (UAC) jsou izolované
  a jasně označené v UI; aplikace nežádá o admin práva pro funkce, které je
  nepotřebují (např. prohlížení velikostí složek).
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
- **Podepsané release buildy** (Authenticode) a reprodukovatelné buildy v CI.
- **Statická analýza a dependency scanning v CI** (Roslyn analyzery, CodeQL,
  Dependabot/Renovate) — viz Fáze 9 v [`TODO.md`](../TODO.md).

## Rozsah

Tato politika pokrývá `app/` (desktopová aplikace) i `web/` (produktový web).
Web nemá žádné přihlašování ani formuláře sbírající citlivá data, což omezuje
jeho útočnou plochu na standardní statická rizika (hlavičky, CSP, závislosti).
