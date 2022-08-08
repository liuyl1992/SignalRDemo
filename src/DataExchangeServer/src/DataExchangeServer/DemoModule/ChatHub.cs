using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System;

namespace DataExchangeServer
{
    [Authorize]
    public class ChatHub : Hub
    {
        private static ConcurrentDictionary<int, string> ORDER_ConnectionIds = new ConcurrentDictionary<int, string>();

        public ChatHub()
        {
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetorder"></param>
        /// <param name="currentOrder">current index</param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task SendMessage(string targetorder, int currentOrder, string message)
        {
            // 存储kafka，
            var connectId = Context.ConnectionId;
            System.Console.WriteLine($"connectId={connectId} is sending a message");
            if (targetorder.Contains("A"))
            {
                //下级给上级发消息
                var parentConnectionIds = ORDER_ConnectionIds.Where(s => s.Key < currentOrder).ToList();
                foreach (var item in parentConnectionIds)
                {
                    await Clients.Client(item.Value.ToString()).SendAsync("ReceiveMessage", targetorder, item.Value.ToString(), message);
                    //await Clients.All.SendAsync("ReceiveMessage", targetorder, message);
                    System.Console.WriteLine($"");
                }
                return;
            }

            //message 要发的客户端标识
            var targetConnectionId = ORDER_ConnectionIds.Where(s => s.Key == int.Parse(targetorder)).FirstOrDefault();
            //上级给指定下级发消息
            await Clients.Client(targetConnectionId.Value.ToString()).SendAsync("ReceiveMessage", targetorder, targetConnectionId.Value.ToString(), message);
            System.Console.WriteLine($"connectId={connectId} is sending a message to {targetConnectionId}");
        }

        public override async Task OnConnectedAsync()
        {
            var connectId = Context.ConnectionId;
            System.Console.WriteLine($"A new client is connected. connectId={connectId}");
            int maxOrder = 0;
            if (!ORDER_ConnectionIds.Any())
            {
                maxOrder = 1;
                ORDER_ConnectionIds.TryAdd(1, connectId);
            }
            else
            {
                maxOrder = ORDER_ConnectionIds.Max(s => s.Key) + 1;
                ORDER_ConnectionIds.TryAdd(maxOrder, connectId);
            }

            await Clients.All.SendAsync("Init", connectId, maxOrder);
            //cluster deploy:
            //1、message information is stored in a distributed database
            //2、Keep the session 
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            //if (exception == null)
            //    return;
            var connectId = Context.ConnectionId;

            var connectionId = ORDER_ConnectionIds.Where(s => s.Value == connectId).FirstOrDefault().Key;
            ORDER_ConnectionIds.Remove(connectionId, out string vale);
            await Task.CompletedTask;
            Console.WriteLine($"EXCEPTION!!!:{exception?.Message}");
        }
    }
}
