using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace MarvelHeroes.DpsMeter.Windows;

/// <summary>
/// Bundled <c>CHANGELOG.md</c> viewer surfaced from <b>Settings → About →
/// View changelog</b>.  Reads the markdown text from the WPF resource bundle
/// (<c>pack://application:,,,/CHANGELOG.md</c>) at construction time and
/// dumps it into a read-only TextBox.  No Markdown rendering -- the source
/// is meant to be readable as plain text, and shipping a Markdown renderer
/// would add a dependency for one feature.
///
/// <para><b>"Open on GitHub" fallback</b> — clicking it opens the canonical
/// changelog at https://github.com/vash-silver/cerebro/blob/main/CHANGELOG.md
/// in the user's default browser, in case they want to skim a newer revision
/// than the version they're running.</para>
/// </summary>
public partial class ChangelogWindow : Window
{
    /// <summary>Public canonical URL of the changelog.  Hardcoded because
    /// it's tied to the release repo we publish to; if the repo ever moves,
    /// update this string + the URL in <see cref="Services.UpdateChecker"/>.</summary>
    private const string GithubChangelogUrl = "https://github.com/vash-silver/cerebro/blob/main/CHANGELOG.md";

    public ChangelogWindow()
    {
        InitializeComponent();

        // Stamp the running version into the subtitle so the user can
        // cross-reference "what I'm running" with the changelog entries.
        try
        {
            VersionSubtitle.Text = $"(running v{Services.CerebroVersion.DisplayVersion})";
        }
        catch { VersionSubtitle.Text = ""; }

        ChangelogText.Text = LoadChangelogText();
    }

    /// <summary>Pull the bundled <c>CHANGELOG.md</c> out of the WPF resource
    /// bundle as a UTF-8 string.  Returns a friendly fallback message when
    /// the resource isn't found (shouldn't happen in a built EXE, but the
    /// dev build before the first <c>dotnet build</c> after adding the
    /// resource entry would hit this path).</summary>
    private static string LoadChangelogText()
    {
        try
        {
            var info = Application.GetResourceStream(new Uri("pack://application:,,,/CHANGELOG.md"));
            if (info?.Stream == null)
                return "Changelog resource not found.  See " + GithubChangelogUrl;
            using var reader = new StreamReader(info.Stream, System.Text.Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            return $"Failed to load bundled changelog: {ex.GetType().Name}: {ex.Message}\n\nSee {GithubChangelogUrl}";
        }
    }

    private void OpenOnGithubButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = GithubChangelogUrl,
                UseShellExecute = true,
            });
        }
        catch { /* shell-execute can fail on locked-down systems; non-fatal */ }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
