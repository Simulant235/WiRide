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
            return new Window(new AppShell());
        }
    }
}