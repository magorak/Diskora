namespace Diskora.Core.Smart;

/// <summary>
/// Katalog nejběžnějších S.M.A.R.T. atributů (ID nejsou napříč výrobci
/// standardizovaná stoprocentně, ale tato sada odpovídá běžné praxi napříč
/// ATA/SATA HDD i SSD disky). Cílem je srozumitelné vysvětlení rizika pro
/// běžného uživatele, ne úplná technická specifikace - to je odlišující
/// prvek Diskory oproti nástrojům, které jen vypíší syrová čísla.
/// </summary>
public static class SmartAttributeCatalog
{
    private static readonly IReadOnlyDictionary<byte, SmartAttributeDefinition> Definitions =
        new List<SmartAttributeDefinition>
        {
            new(1, "Míra chyb při čtení", "Jak často disk musí opravovat chyby při čtení dat. Občasné nízké hodnoty jsou normální."),
            new(3, "Doba roztočení", "Jak dlouho trvá roztočit talíře na provozní otáčky (jen mechanické HDD). Postupné prodlužování ukazuje na opotřebení motoru."),
            new(4, "Počet roztočení", "Kolikrát se disk roztočil a zastavil. Informativní hodnota."),
            new(5, "Přemapované sektory", "Počet vadných sektorů, které disk nahradil záložními. Jakýkoli nárůst nad nulu je varovný signál opotřebení."),
            new(7, "Míra chyb vyhledávání", "Jak často dochází k chybě při polohování hlaviček (jen mechanické HDD)."),
            new(9, "Doba provozu", "Celkový počet hodin, kdy byl disk zapnutý. Informativní hodnota, sama o sobě neznamená problém."),
            new(10, "Opakování roztočení", "Kolikrát disk musel opakovaně zkoušet roztočit talíře (jen mechanické HDD)."),
            new(11, "Opakování kalibrace", "Kolikrát disk musel zopakovat kalibraci polohy hlaviček (jen mechanické HDD)."),
            new(12, "Počet zapnutí", "Kolikrát byl disk zapnut/vypnut. Informativní hodnota."),
            new(173, "Opotřebení paměťových buněk (SSD)", "Míra opotřebení flash pamětí u SSD. Klesající normalizovaná hodnota značí blížící se konec životnosti."),
            new(177, "Rozsah opotřebení (SSD)", "Rozdíl v opotřebení mezi jednotlivými bloky paměti SSD."),
            new(181, "Chyby zápisu do bloku (SSD)", "Počet neúspěšných pokusů o zápis do paměťového bloku."),
            new(182, "Chyby mazání bloku (SSD)", "Počet neúspěšných pokusů o smazání paměťového bloku."),
            new(187, "Nahlášené neopravitelné chyby", "Chyby, které disk nedokázal interně opravit. Nenulová hodnota je vážný signál."),
            new(188, "Vypršení časového limitu příkazu", "Kolikrát disk nestihl odpovědět na příkaz včas."),
            new(190, "Teplota (vzduch)", "Aktuální provozní teplota disku ve stupních Celsia."),
            new(192, "Počet nouzových vypnutí", "Kolikrát disk zaparkoval hlavičky kvůli ztrátě napájení nebo nárazu."),
            new(193, "Počet pohybových cyklů", "Kolikrát se hlavičky přesunuly do parkovací pozice a zpět (jen mechanické HDD)."),
            new(194, "Teplota", "Aktuální provozní teplota disku ve stupních Celsia. Trvale vysoká teplota zkracuje životnost."),
            new(196, "Události přemapování", "Kolikrát disk provedl přemapování vadného sektoru. Related k atributu 5."),
            new(197, "Čekající vadné sektory", "Sektory podezřelé z vadnosti, čekající na opravu při dalším zápisu. Nenulová hodnota vyžaduje pozornost."),
            new(198, "Neopravitelné sektory (offline)", "Sektory, které disk nedokázal opravit ani offline testem. Vážný signál blížícího se selhání."),
            new(199, "Chyby UDMA CRC", "Chyby přenosu dat po kabelu/sběrnici - často ukazuje na vadný kabel nebo konektor, ne nutně na vadný disk."),
            new(200, "Míra chyb zápisu", "Jak často se nepodařilo správně zapsat data na plotnu (jen mechanické HDD). Nazývá se také Multi-Zone Error Rate."),
            new(233, "Ukazatel opotřebení média (SSD)", "Souhrnný ukazatel zbývající životnosti SSD, 100 = nové, 0 = konec životnosti."),
            new(241, "Celkem zapsáno (SSD)", "Celkové množství dat zapsaných na SSD za celou dobu životnosti."),
            new(242, "Celkem přečteno (SSD)", "Celkové množství dat přečtených z SSD za celou dobu životnosti."),
        }.ToDictionary(d => d.Id);

    public static SmartAttributeDefinition? Find(byte id) =>
        Definitions.TryGetValue(id, out var definition) ? definition : null;

    public static string GetName(byte id) => Find(id)?.Name ?? $"Neznámý atribut ({id})";

    public static string GetExplanation(byte id) =>
        Find(id)?.Explanation ?? "Pro tento atribut zatím nemáme podrobné vysvětlení.";
}
