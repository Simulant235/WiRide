namespace WiRide.Services;

// Kurzfassung der laufenden Sitzung fuer Widget und Mini-Fenster
public record SessionSnapshot(bool Active, bool Muted, string Title, string Detail, IReadOnlyList<string> Riders)
{
    public static SessionSnapshot Idle => new(false, false, "WiRide", Loc.T("WidgetIdle"), Array.Empty<string>());
}

// Anzeige ausserhalb der App (Android: Home-Widget und schwebendes Mini-Fenster)
public interface IStatusSurface
{
    void Update(SessionSnapshot snapshot);
    void SetAppInBackground(bool background);
}

public class NoStatusSurface : IStatusSurface
{
    public void Update(SessionSnapshot snapshot) { }
    public void SetAppInBackground(bool background) { }
}

// Haelt Widget und Mini-Fenster aktuell (bei jeder Aenderung und alle 2 s waehrend einer Sitzung)
public class StatusPublisher
{
    private readonly IntercomService _intercom;
    private readonly IStatusSurface _surface;
    private Timer? _timer;

    public StatusPublisher(IntercomService intercom, IStatusSurface surface)
    {
        _intercom = intercom;
        _surface = surface;
    }

    public void Start()
    {
        _intercom.StateChanged += (_, _) => Publish();
        _timer = new Timer(_ => { if (_intercom.Role != IntercomRole.None) Publish(); }, null, 2000, 2000);
        Publish();
    }

    public void SetAppInBackground(bool background) => _surface.SetAppInBackground(background);

    public void Publish() => _surface.Update(Build());

    private SessionSnapshot Build()
    {
        var network = _intercom.Network;

        switch (_intercom.Role)
        {
            case IntercomRole.Host:
            {
                var riders = network.ConnectedClients
                    .OrderBy(c => c.ClientName)
                    .Select(c => $"{c.ClientName} · {c.LatencyText}")
                    .ToList();
                return new SessionSnapshot(true, _intercom.IsMuted, Loc.T("HostRunning"),
                    Loc.T("RidersCount", riders.Count), riders);
            }

            case IntercomRole.Client:
            {
                var detail = network.IsConnected
                    ? (network.LatencyMs > 0 ? Loc.T("ClientPing", network.LatencyMs) : Loc.T("ClientConnectionOk"))
                    : Loc.T("ClientReconnecting");
                return new SessionSnapshot(true, _intercom.IsMuted,
                    Loc.T("ClientConnectedTo", network.HostName ?? network.HostIp), detail, Array.Empty<string>());
            }

            default:
                return SessionSnapshot.Idle;
        }
    }
}
