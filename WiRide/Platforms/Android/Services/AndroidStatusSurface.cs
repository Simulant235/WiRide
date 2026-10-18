using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using WiRide.Services;
using AndroidApp = Android.App.Application;
using AView = Android.Views.View;
using Color = Android.Graphics.Color;
using Android.Runtime;

namespace WiRide.Platforms.Android.Services;

// Home-Widget mit Status und Mute-Knopf
[BroadcastReceiver(Label = "WiRide", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/wiride_widget_info")]
public class WiRideWidgetProvider : AppWidgetProvider
{
    public const string ActionToggleMute = "com.wiride.TOGGLE_MUTE";

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds) =>
        AndroidStatusSurface.Instance?.RefreshWidget();

    public override void OnReceive(Context? context, Intent? intent)
    {
        base.OnReceive(context, intent);

        if (intent?.Action == ActionToggleMute)
            AndroidStatusSurface.ToggleMute();
    }
}

// Aktualisiert Widget und schwebendes Mini-Fenster
public class AndroidStatusSurface : IStatusSurface
{
    public static AndroidStatusSurface? Instance { get; private set; }

    private readonly Context _context = AndroidApp.Context;
    private SessionSnapshot _snapshot = SessionSnapshot.Idle;
    private bool _inBackground;

    private LinearLayout? _overlay;
    private TextView? _overlayTitle;
    private TextView? _overlayDetail;
    private TextView? _overlayRiders;
    private TextView? _overlayMute;

    public AndroidStatusSurface() => Instance = this;

    public void Update(SessionSnapshot snapshot)
    {
        _snapshot = snapshot;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshWidget();
            RefreshOverlay();
        });
    }

    public void SetAppInBackground(bool background)
    {
        _inBackground = background;
        MainThread.BeginInvokeOnMainThread(RefreshOverlay);
    }

    public static void ToggleMute()
    {
        var intercom = ServiceHelper.Get<IntercomService>();
        if (intercom.Role == IntercomRole.None)
            return;

        intercom.IsMuted = !intercom.IsMuted;
        ServiceHelper.Get<StatusPublisher>().Publish();
    }

    private PendingIntent OpenAppIntent()
    {
        var launch = _context.PackageManager!.GetLaunchIntentForPackage(_context.PackageName!)!;
        launch.AddFlags(ActivityFlags.SingleTop | ActivityFlags.NewTask);
        return PendingIntent.GetActivity(_context, 0, launch, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    }

    // ---------------- Widget ----------------

    public void RefreshWidget()
    {
        try
        {
            var manager = AppWidgetManager.GetInstance(_context);
            var component = new ComponentName(_context, Java.Lang.Class.FromType(typeof(WiRideWidgetProvider)));
            if (manager == null || manager.GetAppWidgetIds(component)?.Length == 0)
                return;

            var views = new RemoteViews(_context.PackageName, Resource.Layout.wiride_widget);
            views.SetTextViewText(Resource.Id.widget_title, _snapshot.Title);
            views.SetTextViewText(Resource.Id.widget_detail, _snapshot.Detail);
            views.SetTextViewText(Resource.Id.widget_riders, string.Join("\n", _snapshot.Riders.Take(3)));
            views.SetTextViewText(Resource.Id.widget_mute,
                !_snapshot.Active ? Loc.T("Open") : _snapshot.Muted ? Loc.T("WidgetUnmute") : Loc.T("WidgetMute"));

            views.SetOnClickPendingIntent(Resource.Id.widget_root, OpenAppIntent());

            if (_snapshot.Active)
            {
                var mute = new Intent(_context, typeof(WiRideWidgetProvider)).SetAction(WiRideWidgetProvider.ActionToggleMute);
                views.SetOnClickPendingIntent(Resource.Id.widget_mute,
                    PendingIntent.GetBroadcast(_context, 1, mute, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent));
            }
            else
            {
                views.SetOnClickPendingIntent(Resource.Id.widget_mute, OpenAppIntent());
            }

            manager.UpdateAppWidget(component, views);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Widget-Update fehlgeschlagen: {ex.Message}");
        }
    }

    // ---------------- Mini-Fenster ----------------

    public static bool CanDrawOverlays =>
        !OperatingSystem.IsAndroidVersionAtLeast(23) || global::Android.Provider.Settings.CanDrawOverlays(AndroidApp.Context);

    public static void RequestOverlayPermission()
    {
        var intent = new Intent(global::Android.Provider.Settings.ActionManageOverlayPermission,
            global::Android.Net.Uri.Parse("package:" + AndroidApp.Context.PackageName));
        intent.AddFlags(ActivityFlags.NewTask);
        AndroidApp.Context.StartActivity(intent);
    }

    private void RefreshOverlay()
    {
        bool show = _inBackground && _snapshot.Active && AppSettings.OverlayEnabled && CanDrawOverlays;

        if (!show)
        {
            RemoveOverlay();
            return;
        }

        if (_overlay == null)
            CreateOverlay();

        _overlayTitle!.Text = _snapshot.Title;
        _overlayDetail!.Text = _snapshot.Detail;
        _overlayRiders!.Text = string.Join("\n", _snapshot.Riders.Take(5));
        _overlayRiders.Visibility = _snapshot.Riders.Count > 0 ? ViewStates.Visible : ViewStates.Gone;
        _overlayMute!.Text = _snapshot.Muted ? Loc.T("WidgetUnmute") : Loc.T("WidgetMute");
    }

    private void CreateOverlay()
    {
        var wm = _context.GetSystemService(Context.WindowService)!.JavaCast<IWindowManager>()!;
        float dp = _context.Resources!.DisplayMetrics!.Density;

        var background = new GradientDrawable();
        background.SetColor(Color.ParseColor("#EE1A1D23"));
        background.SetCornerRadius(18 * dp);

        _overlay = new LinearLayout(_context) { Orientation = Orientation.Vertical, Background = background };
        _overlay.SetPadding((int)(14 * dp), (int)(10 * dp), (int)(14 * dp), (int)(10 * dp));

        _overlayTitle = Text(14, "#F5F6F7", bold: true);
        _overlayDetail = Text(12, "#9AA0A8");
        _overlayRiders = Text(12, "#32D74B");

        var muteBackground = new GradientDrawable();
        muteBackground.SetColor(Color.ParseColor("#FF8A00"));
        muteBackground.SetCornerRadius(12 * dp);
        _overlayMute = Text(13, "#141414", bold: true);
        _overlayMute.Background = muteBackground;
        _overlayMute.Gravity = GravityFlags.Center;
        _overlayMute.SetPadding((int)(10 * dp), (int)(6 * dp), (int)(10 * dp), (int)(6 * dp));
        _overlayMute.Click += (_, _) => ToggleMute();

        _overlay.AddView(_overlayTitle);
        _overlay.AddView(_overlayDetail);
        _overlay.AddView(_overlayRiders);
        var muteParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = (int)(8 * dp)
        };
        _overlay.AddView(_overlayMute, muteParams);

        var layout = new WindowManagerLayoutParams(
            ViewGroup.LayoutParams.WrapContent,
            ViewGroup.LayoutParams.WrapContent,
            OperatingSystem.IsAndroidVersionAtLeast(26) ? WindowManagerTypes.ApplicationOverlay : WindowManagerTypes.Phone,
            WindowManagerFlags.NotFocusable,
            Format.Translucent)
        {
            Gravity = GravityFlags.Top | GravityFlags.Start,
            X = (int)(12 * dp),
            Y = (int)(160 * dp)
        };

        _overlay.SetOnTouchListener(new DragListener(wm, layout, () => OpenAppIntent().Send()));
        wm.AddView(_overlay, layout);
    }

    private TextView Text(float size, string color, bool bold = false)
    {
        var view = new TextView(_context) { TextSize = size };
        view.SetTextColor(Color.ParseColor(color));
        if (bold)
            view.SetTypeface(null, TypefaceStyle.Bold);
        return view;
    }

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;

        try
        {
            _context.GetSystemService(Context.WindowService)!.JavaCast<IWindowManager>()!.RemoveView(_overlay);
        }
        catch { }

        _overlay = null;
    }

    // Verschieben per Finger; kurzes Tippen oeffnet die App
    private class DragListener : Java.Lang.Object, AView.IOnTouchListener
    {
        private readonly IWindowManager _wm;
        private readonly WindowManagerLayoutParams _params;
        private readonly Action _onTap;
        private float _startX, _startY;
        private int _originX, _originY;
        private bool _moved;

        public DragListener(IWindowManager wm, WindowManagerLayoutParams layoutParams, Action onTap)
        {
            _wm = wm;
            _params = layoutParams;
            _onTap = onTap;
        }

        public bool OnTouch(AView? v, MotionEvent? e)
        {
            if (v == null || e == null)
                return false;

            switch (e.Action)
            {
                case MotionEventActions.Down:
                    _startX = e.RawX;
                    _startY = e.RawY;
                    _originX = _params.X;
                    _originY = _params.Y;
                    _moved = false;
                    return true;

                case MotionEventActions.Move:
                    int dx = (int)(e.RawX - _startX);
                    int dy = (int)(e.RawY - _startY);
                    if (Math.Abs(dx) > 10 || Math.Abs(dy) > 10)
                        _moved = true;
                    _params.X = _originX + dx;
                    _params.Y = _originY + dy;
                    _wm.UpdateViewLayout(v, _params);
                    return true;

                case MotionEventActions.Up:
                    if (!_moved)
                        _onTap();
                    return true;
            }

            return false;
        }
    }
}
