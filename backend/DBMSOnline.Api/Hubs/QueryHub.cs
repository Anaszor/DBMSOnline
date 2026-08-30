using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace DBMSOnline.Api.Hubs;

public class QueryHub : Hub
{
    public async Task JoinWorkspace(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Tenant_{tenantId}");
        await Clients.Group($"Tenant_{tenantId}").SendAsync("UserJoinedWorkspace", Context.ConnectionId);
    }

    public async Task LeaveWorkspace(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Tenant_{tenantId}");
        await Clients.Group($"Tenant_{tenantId}").SendAsync("UserLeftWorkspace", Context.ConnectionId);
    }

    public async Task BroadcastQueryProgress(string queryId, string status, int rowsProcessed)
    {
        await Clients.Caller.SendAsync("QueryProgress", new { queryId, status, rowsProcessed, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastAuditAlert(string userRole, string action, string riskLevel)
    {
        await Clients.All.SendAsync("AuditAlert", new { userRole, action, riskLevel, timestamp = DateTime.UtcNow });
    }
}
