using Diskora.Core.Smart;

namespace Diskora.Core.Tests;

public class SmartAttributeCatalogTests
{
    [Fact]
    public void GetName_KnownAttribute_ReturnsCzechName()
    {
        var name = SmartAttributeCatalog.GetName(5);

        Assert.Equal("Přemapované sektory", name);
    }

    [Fact]
    public void GetName_UnknownAttribute_ReturnsFallbackWithId()
    {
        var name = SmartAttributeCatalog.GetName(250);

        Assert.Contains("250", name);
    }

    [Fact]
    public void GetExplanation_UnknownAttribute_ReturnsGenericFallback()
    {
        var explanation = SmartAttributeCatalog.GetExplanation(250);

        Assert.False(string.IsNullOrWhiteSpace(explanation));
    }

    [Theory]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    [InlineData((byte)11)]
    [InlineData((byte)200)]
    public void Find_AtributyZeZiveTestovanehoHdd_JsouVKatalogu(byte id)
    {
        // Tyhle ID reálně hlásí WDC WD40EZRZ v testovacím prostředí a do teď
        // se zobrazovaly jako „Neznámý atribut" - odhaleno až po opravě legacy
        // cesty, kdy poprvé začaly ATA atributy vůbec dorazit do UI.
        Assert.NotNull(SmartAttributeCatalog.Find(id));
    }

    [Fact]
    public void Find_KnownAttribute_HasNonEmptyNameAndExplanation()
    {
        var definition = SmartAttributeCatalog.Find(197);

        Assert.NotNull(definition);
        Assert.False(string.IsNullOrWhiteSpace(definition!.Name));
        Assert.False(string.IsNullOrWhiteSpace(definition.Explanation));
    }
}
