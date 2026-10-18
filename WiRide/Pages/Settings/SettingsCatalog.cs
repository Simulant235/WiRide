using WiRide.Services;

namespace WiRide.Pages;

// Alle Einstellungen, nach Kategorien. Innerhalb einer Kategorie wird alphabetisch sortiert.
public static class SettingsCatalog
{
    public const string SourceUrl = "https://github.com/Simulant235/WiRide";

    private static IAudioService Audio => ServiceHelper.Get<IAudioService>();
    private static IWiFiService Wifi => ServiceHelper.Get<IWiFiService>();

    public static IReadOnlyList<SettingsCategory> Categories { get; } = new List<SettingsCategory>
    {
        new("CatProfile", "CatProfileDesc", Icons.Person, Profile),
        new("CatAudio", "CatAudioDesc", Icons.Headset, Audio_),
        new("CatVoice", "CatVoiceDesc", Icons.Mic, Voice),
        new("CatAppearance", "CatAppearanceDesc", Icons.Tune, Appearance),
        new("CatHotspot", "CatHotspotDesc", Icons.WifiTethering, Hotspot),
        new("CatConnection", "CatConnectionDesc", Icons.Wifi, Connection),
        new("CatSystem", "CatSystemDesc", Icons.Settings, SystemItems),
        new("CatAbout", "CatAboutDesc", Icons.Info, About),
    };

    private static IEnumerable<SettingItem> Profile()
    {
        yield return new TextSetting
        {
            TitleKey = "SetName",
            DescriptionKey = "SetNameDesc",
            Icon = Icons.Person,
            PlaceholderKey = "NamePlaceholder",
            MaxLength = 24,
            Get = () => AppSettings.RiderName,
            Set = value =>
            {
                if (!string.IsNullOrWhiteSpace(value))
                    AppSettings.RiderName = value;
                return null;
            }
        };
    }

    private static IEnumerable<SettingItem> Audio_()
    {
        yield return new SliderSetting
        {
            TitleKey = "SetVolume",
            Icon = Icons.Volume,
            Get = () => AppSettings.Volume,
            Set = v =>
            {
                AppSettings.Volume = (float)v;
                Audio.Volume = (float)v;
            }
        };
        yield return new ToggleSetting
        {
            TitleKey = "SetOpus",
            DescriptionKey = "SetOpusDesc",
            Icon = Icons.Tune,
            Get = () => AppSettings.UseOpus,
            Set = v => AppSettings.UseOpus = v
        };
        yield return new ChoiceSetting
        {
            TitleKey = "SetLatency",
            DescriptionKey = "SetLatencyDesc",
            Icon = Icons.Autorenew,
            Options = () => new[] { Loc.T("LatencyLow"), Loc.T("LatencyCompat") },
            Get = () => AppSettings.LowLatency ? 0 : 1,
            Set = i => AppSettings.LowLatency = i == 0
        };
        yield return new ChoiceSetting
        {
            TitleKey = "SetQuality",
            DescriptionKey = "SetQualityDesc",
            Icon = Icons.Headset,
            Options = () => new[] { Loc.T("Q8"), Loc.T("Q16"), Loc.T("Q24"), Loc.T("Q48") },
            Get = () => Array.IndexOf(AppSettings.SupportedSampleRates, AppSettings.SampleRate),
            Set = i => AppSettings.SampleRate = AppSettings.SupportedSampleRates[i]
        };
    }

    private static IEnumerable<SettingItem> Voice()
    {
        yield return new ToggleSliderSetting
        {
            TitleKey = "SetNormalize",
            DescriptionKey = "SetNormalizeDesc",
            Icon = Icons.Volume,
            GetEnabled = () => AppSettings.NormalizeEnabled,
            SetEnabled = v => AppSettings.NormalizeEnabled = v,
            GetValue = () => AppSettings.NormalizeStrength,
            SetValue = v => AppSettings.NormalizeStrength = v
        };
        yield return new ToggleSliderSetting
        {
            TitleKey = "SetNoiseReduction",
            DescriptionKey = "SetNoiseReductionDesc",
            Icon = Icons.Tune,
            GetEnabled = () => AppSettings.NoiseReductionEnabled,
            SetEnabled = v => AppSettings.NoiseReductionEnabled = v,
            GetValue = () => AppSettings.NoiseReductionStrength,
            SetValue = v => AppSettings.NoiseReductionStrength = v
        };
        yield return new ToggleSliderSetting
        {
            TitleKey = "SetVoiceActivation",
            DescriptionKey = "SetVoiceActivationDesc",
            Icon = Icons.Mic,
            Experimental = true,
            SliderLabelKey = "Sensitivity",
            GetEnabled = () => AppSettings.VoiceActivationEnabled,
            SetEnabled = v => AppSettings.VoiceActivationEnabled = v,
            GetValue = () => AppSettings.VoiceActivationSensitivity,
            SetValue = v => AppSettings.VoiceActivationSensitivity = v
        };
    }

