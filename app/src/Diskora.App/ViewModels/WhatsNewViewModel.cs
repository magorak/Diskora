using System.Collections.ObjectModel;
using Diskora.Core.Changelog;
using Diskora.Core.Models;

namespace Diskora.App.ViewModels;

public sealed class WhatsNewViewModel
{
    public WhatsNewViewModel(string changelogMarkdown, string version)
    {
        Version = version;

        foreach (var release in ChangelogParser.Parse(changelogMarkdown))
        {
            Releases.Add(new ChangelogReleaseRowViewModel(release));
        }
    }

    public string Version { get; }

    public string Heading => $"Co je nového v Diskoře {Version}";

    public ObservableCollection<ChangelogReleaseRowViewModel> Releases { get; } = [];

    public bool IsEmpty => Releases.Count == 0;
}

public sealed class ChangelogReleaseRowViewModel(ChangelogRelease release)
{
    public string Title { get; } = release.Title;

    public IReadOnlyList<ChangelogSectionRowViewModel> Sections { get; } =
        release.Sections
            .Where(section => section.Items.Count > 0)
            .Select(section => new ChangelogSectionRowViewModel(section))
            .ToList();
}

public sealed class ChangelogSectionRowViewModel(ChangelogSection section)
{
    public string Category { get; } = section.Category;

    public IReadOnlyList<ChangelogItemRowViewModel> Items { get; } =
        section.Items.Select(item => new ChangelogItemRowViewModel(item)).ToList();
}

public sealed class ChangelogItemRowViewModel(string text)
{
    public IReadOnlyList<ChangelogSegment> Segments { get; } = ChangelogParser.ParseInline(text);
}
