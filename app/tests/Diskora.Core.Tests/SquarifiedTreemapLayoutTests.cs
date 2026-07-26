using Diskora.Core.Layout;

namespace Diskora.Core.Tests;

public sealed class SquarifiedTreemapLayoutTests
{
    [Fact]
    public void Layout_EmptyWeights_ReturnsEmptyList()
    {
        var result = SquarifiedTreemapLayout.Layout([], 0, 0, 100, 100);

        Assert.Empty(result);
    }

    [Fact]
    public void Layout_ZeroSizeBounds_ReturnsEmptyRectsWithoutThrowing()
    {
        var result = SquarifiedTreemapLayout.Layout([1, 2, 3], 0, 0, 0, 100);

        Assert.Equal(3, result.Count);
        Assert.All(result, rect => Assert.Equal(0, rect.Width));
    }

    [Fact]
    public void Layout_SingleWeight_FillsEntireRect()
    {
        var result = SquarifiedTreemapLayout.Layout([42], 10, 20, 200, 100);

        var rect = Assert.Single(result);
        Assert.Equal(10, rect.X);
        Assert.Equal(20, rect.Y);
        Assert.Equal(200, rect.Width);
        Assert.Equal(100, rect.Height);
    }

    [Fact]
    public void Layout_TwoEqualWeights_WideRect_SplitsSideBySideIntoSquares()
    {
        var result = SquarifiedTreemapLayout.Layout([1, 1], 0, 0, 200, 100);

        Assert.Equal(2, result.Count);
        Assert.All(result, rect =>
        {
            Assert.Equal(100, rect.Width, precision: 6);
            Assert.Equal(100, rect.Height, precision: 6);
        });
        var xs = result.Select(r => r.X).OrderBy(v => v).ToList();
        Assert.Equal(0, xs[0], precision: 6);
        Assert.Equal(100, xs[1], precision: 6);
    }

    [Fact]
    public void Layout_TwoEqualWeights_TallRect_SplitsTopAndBottomIntoSquares()
    {
        var result = SquarifiedTreemapLayout.Layout([1, 1], 0, 0, 100, 200);

        Assert.Equal(2, result.Count);
        Assert.All(result, rect =>
        {
            Assert.Equal(100, rect.Width, precision: 6);
            Assert.Equal(100, rect.Height, precision: 6);
        });
        var ys = result.Select(r => r.Y).OrderBy(v => v).ToList();
        Assert.Equal(0, ys[0], precision: 6);
        Assert.Equal(100, ys[1], precision: 6);
    }

    [Fact]
    public void Layout_NonPositiveWeight_GetsEmptyRectAndIsExcludedFromArea()
    {
        var result = SquarifiedTreemapLayout.Layout([50, 0, -5, 50], 0, 0, 200, 100);

        Assert.Equal(new TreemapRect(), result[1]);
        Assert.Equal(new TreemapRect(), result[2]);
        Assert.True(result[0].Width * result[0].Height > 0);
        Assert.True(result[3].Width * result[3].Height > 0);
    }

    [Fact]
    public void Layout_PreservesOriginalIndexOrderRegardlessOfInternalSorting()
    {
        var result = SquarifiedTreemapLayout.Layout([1, 100, 1], 0, 0, 300, 100);

        var largestArea = result[1].Width * result[1].Height;
        var smallestArea1 = result[0].Width * result[0].Height;
        var smallestArea2 = result[2].Width * result[2].Height;

        Assert.True(largestArea > smallestArea1);
        Assert.True(largestArea > smallestArea2);
    }

    [Theory]
    [InlineData(new double[] { 10, 5, 3, 2, 1 })]
    [InlineData(new double[] { 1, 1, 1, 1, 1, 1, 1 })]
    [InlineData(new double[] { 1000, 1, 1, 1 })]
    public void Layout_TotalRectArea_ConservesInputArea(double[] weights)
    {
        const double width = 400;
        const double height = 250;

        var result = SquarifiedTreemapLayout.Layout(weights, 0, 0, width, height);

        var totalArea = result.Sum(rect => rect.Width * rect.Height);

        Assert.Equal(width * height, totalArea, precision: 6);
    }

    [Fact]
    public void Layout_RectsDoNotOverlap()
    {
        var weights = new double[] { 40, 25, 15, 10, 5, 3, 2 };
        var result = SquarifiedTreemapLayout.Layout(weights, 0, 0, 500, 300);

        for (var i = 0; i < result.Count; i++)
        {
            for (var j = i + 1; j < result.Count; j++)
            {
                Assert.False(Overlaps(result[i], result[j]), $"Rects {i} and {j} overlap");
            }
        }
    }

    private static bool Overlaps(TreemapRect a, TreemapRect b)
    {
        const double epsilon = 1e-6;
        return a.X + epsilon < b.X + b.Width && b.X + epsilon < a.X + a.Width &&
               a.Y + epsilon < b.Y + b.Height && b.Y + epsilon < a.Y + a.Height;
    }
}
