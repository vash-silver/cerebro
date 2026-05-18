using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MarvelHeroes.DpsMeter.Services;

namespace MarvelHeroes.DpsMeter.Controls;

/// <summary>
/// WeakAuras-style free-layout buff display for the floating buff overlay.  Each
/// tracked buff renders as a bare icon (no chrome) at a user-positioned (X, Y) on a
/// Canvas, sized to a user-configurable square.  In edit mode the user drags icons to
/// place them and resizes via a corner grip; on release the new geometry persists to
/// <see cref="TrackedBuffsConfig.Layouts"/>.
///
/// <para>Incremental sync model: VMs implement <see cref="INotifyPropertyChanged"/>
/// and live in an <see cref="ObservableCollection{T}"/> assigned to the host once.
/// <see cref="UpdateBuffs"/> diffs the watchlist against existing VMs -- existing
/// chips keep their visual containers (and any active mouse capture from a drag),
/// only the changed properties re-bind.  This is what makes fast drags feel smooth:
/// the captured Border element is never destroyed mid-gesture.</para>
///
/// <para>The "edit mode" toggle is owned by the host (<c>BuffOverlayWindow</c>) and
/// pushed to this control via <see cref="SetEditMode"/>.  Edit mode makes the chips
/// draggable (mouse capture grabs them), shows a dashed border, exposes the resize
/// grip, and changes the cursor to a move-glyph.  Non-edit mode is click-through ,
/// the chips render but don't capture mouse input, so the game gets clicks normally.</para>
/// </summary>
public partial class FreeLayoutBuffPanel : UserControl
{
    private bool _editMode;
    /// <summary>Live collection of widget VMs.  Assigned to <c>WidgetHost.ItemsSource</c>
    /// exactly once (in the ctor).  All subsequent changes are in-place: existing VMs
    /// keep their visual containers (and any active mouse capture), so a fast drag
    /// won't lose tracking just because <see cref="UpdateBuffs"/> fired mid-gesture.</summary>
    private readonly ObservableCollection<WidgetVm> _widgets = new();

    // Drag state.  Captured once on MouseDown, cleared on MouseUp.  Holding state on
    // the panel rather than per-chip because only one chip can be dragged at a time
    // anyway, and global state simplifies the multi-handler routing.
    private WidgetVm? _draggingChip;
    private Point _dragStartPanelPt;
    private double _dragStartChipX, _dragStartChipY;
    private bool _draggingResize;
    private double _dragStartChipSize;

    public FreeLayoutBuffPanel()
    {
        InitializeComponent();
        WidgetHost.ItemsSource = _widgets;
    }

    /// <summary>Toggle edit mode.  In edit mode chips show a dashed border, a resize
    /// grip in the bottom-right, and react to mouse-drag for repositioning.  Out of
    /// edit mode chips render normally and ignore mouse input (the chip's
    /// <c>Background=Transparent</c> means hit-tests fall through to underlying
    /// elements).</summary>
    public void SetEditMode(bool edit)
    {
        if (_editMode == edit) return;
        _editMode = edit;
        // Re-flow current VMs to pick up the new visibility values for edit-only chrome
        // (borders, resize grip, cursor).  INPC handles propagation -- no ItemsSource reset.
        foreach (var vm in _widgets)
        {
            vm.EditBorderBrush      = _editMode ? EditBorderActiveBrush  : EditBorderInactiveBrush;
            vm.EditBorderThickness  = _editMode ? new Thickness(1)       : new Thickness(0);
            vm.ResizeHandleVisibility = _editMode ? Visibility.Visible    : Visibility.Collapsed;
            vm.ChipCursor           = _editMode ? Cursors.SizeAll         : Cursors.Arrow;
        }
    }

