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
using Tort.Models;
using Tort.Models.GameJsonModels;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Tort.Middleware
{

    static class Global
    {
        public static void SendMessagePool(string message)
        {
            foreach(var h in HandlersPool)
            {
                if (h != null)
                {
                    h.message = message;
                }
                else
                {
                    HandlersPool.Remove(h);
                }                
            }
        }

        public static List<SocketHandler> HandlersPool { get; set; } = new List<SocketHandler>();
    }

    public class SocketHandler
    {
        ApplicationDbContext _context;

        public string message;

        public const int BufferSize = 4096;

        WebSocket socket;
        

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
                if (message != null)
                {
                    var data = Encoding.UTF8.GetBytes(message);
                    buffer = new ArraySegment<byte>(data);
                    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                    message = null;
                }                                              
            }
        }

        

        private async Task ReceiveData()
        {
            var buffer = new ArraySegment<byte>(new byte[BufferSize]);

            while (this.socket.State == WebSocketState.Open)
            {
                var incoming = await this.socket.ReceiveAsync(buffer, CancellationToken.None);
                if (incoming.MessageType == WebSocketMessageType.Text)
                {
                    Global.SendMessagePool(Encoding.UTF8.GetString(buffer.Array));
                }                
            }
        }

        private static async Task Acceptor(HttpContext hc, Func<Task> n)
        {
            if (!hc.WebSockets.IsWebSocketRequest)
                await n();

            var context = (ApplicationDbContext)hc.RequestServices.GetService(typeof(ApplicationDbContext));

            var socket = await hc.WebSockets.AcceptWebSocketAsync();
            var h = new SocketHandler(socket, context);
            Global.HandlersPool.Add(h);
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