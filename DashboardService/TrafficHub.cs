using Microsoft.AspNetCore.SignalR;

namespace DashboardService;

public class TrafficHub : Hub
{
    private readonly IntersectionStateStore _store;

    public TrafficHub(IntersectionStateStore store)
    {
        _store = store;
    }

    // When a browser connects, immediately send it the current state
    // of all intersections so the map isn't blank on load
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("InitialState", _store.GetAll());
        await base.OnConnectedAsync();
    }
}
