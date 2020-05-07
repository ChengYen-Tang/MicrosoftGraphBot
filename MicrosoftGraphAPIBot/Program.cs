﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MicrosoftGraphAPIBot.Models;
using MicrosoftGraphAPIBot.Telegram;
using System.Linq;

namespace MicrosoftGraphAPIBot
{
    class Program
    {
        static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.AddFile("Logs/{Date}.txt");
#if DEBUG
                    logging.SetMinimumLevel(LogLevel.Debug);
#else
                    logging.SetMinimumLevel(LogLevel.Information);
#endif
                })
                .ConfigureServices((hostContext, services) =>
                {
                    string SQLHost = hostContext.Configuration["MSSQL:Host"];
                    string SQLPort = hostContext.Configuration["MSSQL:Port"];
                    string SQLUser = hostContext.Configuration["MSSQL:User"];
                    string SQLPassword = hostContext.Configuration["MSSQL:Password"];
                    string SQLDataBase = hostContext.Configuration["MSSQL:DataBase"];
                    string DBConnection = string.Format("Data Source={0},{1};Initial Catalog={2};User ID={3};Password={4}", SQLHost, SQLPort, SQLDataBase, SQLUser, SQLPassword);
                    services.AddDbContext<BotDbContext>(options =>
                    {
                        options.UseSqlServer(DBConnection);
                    });

                    services.AddTransient<TelegramHandler>();
                    services.AddHostedService<TelegramBotService>();
                });
    }
}
