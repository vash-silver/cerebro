using System.Windows;

namespace MarvelHeroes.DpsMeter.Windows;

/// <summary>
/// Standalone window host for <see cref="Controls.ReportViewerPanel"/>.  The whole report-
/// viewer UI lives in the UserControl now; this window just gives the right-click "View
/// reports" menu and any other "open in a new window" caller a place to land.  The main
/// app window's Reports tab also hosts the panel directly without going through here.
/// </summary>
public partial class ReportViewerWindow : Window
{
    public ReportViewerWindow()
    {
        InitializeComponent();
        // Forward the panel's Close button click to the window so the existing right-click
        // workflow ("View reports" -> window opens -> Close button shuts it) still works.
        Panel.CloseRequested += (_, _) => Close();
    }
}