    private static IEnumerable<SettingItem> Appearance()
    {
        yield return new ChoiceSetting
        {
            TitleKey = "SetTheme",
            Icon = Icons.Tune,
            ReloadsUi = true,
            Options = () => new[] { Loc.T("ThemeSystem"), Loc.T("ThemeDark"), Loc.T("ThemeLight") },
            Get = () => (int)AppSettings.Theme,
            Set = i => AppSettings.Theme = (ThemeChoice)i
        };
        yield return new ChoiceSetting
        {
            TitleKey = "SetFontSize",
            Icon = Icons.Edit,
            ReloadsUi = true,
            Options = () => new[] { Loc.T("FontSmall"), Loc.T("FontNormal"), Loc.T("FontLarge"), Loc.T("FontXLarge") },
            Get = () => AppSettings.FontScaleIndex,
            Set = i => AppSettings.FontScaleIndex = i
        };

        var languages = Loc.AvailableLanguages.OrderBy(l => l.NativeName).ToList();
        yield return new ChoiceSetting
        {
            TitleKey = "SetLanguage",
            Icon = Icons.Info,
            ReloadsUi = true,
            Options = () => new[] { Loc.T("LangSystem") }.Concat(languages.Select(l => l.NativeName)).ToList(),
            Get = () => AppSettings.Language == AppSettings.SystemLanguage
                ? 0
                : languages.FindIndex(l => l.Code == AppSettings.Language) + 1,
            Set = i => AppSettings.Language = i == 0 ? AppSettings.SystemLanguage : languages[i - 1].Code
        };
    }

    private static IEnumerable<SettingItem> Hotspot()
    {
        yield return new ChoiceSetting
        {
            TitleKey = "SetCharset",
            DescriptionKey = "SetCharsetDesc",
            Icon = Icons.Lock,
            Options = () => new[] { Loc.T("CharsetMixed"), Loc.T("CharsetDigits"), Loc.T("CharsetLetters") },
            Get = () => (int)AppSettings.PasswordCharset,
            Set = i => AppSettings.PasswordCharset = (PasswordCharset)i
        };
        yield return new ActionSetting
        {
            TitleKey = "SetOpenHotspot",
            Icon = Icons.Router,
            Run = _ => { Wifi.OpenHotspotSettings(); return Task.CompletedTask; }
        };
    }

    private static IEnumerable<SettingItem> Connection()
    {
        yield return new ToggleSetting
        {
            TitleKey = "SetAutoReconnect",
            DescriptionKey = "SetAutoReconnectDesc",
            Icon = Icons.Autorenew,
            Get = () => AppSettings.AutoReconnect,
            Set = v => AppSettings.AutoReconnect = v
        };
        yield return new TextSetting
        {
            TitleKey = "SetPort",
            DescriptionKey = "SetPortDesc",
            Icon = Icons.Router,
            Keyboard = Keyboard.Numeric,
            MaxLength = 5,
            Get = () => AppSettings.Port.ToString(),
            Set = value =>
            {
                if (!int.TryParse(value, out var port) || port < 1024 || port > 65535)
                    return Loc.T("PortInvalid");
                AppSettings.Port = port;
                return null;
            }
        };
    }

    private static IEnumerable<SettingItem> SystemItems()
    {
        yield return new ActionSetting
        {
            TitleKey = "SetAppPermissions",
            Icon = Icons.Lock,
            Run = _ => { AppInfo.Current.ShowSettingsUI(); return Task.CompletedTask; }
        };
        yield return new ActionSetting
        {
            TitleKey = "SetLocationSettings",
            Icon = Icons.Location,
            Run = _ => OpenAndroidSettings("android.settings.LOCATION_SOURCE_SETTINGS")
        };
        yield return new ActionSetting
        {
            TitleKey = "SetWifiSettings",
            Icon = Icons.Wifi,
            Run = _ => OpenAndroidSettings("android.settings.WIFI_SETTINGS")
        };
        yield return new ActionSetting
        {
            TitleKey = "SetReset",
            Icon = Icons.Reset,
            Danger = true,
            Run = async page =>
            {
                if (!await page.DisplayAlertAsync(Loc.T("ResetTitle"), Loc.T("ResetText"), Loc.T("Reset"), Loc.T("Cancel")))
                    return;

                AppSettings.Reset();
                Audio.Volume = AppSettings.Volume;
                await ThemeManager.ReloadUiAsync(reopenSettings: true);
            }
        };
    }

    private static IEnumerable<SettingItem> About()
    {
        yield return new InfoSetting
        {
            TitleKey = "SetVersion",
            Icon = Icons.Info,
            Value = () => AppInfo.Current.VersionString
        };
        yield return new ActionSetting
        {
            TitleKey = "SetSource",
            Icon = Icons.ChevronRight,
            Run = _ => Launcher.Default.OpenAsync(SourceUrl)
        };
    }

    public static Task OpenAndroidSettings(string action)
    {
#if ANDROID
        var intent = new Android.Content.Intent(action);
        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
        Android.App.Application.Context.StartActivity(intent);
#endif
        return Task.CompletedTask;
    }
}
