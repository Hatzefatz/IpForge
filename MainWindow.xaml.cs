using System;
using IpForge.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace IpForge;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ResizeAndCenterWindow();

        RootNavigationView.SelectedItem =
            RootNavigationView.MenuItems[0];

        ContentFrame.Navigate(typeof(AdapterPage));
    }

    private void ResizeAndCenterWindow()
    {
        DisplayArea displayArea = DisplayArea.GetFromWindowId(
            AppWindow.Id,
            DisplayAreaFallback.Primary);

        RectInt32 workArea = displayArea.WorkArea;

        int width = Math.Min(
            1400,
            workArea.Width - 80);

        int height = Math.Min(
            900,
            workArea.Height - 80);

        int x = workArea.X +
                (workArea.Width - width) / 2;

        int y = workArea.Y +
                (workArea.Height - height) / 2;

        AppWindow.MoveAndResize(
            new RectInt32(
                x,
                y,
                width,
                height));
    }

    private void RootNavigationView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag)
        {
            return;
        }

        switch (tag)
        {
            case "Adapters":
                NavigateTo(typeof(AdapterPage));
                break;

            case "Presets":
                NavigateTo(typeof(PresetsPage));
                break;
        }
    }

    private void NavigateTo(Type pageType)
    {
        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}