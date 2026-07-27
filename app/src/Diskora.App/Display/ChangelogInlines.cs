using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Diskora.Core.Models;

namespace Diskora.App.Display;

/// <summary>
/// Naplní <see cref="TextBlock.Inlines"/> podle úseků položky changelogu.
/// Kolekce Inlines není závislostní vlastnost, takže na ni nejde bindovat
/// přímo - tohle je nejmenší způsob, jak text s odlišeným kódem vysázet jako
/// JEDEN plynule zalamovaný odstavec (ItemsControl nad úseky by zalomení
/// uvnitř věty rozbil).
/// </summary>
public static class ChangelogInlines
{
    public static readonly DependencyProperty SegmentsProperty =
        DependencyProperty.RegisterAttached(
            "Segments",
            typeof(IReadOnlyList<ChangelogSegment>),
            typeof(ChangelogInlines),
            new PropertyMetadata(null, OnSegmentsChanged));

    public static void SetSegments(DependencyObject element, IReadOnlyList<ChangelogSegment>? value) =>
        element.SetValue(SegmentsProperty, value);

    public static IReadOnlyList<ChangelogSegment>? GetSegments(DependencyObject element) =>
        (IReadOnlyList<ChangelogSegment>?)element.GetValue(SegmentsProperty);

    private static void OnSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock)
        {
            return;
        }

        textBlock.Inlines.Clear();

        if (e.NewValue is not IReadOnlyList<ChangelogSegment> segments)
        {
            return;
        }

        foreach (var segment in segments)
        {
            var run = new Run(segment.Text);

            if (segment.IsCode)
            {
                run.FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New, monospace");
            }

            if (segment.IsBold)
            {
                run.FontWeight = FontWeights.SemiBold;
            }

            textBlock.Inlines.Add(run);
        }
    }
}
