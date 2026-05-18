using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
/// WeakAuras-style free-layout panel for the cooldown overlay.  Each tracked power
/// renders as a bare icon at its persisted (X, Y), with:
/// <list type="bullet">
///   <item>Full opacity when the power is ready</item>
///   <item>Dimmed icon + dark fill-from-bottom rectangle + countdown text while on
///         cooldown</item>
///   <item>Edit-mode chrome (yellow border + corner resize grip) when the overlay
///         is unlocked</item>
/// </list>
///
/// <para>Same INPC + ObservableCollection incremental-sync model as
/// <see cref="FreeLayoutBuffPanel"/>: existing chips keep their visual containers
/// and any active mouse capture across updates, so fast drags don't lose tracking.</para>
/// </summary>
public partial class FreeLayoutCooldownPanel : UserControl
{
    private bool _editMode;
    private CooldownTracker? _tracker;
    private readonly ObservableCollection<WidgetVm> _widgets = new();

    private WidgetVm? _draggingChip;
    private Point _dragStartPanelPt;
    private double _dragStartChipX, _dragStartChipY;
    private bool _draggingResize;
    private double _dragStartChipSize;

    public FreeLayoutCooldownPanel()
    {
        InitializeComponent();
        WidgetHost.ItemsSource = _widgets;
    }

    /// <summary>Hand the live tracker to the panel.  Reading per-power state on
    /// every tick uses <see cref="CooldownTracker.TryGetState"/> which is lock-
    /// protected, so this is safe to call before / after the overlay is shown.</summary>
    public void SetTracker(CooldownTracker? tracker) => _tracker = tracker;

    public void SetEditMode(bool edit)
    {
        if (_editMode == edit) return;
        _editMode = edit;
        foreach (var vm in _widgets)
        {
            vm.EditBorderBrush      = _editMode ? EditBorderActiveBrush  : EditBorderInactiveBrush;
            vm.EditBorderThickness  = _editMode ? new Thickness(1)       : new Thickness(0);
            vm.ResizeHandleVisibility = _editMode ? Visibility.Visible    : Visibility.Collapsed;
            vm.ChipCursor           = _editMode ? Cursors.SizeAll         : Cursors.Arrow;
        }
    }

    /// <summary>Recompute per-chip cooldown state for the supplied wall-clock and
    /// sync the panel's widget collection to the current watchlist.  Drives the
    /// fade / progress overlay / countdown text on every tick.</summary>
    public void UpdateCooldowns(DateTime nowUtc)
    {
        var cfg = CooldownTrackerConfig.Current;

        // Build the desired set: one chip per watchlist entry.  Server-authoritative
        // cooldown + charge state lives on _tracker.  "Ready" semantics:
        //   * Charged ability (ChargesMax > 0): ready iff ChargesAvailable > 0.
        //     The cooldown sweep shown is the next-charge regen timer (cooldown
        //     duration tracks that for charged abilities).
        //   * Non-charged ability: ready iff not on cooldown.
        // CDR procs are handled automatically because the server pushes updated
        // PowerCooldownDuration deltas; we overwrite our local copy on each tick.
        var desired = new Dictionary<uint, ChipSnapshot>(cfg.Tracked.Count);
        foreach (var protoId in cfg.Tracked)
        {
            var layout = cfg.GetLayout(protoId);
            var state = _tracker?.TryGetState(protoId);
            double cdSec = 0;
            double remainingSec = 0;
            bool ready = true;
            int chargesAvail = 0;
            int chargesMax = 0;
            if (state != null)
            {
                ready        = state.IsReady(nowUtc);
                chargesAvail = state.ChargesAvailable;
                chargesMax   = state.ChargesMax;
                if (state.OnCooldown && state.CooldownDurationMs > 0)
                {
                    double elapsedMs = (nowUtc - state.CooldownStartUtc).TotalMilliseconds;
                    double remMs     = Math.Max(0, state.CooldownDurationMs - elapsedMs);
                    remainingSec = remMs / 1000.0;
                    cdSec        = state.CooldownDurationMs / 1000.0;
                }
            }
            desired[protoId] = new ChipSnapshot(protoId, layout, cdSec, remainingSec, ready, chargesAvail, chargesMax);
        }

        // Pass 1: remove VMs no longer in the desired set (untracked).  Skip the
        // currently-dragging chip so a mid-drag config change can't yank the chip
        // out from under the user.
        for (int i = _widgets.Count - 1; i >= 0; i--)
        {
            var vm = _widgets[i];
            if (vm == _draggingChip) continue;
            if (!desired.ContainsKey(vm.ProtoId)) _widgets.RemoveAt(i);
        }

        // Pass 2: update existing in place; add new ones for newly-tracked powers.
        var existing = new Dictionary<uint, WidgetVm>(_widgets.Count);
        foreach (var w in _widgets) existing[w.ProtoId] = w;

        foreach (var kv in desired)
        {
            var snap = kv.Value;
            var icon = LoadIcon(snap.ProtoId);
            if (existing.TryGetValue(snap.ProtoId, out var vm))
            {
                // Don't clobber X/Y/Size mid-drag.
                if (vm != _draggingChip)
                {
                    vm.X = snap.Layout.X;
                    vm.Y = snap.Layout.Y;
                    vm.Size = snap.Layout.Size;
                    vm.RemainingFontSize    = Math.Max(11, snap.Layout.Size * 0.30);
                    vm.PlaceholderFontSize  = Math.Max(12, snap.Layout.Size * 0.40);
                }
                UpdateCooldownVisuals(vm, snap, icon);
            }
            else
            {
                var fresh = new WidgetVm { ProtoId = snap.ProtoId };
                fresh.X = snap.Layout.X;
                fresh.Y = snap.Layout.Y;
                fresh.Size = snap.Layout.Size;
                fresh.RemainingFontSize    = Math.Max(11, snap.Layout.Size * 0.30);
                fresh.PlaceholderFontSize  = Math.Max(12, snap.Layout.Size * 0.40);
                fresh.EditBorderBrush      = _editMode ? EditBorderActiveBrush : EditBorderInactiveBrush;
                fresh.EditBorderThickness  = _editMode ? new Thickness(1)      : new Thickness(0);
                fresh.ResizeHandleVisibility = _editMode ? Visibility.Visible   : Visibility.Collapsed;
                fresh.ChipCursor           = _editMode ? Cursors.SizeAll        : Cursors.Arrow;
                UpdateCooldownVisuals(fresh, snap, icon);
                _widgets.Add(fresh);
            }
        }
    }

