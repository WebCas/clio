using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;


namespace Clio.SignalR;

public class ChatHub : Hub
{

	public override async Task OnConnectedAsync() {
		await Clients.All.SendAsync("ReceiveMessage", "Server", $"{Context.ConnectionId} joined the chat");
	}

	public async Task SendMessage(string user, string message)
		=> await Clients.All.SendAsync("ReceiveMessage", user, message);

}