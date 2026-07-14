using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PrintingBooksPortal.Data;
using PrintingBooksPortal.Models;
using PrintingBooksPortal.Services;

namespace PrintingBooksPortal.Hubs;

[Authorize(Roles = "Shop")]
public class PrintJobHub : Hub
{
    private readonly AppDbContext _db;
    private readonly ILogger<PrintJobHub> _logger;

    public PrintJobHub(AppDbContext db, ILogger<PrintJobHub> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RequestPrint(int bookId, int copies)
    {
        var userId = Context.UserIdentifier ?? Context.ConnectionId;
        var userName = Context.User?.Identity?.Name ?? "unknown";

        _logger.LogInformation("SignalR print request from {User} for book {BookId}, {Copies} copies", userName, bookId, copies);

        await Clients.Caller.SendAsync("PrintRequested", new
        {
            bookId,
            copies,
            status = "logged"
        });
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("PrintJobHub client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("PrintJobHub client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