    /// <summary>Update the visuals (icon, opacity, overlay height, countdown text)
    /// for one chip from a fresh cooldown snapshot.  Pulled out of UpdateCooldowns
    /// so the same logic serves both "update existing VM" and "newly-added VM"
    /// branches.</summary>
    private static void UpdateCooldownVisuals(WidgetVm vm, ChipSnapshot snap, BitmapImage? icon)
    {
        vm.IconImageSource       = icon;
        vm.PlaceholderVisibility = icon != null ? Visibility.Collapsed : Visibility.Visible;

        // Charge badge: only show when this is a known charged ability (max > 0).
        // Shows current count -- "x3 / x2 / x1" style.  When charges == 0 the
        // badge still renders to make the absence explicit ("0" is more useful
        // than no badge for the user trying to read state at a glance).
        if (snap.ChargesMax > 0)
        {
            vm.ChargeText       = "x" + snap.ChargesAvailable;
            vm.ChargeVisibility = Visibility.Visible;
        }
        else
        {
            vm.ChargeText       = "";
            vm.ChargeVisibility = Visibility.Collapsed;
        }

        if (snap.Ready)
        {
            // Ready: icon at full opacity, no overlay, no countdown.  For charged
            // abilities that have a cooldown ticking down on the NEXT charge, we
            // still hide the overlay -- the user cares about "can I cast right
            // now", which charges > 0 already answers.
            vm.IconOpacity            = 1.0;
            vm.CooldownOverlayHeight  = 0;
            vm.RemainingVisibility    = Visibility.Collapsed;
            vm.RemainingText          = "";
        }
        else
        {
            vm.IconOpacity            = 0.35;
            // Fill from bottom: height proportional to remaining cooldown
            // fraction.  When remaining == total (just fired) the overlay covers
            // the whole icon; when remaining ~= 0 the overlay vanishes and the
            // icon lights up on the next tick.
            double frac = snap.CooldownSec > 0 ? snap.RemainingSec / snap.CooldownSec : 0;
            vm.CooldownOverlayHeight  = Math.Max(0, Math.Min(snap.Layout.Size, snap.Layout.Size * frac));
            vm.RemainingText          = FormatRemaining(snap.RemainingSec);
            vm.RemainingVisibility    = Visibility.Visible;
        }
    }

    // ── Drag handlers ──────────────────────────────────────────────────────────────

