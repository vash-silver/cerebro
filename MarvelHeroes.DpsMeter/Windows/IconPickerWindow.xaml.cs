using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MarvelHeroes.DpsMeter.Services;

namespace MarvelHeroes.DpsMeter.Windows;

/// <summary>
/// Modal grid picker for the ~2.3k bundled in-game power icons.  Used by
/// <c>BuffTrackerPanel</c> as an alternative to the file picker -- the user can pick a
/// game-extracted icon visually rather than supplying a custom file from disk.
///
/// <para>Built-in features:</para>
/// <list type="bullet">
///   <item>Live substring filter on the search box -- typing "nightcrawler" narrows the
///         grid to icons whose basename contains that substring, case-insensitively.</item>
///   <item>Sorted: when no filter is active, the catalog is rendered in name order (which
///         conveniently clusters per-hero powers together because basenames are
///         <c>power_&lt;hero&gt;_&lt;ability&gt;</c>).  With a filter, results stay in name
///         order so re-typing similar searches gives stable ordering.</item>
///   <item>Lazy thumbnail decode: each row's <see cref="IconRowVm.Image"/> is decoded the
///         first time the row's data template binds, not all at once on window open --
///         keeps the open-dialog latency under a second even at the full 2.3k size.</item>
/// </list>
///
/// <para>Result protocol: <see cref="SelectedBasename"/> is set when the user clicks an
/// icon; the window then closes with <c>DialogResult = true</c>.  Cancel / Esc / window-X
/// close with <c>DialogResult = false</c> and a null SelectedBasename.</para>
/// </summary>
public partial class IconPickerWindow : Window
{
    /// <summary>The basename the user clicked (e.g. <c>"power_nightcrawler_teleport"</c>),
    /// or <c>null</c> when the user cancelled.  Read after <see cref="Window.ShowDialog"/>
    /// returns; use <c>BundledIconCatalog.GetPackUri</c> to translate to a loadable URI.</summary>
    public string? SelectedBasename { get; private set; }

    /// <summary>Cache the row VMs the first time we build them so re-filtering doesn't
    /// re-decode thumbnails.  The collection is rebuilt as a filtered View each time the
    /// search box changes, but the underlying VM (with the cached BitmapImage) is shared.</summary>
    private readonly List<IconRowVm> _allRows;

    public IconPickerWindow()
    {
        InitializeComponent();
        _allRows = BundledIconCatalog.Basenames.Select(bn => new IconRowVm(bn)).ToList();
        // First-paint shows everything; the user types to narrow.  Dispatcher-deferred
        // initial filter so the WindowChrome paints first; without this the modal looks
        // frozen for ~300 ms while the 2.3 k Image elements realize.
        Loaded += (_, _) =>
        {
            SearchBox.Focus();
            Dispatcher.BeginInvoke(new Action(() => ApplyFilter("")), DispatcherPriority.Background);
        };
    }

    private void ApplyFilter(string filter)
    {
        IEnumerable<IconRowVm> rows = _allRows;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            string needle = filter.Trim();
            rows = _allRows.Where(r => r.Basename.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        var materialized = rows.ToList();
        IconGrid.ItemsSource = materialized;
        StatusLine.Text = string.IsNullOrWhiteSpace(filter)
            ? $"{materialized.Count} icons"
            : $"{materialized.Count} of {_allRows.Count} match '{filter.Trim()}'";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => ApplyFilter(SearchBox.Text);

    private void ClearFilterButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        SearchBox.Focus();
    }

    private void IconButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not string basename || string.IsNullOrEmpty(basename)) return;
        SelectedBasename = basename;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedBasename = null;
        DialogResult = false;
        Close();
    }

    /// <summary>Per-row VM bound by the DataTemplate.  <see cref="Image"/> is decoded
    /// lazily on first access so opening the modal isn't blocked decoding 2.3k thumbnails
    /// up front -- WPF's data-template realization triggers Image getter when the row
    /// scrolls into view.</summary>
    public sealed class IconRowVm
    {
        public string Basename { get; }
        private BitmapImage? _image;

        public IconRowVm(string basename) { Basename = basename; }

        public ImageSource Image
        {
            get
            {
                if (_image != null) return _image;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption      = BitmapCacheOption.OnLoad;
                bmp.CreateOptions    = BitmapCreateOptions.IgnoreColorProfile;
                bmp.UriSource        = new Uri(BundledIconCatalog.GetPackUri(Basename), UriKind.Absolute);
                // Decode at 64px (chip-sized) so the picker doesn't burn memory on
                // higher-res thumbnails the user only sees as 40px tiles.
                bmp.DecodePixelWidth = 64;
                bmp.EndInit();
                if (bmp.CanFreeze) bmp.Freeze();
                _image = bmp;
                return bmp;
            }
        }
    }
}