    /// <summary>Push a fresh active-buffs snapshot to the panel.  Incrementally syncs
    /// the live <see cref="_widgets"/> collection against the watchlist: existing VMs
    /// keep their containers (and any mid-drag mouse capture), new buffs add to the
    /// end, untracked ones get removed.  Same cadence as
    /// <see cref="BuffStripPanel.UpdateBuffs"/>.</summary>
    public void UpdateBuffs(IReadOnlyList<ActiveBuff> active, DateTime nowUtc)
    {
        var cfg = TrackedBuffsConfig.Current;

        // In free-layout mode the panel always renders only the user's watchlist --
        // it's a WeakAuras-style HUD, not a status panel.  Showing every classifier,
        // approved chip would defeat the purpose of the user picking specific buffs.
        if (cfg.Tracked.Count == 0)
        {
            _widgets.Clear();
            return;
        }

        // Group active buffs by chip short name so multi-stack buffs collapse into one
        // widget (with a stack-count overlay).  Identical to BuffStripPanel's grouping
        // logic; lifted out so we don't have to share state.
        var groups = new Dictionary<string, GroupAcc>(StringComparer.OrdinalIgnoreCase);
        foreach (var buff in active)
        {
            string shortName = BuffDisplayClassifier.ShortenForChip(buff.DisplayName);
            if (!cfg.Tracked.Contains(shortName)) continue;
            if (!groups.TryGetValue(shortName, out var acc))
            {
                acc = new GroupAcc { ShortName = shortName };
                groups[shortName] = acc;
            }
            acc.Count++;
            if (buff.ExpiresUtc.HasValue && buff.ExpiresUtc.Value > acc.LatestExpiry)
                acc.LatestExpiry = buff.ExpiresUtc.Value;
        }

        // Build the desired set of (shortName -> snapshot) for this tick.
        var desired = new Dictionary<string, ChipSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var shortName in cfg.Tracked)
        {
            bool isActive = groups.TryGetValue(shortName, out var grp);
            if (!isActive && !_editMode) continue;  // hide inactive auras out of edit mode
            double remainingSec = 0;
            int stackCount = 0;
            if (isActive && grp != null)
            {
                remainingSec = Math.Max(0, (grp.LatestExpiry - nowUtc).TotalSeconds);
                stackCount = grp.Count;
            }
            desired[shortName] = new ChipSnapshot(shortName, isActive, remainingSec, stackCount);
        }

        // Pass 1: remove VMs that are no longer in the desired set.  Iterate backwards
        // so the ObservableCollection index math doesn't get confused as we remove.
        // Don't remove the currently-dragging VM even if it's no longer tracked --
        // that would orphan the mouse capture mid-gesture.  It'll be reaped on the
        // next tick after the user releases.
        for (int i = _widgets.Count - 1; i >= 0; i--)
        {
            var vm = _widgets[i];
            if (vm == _draggingChip) continue;
            if (!desired.ContainsKey(vm.ShortName)) _widgets.RemoveAt(i);
        }

