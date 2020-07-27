﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MicrosoftGraphAPIBot.MicrosoftGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MicrosoftGraphAPIBot.Telegram
{
    /// <summary>
    /// 處理 Telegram Bot 相關行為
    /// </summary>
    public partial class TelegramController
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly ITelegramBotClient botClient;
        private readonly ApiCallManager apiCallManager;
        private readonly BindHandler bindHandler;
        private readonly TelegramCommandGenerator commandGenerator;
        private readonly TelegramHandler telegramHandler;
        private readonly Dictionary<string, (Func<Message, Task>, Func<Message, Task>, Func<CallbackQuery, Task>)> Controller;

        /// <summary>
        /// Create a new TelegramHandler instance.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="configuration"></param>
        /// <param name="botClient"></param>
        /// <param name="bindHandler"></param>
        /// <param name="commandGenerator"></param>
        public TelegramController(ILogger<TelegramController> logger, IConfiguration configuration, ITelegramBotClient botClient, 
            ApiCallManager apiCallManager, BindHandler bindHandler, TelegramCommandGenerator commandGenerator, TelegramHandler telegramHandler)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.apiCallManager = apiCallManager;
            this.botClient = botClient;
            this.bindHandler = bindHandler;
            this.commandGenerator = commandGenerator;
            this.telegramHandler = telegramHandler;

            // key = 指令, value = (指令對應的方法, 使用者回復指令訊息對應的方法, 使用者回復選擇按鈕對應的方法)
            Controller = new Dictionary<string, (Func<Message, Task>, Func<Message, Task>, Func<CallbackQuery, Task>)>
            {
                { TelegramCommand.Start, (Start, null, null)},
                { TelegramCommand.Help, (Help, null, null) },
                { TelegramCommand.Bind, (Bind, null, null) },
                { TelegramCommand.RegApp, (RegisterApp, RegisterAppReplay, null) },
                { TelegramCommand.DeleteApp, (DeleteApp, null, DeleteAppCallback)},
                { TelegramCommand.QueryApp, (QueryApp, null, QueryAppCallback) },
                { TelegramCommand.BindAuth, (BindUserAuth, BindUserAuthReplay, BindUserAuthCallback) },
                { TelegramCommand.UnbindAuth, (UnbindUserAuth, null, UnbindUserAuthCallback) },
                { TelegramCommand.QueryAuth, (QueryUserAuth, null, QueryUserAuthCallback) },
                { TelegramCommand.RunApiTask, (RunApiTask, null, null) },
                { TelegramCommand.AddAdminPermission, (AddAdminPermission, AddAdminPermissionReplay, null) },
                { TelegramCommand.RemoveAdminPermission, (RemoveAdminPermission, null, null) }
            };
        }

        /// <summary>
        /// 發送訊息到指定的聊天室或 Telegram 使用者
        /// </summary>
        /// <param name="chatId"> Telegram 聊天Id </param>
        /// <param name="message"> 要發送的訊息 </param>
        /// <returns></returns>
        public async Task SendMessage(long chatId, string message)
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: message);
        }

        /// <summary>
        /// 處理來自 Bot 的訊息
        /// </summary>
        /// <param name="message"> Telegram message object </param>
        public async Task MessageReceivedHandler(Message message)
        {
            if (message == null || message.Type != MessageType.Text)
                return;

            logger.LogDebug("User Id: {0}", message.Chat.Id);
            await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

            if (message.ReplyToMessage != null && message.ReplyToMessage.From.Id == botClient.BotId)
            {
                string replyCommand = message.ReplyToMessage.Text.Split('\n').First();
                await Controller[replyCommand].Item2.Invoke(message);
                return;
            }

            string command = message.Text.Split(' ').First();

            if (Controller.ContainsKey(command))
            {
                await Controller[command].Item1.Invoke(message);
                return;
            }
                
            await Defult(message).ConfigureAwait(false);
        }

        /// <summary>
        /// 處理來自 Bot 的訊息
        /// </summary>
        /// <param name="callbackQuery"> Telegram callbackQuery object </param>
        public async Task CallbackQueryHandler(CallbackQuery callbackQuery)
        {
            if (callbackQuery == null)
                return;

            await botClient.SendChatActionAsync(callbackQuery.From.Id, ChatAction.Typing);

            if (callbackQuery.Message != null && callbackQuery.Message.From.Id == botClient.BotId)
            {
                string callbackCommand = callbackQuery.Message.Text.Split('\n').First();
                await Controller[callbackCommand].Item3.Invoke(callbackQuery);
            }
        }

        /// <summary>
        /// 處理 /start 事件
        /// </summary>
        /// <param name="message"> Telegram message object </param>
        /// <returns></returns>
        private async Task Start(Message message)
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: configuration["JoinBotMessage"]);

            await Help(message);
        }

        /// <summary>
        /// 處理 /help 事件
        /// </summary>
        /// <param name="message"> Telegram message object </param>
        /// <returns></returns>
        private async Task Help(Message message)
        {
            List<string> result = new List<string> { "指令選單:", ""};
            IEnumerable<(string, string)> menu = await commandGenerator.GenerateMenuCommandsAsync(message.Chat.Id);
            result.AddRange(menu.Select(command => $"{command.Item1, -15} {command.Item2}"));

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: string.Join('\n', result));
        }

        /// <summary>
        /// 處理 /bind 事件
        /// </summary>
        /// <param name="message"> Telegram message object </param>
        /// <returns></returns>
        private async Task Bind(Message message)
        {
            List<string> result = new List<string> { "綁定指令選單:", "" };
            IEnumerable<(string, string)> menu = await commandGenerator.GenerateBindCommandsAsync(message.Chat.Id);
            result.AddRange(menu.Select(command => $"{command.Item1,-15} {command.Item2}"));

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: string.Join('\n', result));
        }

        /// <summary>
        /// 手動執行任務(一般使用者)
        /// </summary>
        /// <param name="message"> Telegram message object </param>
        /// <returns></returns>
        private async Task RunApiTask(Message message)
        {
            var result = await apiCallManager.Run(message.Chat.Id);

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"執行結果: \n {result.Item2}");
        }

        /// <summary>
        /// 新增管理者權限
        /// 
        /// 向使用者要求管理者密碼
        /// </summary>
        /// <param name="message"> Telegram message object </param>
        /// <returns></returns>
        private async Task AddAdminPermission(Message message)
        {
            string command = GetAsyncMethodCommand(MethodBase.GetCurrentMethod());

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: command + "\n" + "請輸入管理者密碼",
                replyMarkup: new ForceReplyMarkup());
        }

        /// <summary>
        /// 新增管理者權限
        /// 
        /// 處理新增管理者權限
        /// </summary>
        /// <param name="message"> Telegram message object </param>
        /// <returns></returns>
        private async Task AddAdminPermissionReplay(Message message)
        {
            bool result = await telegramHandler.AddAdminPermission(message.Chat.Id, message.Chat.Username, message.Text);

            string resultMessage = "升級管理者權限: 失敗";

            if (result)
                resultMessage = "升級管理者權限: 成功";

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: resultMessage);
        }

        /// <summary>
        /// 移除管理者權限
        /// </summary>
        /// <param name="message"> Telegram message object </param>
        /// <returns></returns>
        private async Task RemoveAdminPermission(Message message)
        {
            await telegramHandler.RemoveAdminPermission(message.Chat.Id, message.Chat.Username);

            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "成功移除管理者權限");
        }

        /// <summary>
        /// 處理預設指令以外的事件
        /// </summary>
        /// <param name="message"> Telegram message object </param>
        /// <returns></returns>
        private async Task Defult(Message message)
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: string.Format("Hi @{0} 請使用 /help 獲得完整指令", message.Chat.Username));
        }
    }
}
