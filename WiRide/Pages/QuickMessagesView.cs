using WiRide.Models;
using WiRide.Services;

namespace WiRide.Pages;

// Schnell-Ansagen (Pause, Tanken, ...) und "Musik teilen" waehrend einer Sitzung
public class QuickMessagesView : VerticalStackLayout
{
    private static readonly GroupEventCode[] Codes =
    {
        GroupEventCode.Pause, GroupEventCode.Fuel, GroupEventCode.Police,
        GroupEventCode.Danger, GroupEventCode.SlowDown, GroupEventCode.WaitForMe,
        GroupEventCode.TurnLeft, GroupEventCode.TurnRight, GroupEventCode.AllGood
    };

    private readonly IntercomService _intercom = ServiceHelper.Get<IntercomService>();
    private readonly Button _musicButton;

    public QuickMessagesView()
    {
        Spacing = 10;

        if (AppSettings.QuickMessagesEnabled)
        {
            Add(new Label { Text = Loc.T("QuickMessages"), Style = SettingsUi.Style("WrSectionTitle") });

            var grid = new Grid
            {
                ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) },
                ColumnSpacing = 8,
                RowSpacing = 8
            };

            for (int i = 0; i < Codes.Length; i++)
            {
                var code = Codes[i];
                var button = new Button
                {
                    Text = AnnouncementService.QuickMessageText(code),
                    Style = SettingsUi.Style("WrSecondaryButton"),
                    FontSize = 14,
                    HeightRequest = 48,
                    Padding = new Thickness(4, 0)
                };
                button.Clicked += (_, _) => _intercom.SendQuickMessage(code);

                if (i % 3 == 0)
                    grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                grid.Add(button, i % 3, i / 3);
            }

            Add(grid);
        }

        _musicButton = new Button { Style = SettingsUi.Style("WrSecondaryButton"), Margin = new Thickness(0, 6, 0, 0) };
        _musicButton.Clicked += OnMusicClicked;
        Add(_musicButton);

        Refresh();
    }

    public void Refresh()
    {
        _musicButton.IsVisible = _intercom.CanShareMusic;
        _musicButton.Text = Loc.T(_intercom.IsMusicSharing ? "MusicStop" : "MusicStart");
    }

    private async void OnMusicClicked(object? sender, EventArgs e)
    {
        if (_intercom.IsMusicSharing)
        {
            _intercom.StopMusicSharing();
        }
        else
        {
            var page = this.GetParentPage();
            if (page != null && !await page.DisplayAlertAsync(Loc.T("MusicShare"), Loc.T("MusicShareHint"), Loc.T("Ok"), Loc.T("Cancel")))
                return;

            if (!await _intercom.StartMusicSharingAsync() && page != null)
                await page.DisplayAlertAsync(Loc.T("MusicShare"), Loc.T("MusicShareFailed"), Loc.T("Ok"));
        }

        Refresh();
    }
}

public static class ElementExtensions
{
    public static Page? GetParentPage(this Element element)
    {
        var current = element.Parent;
        while (current != null && current is not Page)
            current = current.Parent;
        return current as Page;
    }
}