        // Pass 2: update existing VMs in place; add new ones for previously-unseen
        // short names.  Keying off ShortName means the visual container survives any
        // change to the watchlist (re-ordering, toggling inactive visibility, etc.).
        var existingByName = _widgets.ToDictionary(w => w.ShortName, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in desired)
        {
            var snap = kv.Value;
            var layout = cfg.GetLayout(snap.ShortName);
            var icon = LoadIcon(snap.ShortName);
            if (existingByName.TryGetValue(snap.ShortName, out var vm))
            {
                // Update the active/transient fields.  Don't clobber X/Y/Size if this
                // VM is the one being dragged -- the drag handlers are the
                // authoritative writers during a gesture.  After release the
                // SaveLayout call updates TrackedBuffsConfig.Layouts, and the next
                // tick will read those back here, so no info is lost.
                if (vm != _draggingChip)
                {
                    vm.X = layout.X;
                    vm.Y = layout.Y;
                    vm.Size = layout.Size;
                    vm.PlaceholderFontSize = Math.Max(8, layout.Size * 0.18);
                    vm.RemainingFontSize   = Math.Max(9, layout.Size * 0.22);
                    vm.StackFontSize       = Math.Max(9, layout.Size * 0.20);
                }
                vm.IconImageSource      = icon;
                vm.IconVisibility       = icon != null ? Visibility.Visible : Visibility.Collapsed;
                vm.PlaceholderVisibility= icon != null ? Visibility.Collapsed : Visibility.Visible;
                vm.RemainingText        = snap.IsActive && snap.RemainingSec > 0 ? FormatRemaining(snap.RemainingSec) : "";
                vm.RemainingVisibility  = snap.IsActive && snap.RemainingSec > 0 ? Visibility.Visible : Visibility.Collapsed;
                vm.StackText            = snap.StackCount > 1 ? $"x{snap.StackCount}" : "";
                vm.StackVisibility      = snap.StackCount > 1 ? Visibility.Visible : Visibility.Collapsed;
                vm.EditBorderBrush      = _editMode ? EditBorderActiveBrush  : EditBorderInactiveBrush;
                vm.EditBorderThickness  = _editMode ? new Thickness(1)       : new Thickness(0);
                vm.ResizeHandleVisibility = _editMode ? Visibility.Visible    : Visibility.Collapsed;
                vm.ChipCursor           = _editMode ? Cursors.SizeAll         : Cursors.Arrow;
                vm.Opacity              = snap.IsActive ? 1.0 : 0.4;
            }
            else
            {
                _widgets.Add(new WidgetVm
                {
                    ShortName            = snap.ShortName,
                    X                    = layout.X,
                    Y                    = layout.Y,
                    Size                 = layout.Size,
                    IconImageSource      = icon,
                    IconVisibility       = icon != null ? Visibility.Visible : Visibility.Collapsed,
                    PlaceholderVisibility= icon != null ? Visibility.Collapsed : Visibility.Visible,
                    PlaceholderFontSize  = Math.Max(8, layout.Size * 0.18),
                    RemainingText        = snap.IsActive && snap.RemainingSec > 0 ? FormatRemaining(snap.RemainingSec) : "",
                    RemainingVisibility  = snap.IsActive && snap.RemainingSec > 0 ? Visibility.Visible : Visibility.Collapsed,
                    RemainingFontSize    = Math.Max(9, layout.Size * 0.22),
                    StackText            = snap.StackCount > 1 ? $"x{snap.StackCount}" : "",
                    StackVisibility      = snap.StackCount > 1 ? Visibility.Visible : Visibility.Collapsed,
                    StackFontSize        = Math.Max(9, layout.Size * 0.20),
                    EditBorderBrush      = _editMode ? EditBorderActiveBrush  : EditBorderInactiveBrush,
                    EditBorderThickness  = _editMode ? new Thickness(1)       : new Thickness(0),
                    ResizeHandleVisibility = _editMode ? Visibility.Visible    : Visibility.Collapsed,
                    ChipCursor           = _editMode ? Cursors.SizeAll         : Cursors.Arrow,
                    Opacity              = snap.IsActive ? 1.0 : 0.4,
                });
            }
        }
    }

    // ── Drag handlers ──────────────────────────────────────────────────────────────

    private void Chip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_editMode) return;
        if (sender is not Border chip) return;
        if (chip.Tag is not string shortName) return;
        var vm = FindWidget(shortName);
        if (vm == null) return;
        _draggingChip = vm;
        _draggingResize = false;
        _dragStartPanelPt = e.GetPosition(this);
        _dragStartChipX = vm.X;
        _dragStartChipY = vm.Y;
        chip.CaptureMouse();
        e.Handled = true;
    }

    private void Chip_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_editMode || _draggingChip == null || _draggingResize) return;
        if (sender is not Border) return;
        var pt = e.GetPosition(this);
        double dx = pt.X - _dragStartPanelPt.X;
        double dy = pt.Y - _dragStartPanelPt.Y;
        // Clamp to non-negative so chips can't slide off the left/top of the
        // overlay and become unreachable.  In WeakAuras mode the window is full
        // screen so the right/bottom is implicitly bounded by the desktop.  INPC
        // handles the visual update -- no ItemsSource reset, mouse capture
        // survives even on the fastest flick.
        _draggingChip.X = Math.Max(0, _dragStartChipX + dx);
        _draggingChip.Y = Math.Max(0, _dragStartChipY + dy);
    }

    private void Chip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingChip == null) return;
        if (sender is Border chip) chip.ReleaseMouseCapture();
        SaveLayout(_draggingChip);
        _draggingChip = null;
        _draggingResize = false;
    }

    // ── Resize handlers ────────────────────────────────────────────────────────────

    private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_editMode) return;
        if (sender is not Rectangle handle) return;
        if (handle.Tag is not string shortName) return;
        var vm = FindWidget(shortName);
        if (vm == null) return;
        _draggingChip = vm;
        _draggingResize = true;
        _dragStartPanelPt = e.GetPosition(this);
        _dragStartChipSize = vm.Size;
        handle.CaptureMouse();
        e.Handled = true;
    }

    private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_editMode || _draggingChip == null || !_draggingResize) return;
        if (sender is not Rectangle) return;
        var pt = e.GetPosition(this);
        // Distance dragged is added to the start size.  Negative drag (toward the
        // origin) shrinks; positive drag (away) grows.  Average X/Y delta keeps
        // diagonal drags feeling natural.  Min 16, max 512 to bound runaway sizing.
        double dx = pt.X - _dragStartPanelPt.X;
        double dy = pt.Y - _dragStartPanelPt.Y;
        double delta = (dx + dy) / 2.0;
        double newSize = Math.Max(16, Math.Min(512, _dragStartChipSize + delta));
        _draggingChip.Size = newSize;
        // Re-compute font sizes off the new size so overlays stay readable as the
        // icon grows / shrinks.  INPC propagates each setter to the bindings.
        _draggingChip.RemainingFontSize   = Math.Max(9, newSize * 0.22);
        _draggingChip.StackFontSize       = Math.Max(9, newSize * 0.20);
        _draggingChip.PlaceholderFontSize = Math.Max(8, newSize * 0.18);
    }

    private void ResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingChip == null) return;
        if (sender is Rectangle handle) handle.ReleaseMouseCapture();
        SaveLayout(_draggingChip);
        _draggingChip = null;
        _draggingResize = false;
    }

    /// <summary>Persist the dragged / resized chip's geometry to
    /// <see cref="TrackedBuffsConfig"/> via the standard save+publish cycle.  Skips
    /// when ShortName is empty (defensive).</summary>
    private static void SaveLayout(WidgetVm vm)
    {
        if (string.IsNullOrEmpty(vm.ShortName)) return;
        var src = TrackedBuffsConfig.Current;
        var clone = new TrackedBuffsConfig
        {
            OnlyShowTracked      = src.OnlyShowTracked,
            ShowStealthStatePill = src.ShowStealthStatePill,
            FreeLayoutMode       = src.FreeLayoutMode,
            OverlayLocked        = src.OverlayLocked,
            Tracked   = new HashSet<string>(src.Tracked,   StringComparer.OrdinalIgnoreCase),
            IconPaths = new Dictionary<string, string>(src.IconPaths, StringComparer.OrdinalIgnoreCase),
            Aliases   = new Dictionary<string, string>(src.Aliases,   StringComparer.OrdinalIgnoreCase),
            Layouts   = new Dictionary<string, BuffLayout>(src.Layouts, StringComparer.OrdinalIgnoreCase),
        };
        clone.Layouts[vm.ShortName] = new BuffLayout { X = vm.X, Y = vm.Y, Size = vm.Size };
        TrackedBuffsConfig.Save(clone);
        TrackedBuffsConfig.ReplaceCurrent(clone);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────

    private WidgetVm? FindWidget(string shortName)
    {
        foreach (var vm in _widgets)
            if (string.Equals(vm.ShortName, shortName, StringComparison.OrdinalIgnoreCase))
                return vm;
        return null;
    }

    private static BitmapImage? LoadIcon(string shortName)
    {
        var path = TrackedBuffsConfig.Current.GetIconPath(shortName);
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            bool isPackUri = path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase);
            if (!isPackUri && !File.Exists(path)) return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption       = BitmapCacheOption.OnLoad;
            bmp.CreateOptions     = BitmapCreateOptions.IgnoreColorProfile;
            bmp.UriSource         = new Uri(path, UriKind.Absolute);
            bmp.DecodePixelWidth  = 256;  // generous so large user sizes don't pixelate
            bmp.EndInit();
            if (bmp.CanFreeze) bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    private static string FormatRemaining(double sec)
    {
        if (sec < 10.0) return $"{sec:0.0}";
        if (sec < 60.0) return $"{(int)sec}";
        return $"{(int)(sec / 60)}:{(int)(sec % 60):00}";
    }

    // Brushes pre-frozen for cheap reuse on every rebuild.
    private static readonly Brush EditBorderActiveBrush =
        Freeze(new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xCC, 0x66)));
    private static readonly Brush EditBorderInactiveBrush = Brushes.Transparent;
    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    /// <summary>Per-tick snapshot of a chip's active state.  Plain record -- doesn't
    /// own visual fields (those are computed off this + persisted layout at the
    /// time we sync into <see cref="WidgetVm"/>).</summary>
    private readonly record struct ChipSnapshot(string ShortName, bool IsActive, double RemainingSec, int StackCount);

    /// <summary>Mutable per-chip widget bound by the data template.  Implements
    /// <see cref="INotifyPropertyChanged"/> so the WPF bindings react to in-place
    /// mutations without an ItemsSource reset -- that's the key to smooth drag /
    /// resize, because the captured Border element is never destroyed mid-gesture.</summary>
    public sealed class WidgetVm : INotifyPropertyChanged
    {
        // Immutable identity.  Used as the key for incremental sync; once a chip
        // is in the collection its ShortName never changes.
        public required string ShortName { get; init; }

        private double _x;
        public double X { get => _x; set => Set(ref _x, value); }

        private double _y;
        public double Y { get => _y; set => Set(ref _y, value); }

        private double _size;
        public double Size { get => _size; set { if (Set(ref _size, value)) OnPropertyChanged(nameof(SizeChanged)); } }

        // Dummy property reserved for future binding sugar; harmless.
        public double SizeChanged => _size;

        private ImageSource? _iconImageSource;
        public ImageSource? IconImageSource { get => _iconImageSource; set => Set(ref _iconImageSource, value); }

        private Visibility _iconVisibility;
        public Visibility IconVisibility { get => _iconVisibility; set => Set(ref _iconVisibility, value); }

        private Visibility _placeholderVisibility;
        public Visibility PlaceholderVisibility { get => _placeholderVisibility; set => Set(ref _placeholderVisibility, value); }

        private double _placeholderFontSize;
        public double PlaceholderFontSize { get => _placeholderFontSize; set => Set(ref _placeholderFontSize, value); }

        private string _remainingText = "";
        public string RemainingText { get => _remainingText; set => Set(ref _remainingText, value); }

        private Visibility _remainingVisibility;
        public Visibility RemainingVisibility { get => _remainingVisibility; set => Set(ref _remainingVisibility, value); }

        private double _remainingFontSize;
        public double RemainingFontSize { get => _remainingFontSize; set => Set(ref _remainingFontSize, value); }

        private string _stackText = "";
        public string StackText { get => _stackText; set => Set(ref _stackText, value); }

        private Visibility _stackVisibility;
        public Visibility StackVisibility { get => _stackVisibility; set => Set(ref _stackVisibility, value); }

        private double _stackFontSize;
        public double StackFontSize { get => _stackFontSize; set => Set(ref _stackFontSize, value); }

        private Brush _editBorderBrush = Brushes.Transparent;
        public Brush EditBorderBrush { get => _editBorderBrush; set => Set(ref _editBorderBrush, value); }

        private Thickness _editBorderThickness;
        public Thickness EditBorderThickness { get => _editBorderThickness; set => Set(ref _editBorderThickness, value); }

        private Visibility _resizeHandleVisibility;
        public Visibility ResizeHandleVisibility { get => _resizeHandleVisibility; set => Set(ref _resizeHandleVisibility, value); }

        private Cursor _chipCursor = Cursors.Arrow;
        public Cursor ChipCursor { get => _chipCursor; set => Set(ref _chipCursor, value); }

        private double _opacity = 1.0;
        public double Opacity { get => _opacity; set => Set(ref _opacity, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }

    private sealed class GroupAcc
    {
        public string ShortName = "";
        public int Count;
        public DateTime LatestExpiry = DateTime.MinValue;
    }
}
