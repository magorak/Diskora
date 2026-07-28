# Přispívání do Diskora

## Než začnete

Otevřené věci a známá omezení sleduje [`ROADMAP.md`](../ROADMAP.md).
Než začnete pracovat na nové funkci, zkontrolujte, jestli už není rozpracovaná,
a odškrtněte položky, které dokončíte.

## Vývojové prostředí

- Windows 10/11
- [.NET SDK](https://dotnet.microsoft.com/) odpovídající `TargetFramework` v
  `app/Directory.Build.props`

```powershell
dotnet build app/Diskora.slnx
dotnet test app/Diskora.slnx
dotnet run --project app/src/Diskora.App
```

## Konvence

- **Jazyk kódu**: anglické názvy identifikátorů, komentáře jen tam, kde vysvětlují
  netriviální "proč" (skrytý invariant, obezřetnost kolem Windows API), ne "co".
- **UI texty**: čeština jako primární jazyk (viz Fáze 8 — lokalizace), anglická
  lokalizace se doplňuje souběžně.
- **Architektura**: nové nízkoúrovňové Windows volání patří do `Diskora.Native`
  za rozhraním, ne přímo do `Diskora.App`.
- **Bezpečnost**: žádné skládání shell příkazů ze stringů, žádný vlastní zápis na
  raw filesystem struktury — viz [ARCHITECTURE.md](ARCHITECTURE.md) a
  [SECURITY.md](SECURITY.md).
- **Testy**: nová logika testovatelná bez reálného hardwaru musí mít jednotkové
  testy; kód závislý na WMI/IOCTL patří za mockovatelné rozhraní.

## Commit zprávy a PR

- Malé, zaměřené commity/PR raději než jeden obří.
- Popiš *proč* změna vznikla, ne jen *co* se změnilo (to je vidět z diffu).
- Aktualizuj `CHANGELOG.md` (sekce `Unreleased`) a u dokončených bodů i `ROADMAP.md`.

## Licence příspěvků

Odesláním příspěvku souhlasíte s jeho zveřejněním pod [GNU GPLv3](../LICENSE).
