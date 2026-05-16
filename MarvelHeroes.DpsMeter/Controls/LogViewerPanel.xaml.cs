using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MarvelHeroes.DpsMeter.Controls;

/// <summary>
/// Live log viewer for the application's diagnostic log file at
/// <c>%LocalAppData%\MarvelHeroesComporator\dps-meter.log</c>.  Tail-style: keeps a
/// bounded ring buffer of the most recent lines, polls the file 2x/second for new bytes,
/// supports a live substring filter, auto-scrolls to the latest line by default, and lets
/// the user copy the currently-visible window to the clipboard.
///
/// <para><b>Memory safety:</b> the log can run to hundreds of MB over a session.  We don't
/// load the whole thing -- on initial load and on Reload, we seek to
/// <see cref="InitialTailBytes"/> bytes from the end and read forward.  A bounded
/// <see cref="MaxLines"/> ring then caps in-memory growth so the panel can run all day
/// without OOMing.  Lines that scroll off the top are dropped silently; the on-disk log
/// is the source of truth for the full history.</para>
///
/// <para><b>Sharing with the writer:</b> the presenter's <c>AppendLog</c> opens the file
/// with default share-mode for append.  We open with <see cref="FileShare.ReadWrite"/> so
/// concurrent reads here don't block writes there.  Position-tracking handles file
/// truncation / rotation by detecting "current length is smaller than where I was last"
/// and reloading the tail from scratch.</para>
/// </summary>
public partial class LogViewerPanel : UserControl
{
    // Path is the same one DpsOverlayPresenter.DiagnosticLogPath uses; resolved per-launch so
    // a roaming profile or %LocalAppData% redirection picks up correctly.
    private static readonly string LogFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarvelHeroesComporator");
    private static readonly string LogPath = Path.Combine(LogFolder, "dps-meter.log");

    /// <summary>On initial load + Reload, how many bytes back from EOF to read.  2 MB is
    /// roughly 20-30k lines for a typical Cerebro log -- enough context for the user to
    /// see "what just happened" without taking forever to render or eating memory.</summary>
    private const long InitialTailBytes = 2L * 1024 * 1024;

    /// <summary>Cap on in-memory lines.  Once exceeded, oldest lines are evicted from the
    /// front of the ObservableCollection.  5000 lines comfortably covers all sensible
    /// "scroll back a few minutes" use cases and keeps the UI fast even with the worst
    /// per-tick chatter (sniffer can emit ~50 lines/sec during dense gameplay).</summary>
    private const int MaxLines = 5000;

    // ── State ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>Full unfiltered buffer.  The visible ListBox is bound to <see cref="_view"/>
    /// (the filtered projection); changes to <see cref="_allLines"/> drive both.</summary>
    private readonly ObservableCollection<string> _allLines = new();

    /// <summary>Filtered projection -- equal to <see cref="_allLines"/> when filter is empty,
    /// otherwise a substring-matched subset.  Rebuilt on filter change; appended to as new
    /// lines arrive (only when they match the current filter).</summary>
    private readonly ObservableCollection<string> _view = new();

    /// <summary>Current filter substring; empty means "show everything".</summary>
    private string _filter = string.Empty;

    /// <summary>Last file position we read up to.  Polling reads from here to current length.</summary>
    private long _filePosition;

    /// <summary>Carry-over for partial lines split across read boundaries.  We buffer until we
    /// see a newline, then commit the completed line.  Without this, fast-moving writers can
    /// produce a half-finished line at the end of a read window and we'd display it as
    /// truncated then duplicate when the rest arrives.</summary>
    private string _pendingLine = string.Empty;

    /// <summary>Poll timer for tailing the log.  2 Hz keeps the file size check cheap (no
    /// FileSystemWatcher needed -- on Windows, the writer's buffered appends sometimes don't
    /// fire change notifications anyway).</summary>
    private DispatcherTimer? _tailTimer;

