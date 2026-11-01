module CustomMessageBox

open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading

(*
sample use:

let cmb = new CustomMessageBox.CustomMessageBox("Verify changes", System.Drawing.SystemIcons.Question, "You moved a dungeon segment. Keep this change?", ["Keep changes"; "Undo"])
cmb.OwnerWindow <- TopLevel.GetTopLevel(this) :?> Window
cmb.ShowDialogBlocking()
if cmb.MessageBoxResult = null || cmb.MessageBoxResult = "Undo" then
    // undo
else
    // keep

(WPF: `cmb.Owner <- Window.GetWindow(this); cmb.ShowDialog() |> ignore`. Avalonia's ShowDialog is asynchronous and
takes the owner as an argument, so ShowDialogBlocking runs a nested dispatcher loop until the box closes, which keeps
the WPF call sites synchronous.)
*)

type CustomMessageBox(title, icon:System.Drawing.Icon, mainText, buttonTexts:seq<string>) as this =
    inherit Window()

    let mutable result = null
    let mutable ownerWindow : Window = null

    do
        this.WindowStartupLocation <- WindowStartupLocation.CenterOwner
        this.SystemDecorations <- SystemDecorations.Full   // (WPF: WindowStyle.SingleBorderWindow)
        this.Topmost <- true
        this.Title <- title
        this.SizeToContent <- SizeToContent.WidthAndHeight

        let grid = new Grid()
        grid.RowDefinitions.Add(new RowDefinition(Height=GridLength(1.0, GridUnitType.Star)))
        grid.RowDefinitions.Add(new RowDefinition(Height=GridLength.Auto))

        let mainDock = new DockPanel()
        let image = Graphics.BMPtoImage(icon.ToBitmap())
        image.HorizontalAlignment <- HorizontalAlignment.Left
        image.Margin <- Thickness(30.,0.,0.,0.)
        mainDock.Children.Add(image) |> ignore
        DockPanel.SetDock(image, Dock.Left)
        // (WPF: a read-only, borderless, transparent TextBox, i.e. selectable text. Avalonia's TextBox measures wrapped text a pixel or two
        // too narrow, which pushes the last word onto a clipped second line, so this uses Avalonia's SelectableTextBlock, made for exactly this.)
        let mainTextBlock = new SelectableTextBlock(Text=mainText, Background=Brushes.Transparent, 
                                            TextWrapping=TextWrapping.Wrap, MaxWidth=500., Width=System.Double.NaN, 
                                            VerticalAlignment=VerticalAlignment.Center, Margin=Thickness(12.,20.,41.,15.))
        mainDock.Children.Add(mainTextBlock) |> ignore
        grid.Children.Add(mainDock) |> ignore
        Grid.SetRow(mainDock, 0)

        let buttonDock = new DockPanel(Margin=Thickness(5.,0.,0.,0.))
        let mutable first = true
        for bt in buttonTexts |> Seq.rev do
            let b = new Button(MinWidth=88., MaxWidth=160., Height=26., Margin=Thickness(5.), HorizontalAlignment=HorizontalAlignment.Right)
            if first then
                b.Focus() |> ignore
                first <- false
            DockPanel.SetDock(b, Dock.Right)
            buttonDock.Children.Add(b) |> ignore
            b.Content <- new Label(Content=bt, Padding=Thickness(0.), Margin=Thickness(10.,0.,10.,0.))
            b.Click.Add(fun _ -> result <- bt; this.Close())
        grid.Children.Add(buttonDock) |> ignore
        Grid.SetRow(buttonDock, 1)

        this.Content <- grid

    member this.MessageBoxResult = result
    /// the window to center over and block (WPF: Owner)
    member this.OwnerWindow with get() = ownerWindow and set(w) = ownerWindow <- w
    /// shows the box modally and returns once it's closed (WPF: ShowDialog)
    member this.ShowDialogBlocking() =
        let frame = new DispatcherFrame()
        this.Closed.Add(fun _ -> frame.Continue <- false)
        if ownerWindow <> null then
            this.ShowDialog(ownerWindow) |> ignore
        else
            this.Show()
        Dispatcher.UIThread.PushFrame(frame)
