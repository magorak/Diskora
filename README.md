# Diskora

Diskora je open-source (GPLv3) desktopová aplikace pro Windows pro kontrolu, opravu
a analýzu disků — v jednom nástroji spojuje to, co dnes vyžaduje několik samostatných
programů:

- **Kontrola a oprava integrity disku** (ve stylu ScanDisk / CHKDSK)
- **S.M.A.R.T. monitoring zdraví disku** (ve stylu CrystalDiskInfo)
- **Analýza zaplněnosti disku** — treemapa, duplicity, velké/staré soubory (ve stylu TreeSize)
- **TRIM (SSD) a defragmentace (HDD)** s automatickou detekcí typu disku
- **Podpora virtuálních disků a obrazů** — VHD, VHDX, IMG, ISO

Podporuje HDD, SSD, NVMe i USB disky.

Bez telemetrie, bez reklam, bez placené verze. Celý kód je otevřený pod
[GNU GPLv3](LICENSE).

## Stav projektu

Diskora je ve rané fázi vývoje. Aktuální rozsah a plán najdete v [ROADMAP.md](ROADMAP.md).

## Struktura repozitáře

- [`app/`](app/) — C# / .NET WPF desktopová aplikace
- [`web/`](web/) — produktový web a dokumentace/nápověda
- [`docs/`](docs/) — vývojářská dokumentace (architektura, přispívání, bezpečnost)

## Vývoj

```powershell
dotnet build app/Diskora.slnx
dotnet test app/Diskora.slnx
dotnet run --project app/src/Diskora.App
```

Podrobnosti o architektuře viz [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
Jak přispět viz [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md).
Bezpečnostní politika viz [docs/SECURITY.md](docs/SECURITY.md).

## Licence

[GNU General Public License v3.0](LICENSE).
