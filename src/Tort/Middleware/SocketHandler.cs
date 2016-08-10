using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text;
using Tort.Data;
using System.Linq;
using Newtonsoft.Json;

namespace Tort.Middleware
{
    public class SocketHandler
    {
        ApplicationDbContext _context;

        public const int BufferSize = 4096;

        WebSocket socket;

        private bool hasNewMessage = false;

        private object locker;

        private SocketHandler(WebSocket socket, ApplicationDbContext context)
        {
            this.socket = socket;
            _context = context;
        }

        private async Task SendData()
        {
            var buffer = new ArraySegment<byte>(new byte[BufferSize]);
            
            while (this.socket.State == WebSocketState.Open)
            {
                var data = Encoding.UTF8.GetBytes($"HELLO");                
                buffer = new ArraySegment<byte>(data);
                if (hasNewMessage)
                {
                    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                    hasNewMessage = false;
                }                                              
            }
        }

        private async Task Fake()
        {
            while (true)
            {
                await Task.Delay(1000);
                hasNewMessage = true;                              
            }
        }

        private async Task ReceiveData()
        {
            var buffer = new ArraySegment<byte>(new byte[BufferSize]);

            while (this.socket.State == WebSocketState.Open)
            {
                var incoming = await this.socket.ReceiveAsync(buffer, CancellationToken.None);
                hasNewMessage = true;
            }
        }

        private static async Task Acceptor(HttpContext hc, Func<Task> n)
        {
            if (!hc.WebSockets.IsWebSocketRequest)
                await n();

            var context = (ApplicationDbContext)hc.RequestServices.GetService(typeof(ApplicationDbContext));

            var socket = await hc.WebSockets.AcceptWebSocketAsync();
            var h = new SocketHandler(socket, context);
            h.Fake();
            h.ReceiveData();
            h.SendData();            
        }
        public static void Map(IApplicationBuilder app)
        {
            app.UseWebSockets();
            app.Use(SocketHandler.Acceptor);
        }
    }
}