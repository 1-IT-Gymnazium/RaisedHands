using Microsoft.AspNetCore.SignalR;

namespace RaisedHands.Api.Hubs
{
    public class QuestionHub : Hub
    {
        public async Task SendMessage(string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", message);
        }
    }
}

