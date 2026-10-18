using Microsoft.Extensions.DependencyInjection;

namespace WiRide
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            Services.Loc.Initialize();
            Services.ThemeManager.Apply();
            RequestedThemeChanged += (_, _) => Services.ThemeManager.Apply();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            // Widget/Mini-Fenster aktuell halten; Mini-Fenster nur, wenn die App im Hintergrund ist
            var status = Services.ServiceHelper.Get<Services.StatusPublisher>();
            window.Created += (_, _) => status.Start();
            window.Stopped += (_, _) => status.SetAppInBackground(true);
            window.Resumed += (_, _) => status.SetAppInBackground(false);
            window.Activated += (_, _) => status.SetAppInBackground(false);

            return window;
        }
    }
}