    private void Chip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_editMode) return;
        if (sender is not Border chip) return;
        if (!TryGetProtoId(chip.Tag, out uint protoId)) return;
        var vm = FindWidget(protoId);
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
        var pt = e.GetPosition(this);
        double dx = pt.X - _dragStartPanelPt.X;
        double dy = pt.Y - _dragStartPanelPt.Y;
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
        if (!TryGetProtoId(handle.Tag, out uint protoId)) return;
        var vm = FindWidget(protoId);
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
        var pt = e.GetPosition(this);
        double dx = pt.X - _dragStartPanelPt.X;
        double dy = pt.Y - _dragStartPanelPt.Y;
        double delta = (dx + dy) / 2.0;
        double newSize = Math.Max(24, Math.Min(256, _dragStartChipSize + delta));
        _draggingChip.Size = newSize;
        _draggingChip.RemainingFontSize    = Math.Max(11, newSize * 0.30);
        _draggingChip.PlaceholderFontSize  = Math.Max(12, newSize * 0.40);
    }

    private void ResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingChip == null) return;
        if (sender is Rectangle handle) handle.ReleaseMouseCapture();
        SaveLayout(_draggingChip);
        _draggingChip = null;
        _draggingResize = false;
    }

    private static void SaveLayout(WidgetVm vm)
    {
        var src = CooldownTrackerConfig.Current;
        var clone = new CooldownTrackerConfig
        {
            FreeLayoutMode = src.FreeLayoutMode,
            OverlayLocked  = src.OverlayLocked,
            Tracked   = new List<uint>(src.Tracked),
            Cooldowns = new Dictionary<uint, double>(src.Cooldowns),
            IconPaths = new Dictionary<uint, string>(src.IconPaths),
            Aliases   = new Dictionary<uint, string>(src.Aliases),
            Layouts   = new Dictionary<uint, CooldownLayout>(src.Layouts),
        };
        clone.Layouts[vm.ProtoId] = new CooldownLayout { X = vm.X, Y = vm.Y, Size = vm.Size };
        CooldownTrackerConfig.Save(clone);
        CooldownTrackerConfig.ReplaceCurrent(clone);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────

    private WidgetVm? FindWidget(uint protoId)
    {
        foreach (var vm in _widgets)
            if (vm.ProtoId == protoId) return vm;
        return null;
    }

    private static bool TryGetProtoId(object? tag, out uint protoId)
    {
        protoId = 0;
        if (tag is uint u) { protoId = u; return true; }
        if (tag is string s && uint.TryParse(s, out var parsed)) { protoId = parsed; return true; }
        return false;
    }

    private static BitmapImage? LoadIcon(uint protoId)
    {
        var cfg = CooldownTrackerConfig.Current;
        string? path = cfg.GetIconPath(protoId) ?? PowerIconByProto.GetPackUri(protoId);
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
            bmp.DecodePixelWidth  = 256;
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

    private static readonly Brush EditBorderActiveBrush =
        Freeze(new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xCC, 0x66)));
    private static readonly Brush EditBorderInactiveBrush = Brushes.Transparent;
    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    /// <summary>Per-tick snapshot of a chip's desired state.  Plain record; the VM
    /// is the long-lived mutable INPC container that the bindings observe.</summary>
    private readonly record struct ChipSnapshot(
        uint ProtoId,
        CooldownLayout Layout,
        double CooldownSec,
        double RemainingSec,
        bool Ready,
        int ChargesAvailable,
        int ChargesMax);

    /// <summary>Mutable per-chip widget bound by the data template.  INPC so the
    /// bindings observe in-place mutations -- the captured Border survives any
    /// number of update ticks during a fast drag.</summary>
    public sealed class WidgetVm : INotifyPropertyChanged
    {
        public required uint ProtoId { get; init; }
        private double _x;
        public double X { get => _x; set => Set(ref _x, value); }
        private double _y;
        public double Y { get => _y; set => Set(ref _y, value); }
        private double _size = 64;
        public double Size { get => _size; set => Set(ref _size, value); }
        private ImageSource? _iconImageSource;
        public ImageSource? IconImageSource { get => _iconImageSource; set => Set(ref _iconImageSource, value); }
        private double _iconOpacity = 1.0;
        public double IconOpacity { get => _iconOpacity; set => Set(ref _iconOpacity, value); }
        private Visibility _placeholderVisibility = Visibility.Collapsed;
        public Visibility PlaceholderVisibility { get => _placeholderVisibility; set => Set(ref _placeholderVisibility, value); }
        private double _placeholderFontSize = 20;
        public double PlaceholderFontSize { get => _placeholderFontSize; set => Set(ref _placeholderFontSize, value); }
        private double _cooldownOverlayHeight;
        public double CooldownOverlayHeight { get => _cooldownOverlayHeight; set => Set(ref _cooldownOverlayHeight, value); }
        private string _remainingText = "";
        public string RemainingText { get => _remainingText; set => Set(ref _remainingText, value); }
        private Visibility _remainingVisibility = Visibility.Collapsed;
        public Visibility RemainingVisibility { get => _remainingVisibility; set => Set(ref _remainingVisibility, value); }
        private double _remainingFontSize = 18;
        public double RemainingFontSize { get => _remainingFontSize; set => Set(ref _remainingFontSize, value); }
        private string _chargeText = "";
        public string ChargeText { get => _chargeText; set => Set(ref _chargeText, value); }
        private Visibility _chargeVisibility = Visibility.Collapsed;
        public Visibility ChargeVisibility { get => _chargeVisibility; set => Set(ref _chargeVisibility, value); }
        private Brush _editBorderBrush = Brushes.Transparent;
        public Brush EditBorderBrush { get => _editBorderBrush; set => Set(ref _editBorderBrush, value); }
        private Thickness _editBorderThickness;
        public Thickness EditBorderThickness { get => _editBorderThickness; set => Set(ref _editBorderThickness, value); }
        private Visibility _resizeHandleVisibility = Visibility.Collapsed;
        public Visibility ResizeHandleVisibility { get => _resizeHandleVisibility; set => Set(ref _resizeHandleVisibility, value); }
        private Cursor _chipCursor = Cursors.Arrow;
        public Cursor ChipCursor { get => _chipCursor; set => Set(ref _chipCursor, value); }

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
}
