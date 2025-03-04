﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nybus;
using Nybus.Configuration;
using RabbitMQ.Client;

namespace MessageSender
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var services = new ServiceCollection();

                services.AddLogging(logging => { logging.AddConsole(); });

                services.AddNybus(nybus =>
                {
                    nybus.UseRabbitMqBusEngine(rabbitMq =>
                    {
                        rabbitMq.Configure(configuration =>
                        {
                            configuration.ConnectionFactory = new ConnectionFactory
                            {
                                HostName = "localhost",
                                UserName = "guest",
                                Password = "guest"
                            };
                        });
                    });
                });

                var serviceProvider = services.BuildServiceProvider();

                var host = serviceProvider.GetRequiredService<IBusHost>();

                await host.StartAsync();

                //int educationId = int.Parse(args[0]);

                //Console.WriteLine($"Queueing translation to English for education: {educationId}");

                await host.Bus.InvokeCommandAsync(new TranslateCommand
                {
                    ToLanguage = Language.English,
                    EducationId = 880997
                });

                await host.StopAsync();
            }
            catch (Exception e)
            {

                throw;
            }
           
        }
    }

    [Message("TranslateEducationCommand", "Examples")]
    public class TranslateCommand : ICommand
    {
        public int EducationId { get; set; }

        public Language ToLanguage { get; set; }
    }

    // https://docs.aws.amazon.com/translate/latest/dg/what-is.html
    public enum Language
    {
        English = 1,
        German = 2,
        Swedish = 3,
        Norwegian = 4,
        Finnish = 5,
        Danish = 6,
        French = 7,
        Italian = 8,
        Russian = 9,
        ChineseSimplified = 10
    }
}
