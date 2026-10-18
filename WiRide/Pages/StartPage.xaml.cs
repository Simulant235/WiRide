using WiRide.Services;

namespace WiRide.Pages;

public partial class StartPage : ContentPage
{
    public StartPage()
    {
        InitializeComponent();

#if DEBUG
        AudioTestButton.IsVisible = true;
#endif
    }

    private IDispatcherTimer? _liveTimer;
    private bool _hostLive;
    private int _checks;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        GreetingLabel.Text = Loc.T("StartGreeting", AppSettings.RiderName);
        SetupQuickStart();

        if (!AppSettings.OnboardingDone)
            Dispatcher.Dispatch(async () => await Navigation.PushModalAsync(new OnboardingPage()));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _liveTimer?.Stop();
    }

    // Schnellstart-Karte fuer die letzte Sitzung (Host: erneut hosten, Client: wieder verbinden)
    private void SetupQuickStart()
    {
        var role = AppSettings.LastRole;
        bool show = AppSettings.QuickStartEnabled && role.Length > 0
                    && ServiceHelper.Get<IntercomService>().Role == IntercomRole.None;
        QuickStartCard.IsVisible = show;
        _liveTimer?.Stop();
        if (!show)
            return;

        if (role == AppSettings.RoleHost)
        {
            QuickStartIcon.Text = Icons.WifiTethering;
            QuickStartTitle.Text = Loc.T("QuickHostAgain");
            QuickStartDetail.Text = Loc.T("QuickHostAgainDesc");
            QuickStartDot.IsVisible = false;
            QuickStartCard.Opacity = 1;
            return;
        }

        QuickStartIcon.Text = Icons.Autorenew;
        QuickStartTitle.Text = Loc.T("QuickReconnect", AppSettings.LastHostName);
        QuickStartDot.IsVisible = true;
        SetHostLive(false, checking: true);

        _checks = 0;
        _liveTimer ??= Dispatcher.CreateTimer();
        _liveTimer.Interval = TimeSpan.FromSeconds(4);
        _liveTimer.Tick -= OnLiveTick;
        _liveTimer.Tick += OnLiveTick;
        _liveTimer.Start();
        OnLiveTick(null, EventArgs.Empty);
    }

    private async void OnLiveTick(object? sender, EventArgs e)
    {
        bool live = await IsLastHostLiveAsync(triggerScan: _checks++ % 8 == 0);
        SetHostLive(live, checking: false);
    }

    // Host gilt als online, wenn er im WLAN antwortet oder sein WLAN in Reichweite ist
    private static async Task<bool> IsLastHostLiveAsync(bool triggerScan)
    {
        var wifi = ServiceHelper.Get<IWiFiService>();
        var targets = new List<string> { AppSettings.LastHostIp };
        if (wifi.GatewayIp is { } gateway)
            targets.Add(gateway);

        var hosts = await ServiceHelper.Get<IntercomService>().Network.DiscoverHostsAsync(AppSettings.Port, targets, 800);
        if (hosts.Count > 0)
            return true;

        var ssid = AppSettings.LastWifiSsid;
        return ssid.Length > 0 && wifi.GetVisibleNetworks(triggerScan).Contains(ssid);
    }

    private void SetHostLive(bool live, bool checking)
    {
        _hostLive = live;
        QuickStartCard.Opacity = live ? 1 : 0.5;
        QuickStartDot.Fill = ThemeManager.Get(live ? "WrSuccess" : "WrTextMuted");
        QuickStartDetail.Text = Loc.T(checking ? "QuickChecking" : live ? "QuickHostOnline" : "QuickHostOffline");
    }

    private async void OnQuickStartTapped(object? sender, TappedEventArgs e)
    {
        if (AppSettings.LastRole == AppSettings.RoleHost)
        {
            await Navigation.PushAsync(new HostPage(autoStart: true));
            return;
        }

        if (!_hostLive)
        {
            await DisplayAlertAsync(Loc.T("QuickReconnect", AppSettings.LastHostName), Loc.T("QuickHostOfflineText"), Loc.T("Ok"));
            return;
        }

        await Navigation.PushAsync(new ClientPage(quickReconnect: true));
    }

    private async void OnHostButtonClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new HostPage());
    }

    private async void OnClientButtonClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new ClientPage());
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new SettingsPage());
    }

    private async void OnAudioTestButtonClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new AudioTestPage());
    }
}
