<#
.SYNOPSIS
    Vytvoří portable (single-file, self-contained) build Diskory - žádný instalátor,
    stačí zkopírovat výsledný .exe kamkoliv a spustit, není potřeba .NET runtime
    nainstalovaný v cíli.

.DESCRIPTION
    Skutečný instalátor (Inno Setup/MSIX) zůstává otevřený bod v ROADMAP.md -
    tenhle skript řeší jen portable variantu, kterou šlo v tomto prostředí ověřit
    beze zbytku potřebné infrastruktury (code signing certifikát, Inno Setup).
    Výsledek se necommituje (viz .gitignore, `/dist/`) - generuje se čerstvě podle
    potřeby, stejný princip jako `app/sbom/`.
#>

param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\dist\portable-app")
)

dotnet publish (Join-Path $PSScriptRoot "src\Diskora.App\Diskora.App.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $OutputDirectory