    public LogViewerPanel()
    {
        InitializeComponent();
        LogList.ItemsSource = _view;

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadFromDisk();

        // 2 Hz poll -- balances responsiveness vs the cost of a File.OpenRead per tick.
        _tailTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _tailTimer.Tick += OnTailTick;
        _tailTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Important: stop the timer when the tab is disposed / the window closes, otherwise
        // it keeps holding a strong reference to this panel via the Tick handler.
        if (_tailTimer != null)
        {
            _tailTimer.Stop();
            _tailTimer.Tick -= OnTailTick;
            _tailTimer = null;
        }
    }

    // ── File reading ──────────────────────────────────────────────────────────────────────────

    /// <summary>Initial load + Reload-button entry point.  Clears the in-memory buffer,
    /// seeks to roughly <see cref="InitialTailBytes"/> from EOF, reads forward.  Always
    /// rebuilds both _allLines and _view from scratch.</summary>
    private void LoadFromDisk()
    {
        _allLines.Clear();
        _view.Clear();
        _pendingLine = string.Empty;
        _filePosition = 0;

        if (!File.Exists(LogPath))
        {
            StatusText.Text = $"Log file does not exist yet at {LogPath}";
            return;
        }

        try
        {
            using var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            long startPosition = Math.Max(0, fs.Length - InitialTailBytes);
            // If we sliced into the middle of a UTF-8 codepoint or a partial line, advance
            // forward to the next newline so the first displayed line is whole.
            fs.Seek(startPosition, SeekOrigin.Begin);
            if (startPosition > 0)
            {
                int b;
                while ((b = fs.ReadByte()) != -1)
                {
                    if (b == '\n') break;
                }
            }
            _filePosition = fs.Position;

            // Read the rest as text.  We use StreamReader (which buffers) so .ReadLine works.
            using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                AppendLineUnfiltered(line);
            }
            _filePosition = fs.Position;

            RebuildFilteredView();
            UpdateStatus();
            ScrollToEndIfAutoScroll();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to read log: {ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>Tail tick: see if the file grew, read new bytes from where we left off, split
    /// into lines, append.  Handles truncation by detecting "current length less than my
    /// tracked position" and falling back to a full reload.</summary>
    private void OnTailTick(object? sender, EventArgs e)
    {
        if (!File.Exists(LogPath)) return;
        try
        {
            using var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            long length = fs.Length;
            if (length < _filePosition)
            {
                // File was truncated or replaced.  Reload from scratch.
                LoadFromDisk();
                return;
            }
            if (length == _filePosition) return;  // nothing new

            fs.Seek(_filePosition, SeekOrigin.Begin);
            int delta = checked((int)Math.Min(length - _filePosition, int.MaxValue));
            byte[] buf = new byte[delta];
            int read = fs.Read(buf, 0, delta);
            _filePosition += read;

            // Decode + split.  Carry over any trailing partial line into _pendingLine so a
            // half-flushed log entry doesn't display as truncated then duplicate next tick.
            string chunk = _pendingLine + Encoding.UTF8.GetString(buf, 0, read);
            int last = 0;
            for (int i = 0; i < chunk.Length; i++)
            {
                if (chunk[i] == '\n')
                {
                    int lineEnd = i;
                    if (lineEnd > last && chunk[lineEnd - 1] == '\r') lineEnd--;
                    AppendLineUnfiltered(chunk.Substring(last, lineEnd - last));
                    last = i + 1;
                }
            }
            _pendingLine = chunk.Substring(last);  // tail without a newline yet

            UpdateStatus();
            ScrollToEndIfAutoScroll();
        }
        catch
        {
            // Best-effort tail.  Polling continues; transient IO errors are normal when the
            // writer is mid-append (rare with ReadWrite share, but possible on slow disks).
        }
    }

    /// <summary>Append a single completed line to the unfiltered buffer.  Evicts oldest lines
    /// when the cap is hit; also appends to <see cref="_view"/> if it matches the current
    /// filter (so the visible projection stays in sync without rebuilding from scratch).</summary>
    private void AppendLineUnfiltered(string line)
    {
        _allLines.Add(line);
        while (_allLines.Count > MaxLines)
        {
            // Eviction: also remove from _view if the dropped line is currently visible.  Cheap
            // because the dropped line is always at position 0 of _view when there's no filter,
            // and a no-op when filtered (the dropped line typically wasn't visible anyway).
            string dropped = _allLines[0];
            _allLines.RemoveAt(0);
            if (_filter.Length == 0)
            {
                if (_view.Count > 0) _view.RemoveAt(0);
            }
            else if (LineMatches(dropped))
            {
                int idx = _view.IndexOf(dropped);
                if (idx >= 0) _view.RemoveAt(idx);
            }
        }

        if (_filter.Length == 0 || LineMatches(line))
            _view.Add(line);
    }

    private bool LineMatches(string line)
        => line.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>Reconstruct <see cref="_view"/> from <see cref="_allLines"/> under the current
    /// filter.  Called on filter-text changes; tail appends use the incremental path.</summary>
    private void RebuildFilteredView()
    {
        _view.Clear();
        if (_filter.Length == 0)
        {
            foreach (var line in _allLines) _view.Add(line);
        }
        else
        {
            foreach (var line in _allLines)
                if (LineMatches(line)) _view.Add(line);
        }
    }

    private void UpdateStatus()
    {
        long sizeKb = 0;
        try { if (File.Exists(LogPath)) sizeKb = new FileInfo(LogPath).Length / 1024; } catch { }
        string filterPart = _filter.Length == 0
            ? ""
            : $"  (filter: '{_filter}' → {_view.Count}/{_allLines.Count} lines)";
        StatusText.Text = $"showing {_view.Count} of {_allLines.Count} lines · {sizeKb:N0} KB on disk{filterPart}";
    }

    private void ScrollToEndIfAutoScroll()
    {
        if (AutoScrollCheck.IsChecked != true) return;
        if (_view.Count == 0) return;
        // Defer the scroll to after the layout pass so the just-added item is realised.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try { LogList.ScrollIntoView(_view[_view.Count - 1]); }
            catch { /* item may have been evicted between schedule and run; ignore */ }
        }), DispatcherPriority.Background);
    }

    // ── Toolbar handlers ──────────────────────────────────────────────────────────────────────

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _filter = FilterBox.Text ?? string.Empty;
        RebuildFilteredView();
        UpdateStatus();
        ScrollToEndIfAutoScroll();
    }

    private void AutoRefresh_OnChecked(object sender, RoutedEventArgs e)
    {
        _tailTimer?.Start();
    }

    private void AutoRefresh_OnUnchecked(object sender, RoutedEventArgs e)
    {
        _tailTimer?.Stop();
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        LoadFromDisk();
    }

    /// <summary>Wipe the on-screen buffer without touching the log file on disk.  After
    /// clearing, the tail-poll continues from the current file position -- so the next
    /// new line written to disk shows up in the (empty) view.  Lets the user "start
    /// fresh" before reproducing a behaviour, see only what happens AFTER they hit Clear.
    /// Reload pulls the history back in if they need it.</summary>
    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _allLines.Clear();
        _view.Clear();
        _pendingLine = string.Empty;
        // _filePosition stays put -- tail-tick keeps reading from where it was, so future
        // appends show up but nothing from before the clear comes back.
        UpdateStatus();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        // If the user has selected specific lines, copy those; otherwise copy everything
        // currently in the visible (filtered) projection.  Lines are joined with platform
        // newlines so pasting into Discord / Notepad / a forum thread looks right.
        var lines = LogList.SelectedItems.Count > 0
            ? LogList.SelectedItems.Cast<string>()
            : _view.AsEnumerable();
        var text = string.Join(Environment.NewLine, lines);
        try { Clipboard.SetText(text); }
        catch { /* clipboard occasionally fails when another app is mid-paste; nothing to do */ }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(LogFolder);
            Process.Start(new ProcessStartInfo
            {
                FileName        = LogFolder,
                UseShellExecute = true,
            });
        }
        catch { /* user can navigate manually if Explorer is unavailable */ }
    }
}
