// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using IdentityModel.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Client
{
    public class Program
    {
        private static async Task Main()
        {
            // discover endpoints from metadata
            var client = new HttpClient();
            int currentOrder = 0;
            string currentConnectId = "";

            var disco = await client.GetDiscoveryDocumentAsync("https://localhost:5001");
            if (disco.IsError)
            {
                Console.WriteLine(disco.Error);
                return;
            }

            // request token
            var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = disco.TokenEndpoint,
                ClientId = "client",
                ClientSecret = "secret",

                //Scope = "api1"
            });

            if (tokenResponse.IsError)
            {
                Console.WriteLine(tokenResponse.Error);
                return;
            }

            Console.WriteLine(tokenResponse.Json);
            Console.WriteLine("\n\n");

            // call dataexchange server
            var connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:6000/ChatHub", options =>
                {
                    //options.Headers["access_token"] = tokenResponse.AccessToken;
                    options.AccessTokenProvider = () => Task.FromResult(tokenResponse.AccessToken);
                })
               .Build();

            connection.On<string,string, string>("ReceiveMessage", (target, connectId, message) =>
            {
                var newMessage = $"connectId={connectId}----{target}: {message}";
                Console.WriteLine($"receive message={newMessage}");
            });

            connection.On<string, int>("Init", (connectId, order) =>
            {
                if (currentOrder != 0)
                    return;
                currentOrder = order;
                currentConnectId = connectId;
                var newMessage = $"{connectId}: {order}";
                Console.WriteLine($"current client data is :{newMessage}");
            });

            try
            {
                //conneced
                await connection.StartAsync();

            }
            catch (Exception ex)
            {
            }

            connection.Closed += async (error) =>
            {
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
            };

            goto notice;
        notice:
            Console.WriteLine(@"A:给所有上级发送;数字：给指定下级发送");
            var targetorder = Console.ReadLine();
            if(string.IsNullOrEmpty(targetorder))
                goto notice;
            await connection.InvokeAsync("SendMessage",
                  targetorder, currentOrder, $"message{DateTime.Now}");

            goto notice;
        }
    }
}