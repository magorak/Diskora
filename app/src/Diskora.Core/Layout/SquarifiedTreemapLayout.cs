namespace Diskora.Core.Layout;

/// <summary>
/// Squarified treemap layout (Bruls, Huizing, van Wijk, 2000) - rozmístí položky do
/// obdélníku tak, aby se poměr stran jednotlivých buněk blížil čtverci, na rozdíl od
/// naivního "slice-and-dice" řazení, kde malé položky snadno skončí jako nečitelné
/// tenké proužky. Port široce používaného referenčního algoritmu (viz balíček
/// "squarify"). Čistě geometrické - žádná závislost na WPF ani jiném UI frameworku,
/// aby šel algoritmus jednotkově testovat bez oken/Canvasu.
/// </summary>
public static class SquarifiedTreemapLayout
{
    /// <summary>
    /// Vrací obdélník pro každou váhu ve stejném pořadí, jako byly zadané. Váhy &lt;= 0
    /// dostanou prázdný obdélník (0,0,0,0) a do rozvržení se nepočítají - zbylé kladné
    /// váhy si mezi sebou rozdělí celou plochu. Pro nejlepší poměr stran (nejčtvercovější
    /// buňky) se doporučuje volat se sestupně seřazenými váhami, ale algoritmus je
    /// korektní (obdélníky beze zbytku a překryvu pokryjí celou plochu) i bez seřazení.
    /// </summary>
    public static IReadOnlyList<TreemapRect> Layout(IReadOnlyList<double> weights, double x, double y, double width, double height)
    {
        var result = new TreemapRect[weights.Count];
        if (weights.Count == 0 || width <= 0 || height <= 0)
        {
            return result;
        }

        var ordered = weights
            .Select((weight, index) => (Weight: weight, Index: index))
            .Where(item => item.Weight > 0)
            .OrderByDescending(item => item.Weight)
            .ToList();

        if (ordered.Count == 0)
        {
            return result;
        }

        var totalWeight = ordered.Sum(item => item.Weight);
        var scale = width * height / totalWeight;

        var areas = ordered.Select(item => item.Weight * scale).ToList();
        var indices = ordered.Select(item => item.Index).ToList();

        Squarify(areas, indices, x, y, width, height, result);
        return result;
    }

    private static void Squarify(
        List<double> areas, List<int> indices, double x, double y, double width, double height, TreemapRect[] result)
    {
        while (areas.Count > 0 && width > 0 && height > 0)
        {
            var splitCount = 1;
            while (splitCount < areas.Count &&
                   WorstAspectRatio(areas, splitCount, x, y, width, height) >=
                   WorstAspectRatio(areas, splitCount + 1, x, y, width, height))
            {
                splitCount++;
            }

            var rowAreas = areas.GetRange(0, splitCount);
            var rowIndices = indices.GetRange(0, splitCount);

            var rowRects = LayoutStrip(rowAreas, x, y, width, height);
            for (var i = 0; i < rowRects.Count; i++)
            {
                result[rowIndices[i]] = rowRects[i];
            }

            (x, y, width, height) = Leftover(rowAreas, x, y, width, height);
            areas = areas.GetRange(splitCount, areas.Count - splitCount);
            indices = indices.GetRange(splitCount, indices.Count - splitCount);
        }
    }

    /// <summary>Nejhorší (największí) poměr stran, kdyby prvních <paramref name="count"/> položek
    /// tvořilo jeden pruh - používá se k rozhodnutí, kolik položek do pruhu ještě vzít.</summary>
    private static double WorstAspectRatio(List<double> areas, int count, double x, double y, double width, double height)
    {
        if (count > areas.Count)
        {
            return double.PositiveInfinity;
        }

        var rects = LayoutStrip(areas.GetRange(0, count), x, y, width, height);

        var worst = 0.0;
        foreach (var rect in rects)
        {
            var ratio = rect.Width <= 0 || rect.Height <= 0
                ? double.PositiveInfinity
                : Math.Max(rect.Width / rect.Height, rect.Height / rect.Width);
            worst = Math.Max(worst, ratio);
        }

        return worst;
    }

    /// <summary>Umístí jeden pruh položek podél kratší strany zbývajícího obdélníku - pokud je
    /// obdélník široký, pruh je úzký svislý sloupec (položky navrstvené odshora dolů); pokud
    /// je vysoký, pruh je nízký vodorovný řádek (položky vedle sebe zleva doprava).</summary>
    private static List<TreemapRect> LayoutStrip(List<double> areas, double x, double y, double width, double height) =>
        width >= height ? LayoutColumnStrip(areas, x, y, height) : LayoutRowStrip(areas, x, y, width);

    private static List<TreemapRect> LayoutColumnStrip(List<double> areas, double x, double y, double height)
    {
        var stripWidth = areas.Sum() / height;
        var rects = new List<TreemapRect>(areas.Count);
        var currentY = y;

        foreach (var area in areas)
        {
            var itemHeight = area / stripWidth;
            rects.Add(new TreemapRect(x, currentY, stripWidth, itemHeight));
            currentY += itemHeight;
        }

        return rects;
    }

    private static List<TreemapRect> LayoutRowStrip(List<double> areas, double x, double y, double width)
    {
        var stripHeight = areas.Sum() / width;
        var rects = new List<TreemapRect>(areas.Count);
        var currentX = x;

        foreach (var area in areas)
        {
            var itemWidth = area / stripHeight;
            rects.Add(new TreemapRect(currentX, y, itemWidth, stripHeight));
            currentX += itemWidth;
        }

        return rects;
    }

    private static (double X, double Y, double Width, double Height) Leftover(
        List<double> rowAreas, double x, double y, double width, double height)
    {
        if (width >= height)
        {
            var stripWidth = rowAreas.Sum() / height;
            return (x + stripWidth, y, width - stripWidth, height);
        }

        var stripHeight = rowAreas.Sum() / width;
        return (x, y + stripHeight, width, height - stripHeight);
    }
}
