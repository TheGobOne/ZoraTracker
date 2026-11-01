// Ported from Z1R_WPF/CustomMessageBox.fs (WPF @ 9292b0b).
// (F#: `module CustomMessageBox` holding `type CustomMessageBox`. C# can't nest a class in a class of the same name (CS0542), so, as
// PLAN.md says, it's one class `CustomMessageBox` in namespace Z1R_Avalonia: WPF's `CustomMessageBox.CustomMessageBox(...)` becomes
// `new CustomMessageBox(...)`.)
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Z1R_Avalonia;

/*
sample use:

var cmb = new CustomMessageBox("Verify changes", System.Drawing.SystemIcons.Question, "You moved a dungeon segment. Keep this change?", new[] { "Keep changes", "Undo" });
cmb.OwnerWindow = (Window)TopLevel.GetTopLevel(this);
cmb.ShowDialogBlocking();
if (cmb.MessageBoxResult == null || cmb.MessageBoxResult == "Undo")
    // undo
else
    // keep

(WPF: `cmb.Owner <- Window.GetWindow(this); cmb.ShowDialog() |> ignore`. Avalonia's ShowDialog is asynchronous and
takes the owner as an argument, so ShowDialogBlocking runs a nested dispatcher loop until the box closes, which keeps
the WPF call sites synchronous.)
*/

public class CustomMessageBox : Window
{
    private string result = null;
    private Window ownerWindow = null;

    public CustomMessageBox(string title, System.Drawing.Icon icon, string mainText, IEnumerable<string> buttonTexts)
    {
        this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.SystemDecorations = SystemDecorations.Full;   // (WPF: WindowStyle.SingleBorderWindow)
        this.Topmost = true;
        this.Title = title;
        this.SizeToContent = SizeToContent.WidthAndHeight;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var mainDock = new DockPanel();
        var image = Graphics.BMPtoImage(icon.ToBitmap());
        image.HorizontalAlignment = HorizontalAlignment.Left;
        image.Margin = new Thickness(30.0, 0.0, 0.0, 0.0);
        mainDock.Children.Add(image);
        DockPanel.SetDock(image, Dock.Left);
        // (WPF: a read-only, borderless, transparent TextBox, i.e. selectable text. Avalonia's TextBox measures wrapped text a pixel or two
        // too narrow, which pushes the last word onto a clipped second line, so this uses Avalonia's SelectableTextBlock, made for exactly this.)
        var mainTextBlock = new SelectableTextBlock { Text = mainText, Background = Brushes.Transparent,
                                            TextWrapping = TextWrapping.Wrap, MaxWidth = 500.0, Width = double.NaN,
                                            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12.0, 20.0, 41.0, 15.0) };
        mainDock.Children.Add(mainTextBlock);
        grid.Children.Add(mainDock);
        Grid.SetRow(mainDock, 0);

        var buttonDock = new DockPanel { Margin = new Thickness(5.0, 0.0, 0.0, 0.0) };
        var first = true;
        foreach (var bt in buttonTexts.Reverse())
        {
            var b = new Button { MinWidth = 88.0, MaxWidth = 160.0, Height = 26.0, Margin = new Thickness(5.0), HorizontalAlignment = HorizontalAlignment.Right };
            if (first)
            {
                b.Focus();
                first = false;
            }
            DockPanel.SetDock(b, Dock.Right);
            buttonDock.Children.Add(b);
            b.Content = new Label { Content = bt, Padding = new Thickness(0.0), Margin = new Thickness(10.0, 0.0, 10.0, 0.0) };
            b.Click += (_, _) => { result = bt; this.Close(); };
        }
        grid.Children.Add(buttonDock);
        Grid.SetRow(buttonDock, 1);

        this.Content = grid;
    }

    public string MessageBoxResult => result;
    /// <summary>the window to center over and block (WPF: Owner)</summary>
    public Window OwnerWindow { get => ownerWindow; set => ownerWindow = value; }
    /// <summary>shows the box modally and returns once it's closed (WPF: ShowDialog)</summary>
    public void ShowDialogBlocking()
    {
        var frame = new DispatcherFrame();
        this.Closed += (_, _) => frame.Continue = false;
        if (ownerWindow != null)
            _ = this.ShowDialog(ownerWindow);
        else
            this.Show();
        Dispatcher.UIThread.PushFrame(frame);
    }
}
