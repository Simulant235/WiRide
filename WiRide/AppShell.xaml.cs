namespace WiRide
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Arabisch: Layout von rechts nach links
            FlowDirection = Services.Loc.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }
    }
}
