﻿using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MicrosoftGraphAPIBot.MicrosoftGraph;
using MicrosoftGraphAPIBot.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace MicrosoftGraphBotTests
{
    [TestClass]
    public class BindHandlerTests
    {
        [TestMethod]
        public async Task TestGetAuthUrlAsync()
        {
            Guid clientId = Guid.NewGuid();
            var mocks = Utils.CreateDefaultGraphApiMock(string.Empty);
            await Utils.SetOneValueDbContextAsync(clientId);
            BotDbContext db = Utils.CreateMemoryDbContext();
            DefaultGraphApi defaultGraphApi = new DefaultGraphApi(db, mocks.Item1, mocks.Item2);

            BindHandler bindHandler = new BindHandler(db, defaultGraphApi);
            (string, string) results = await bindHandler.GetAuthUrlAsync(clientId.ToString());

            string email = "test@onmicrosoft.com";
            string message = $"https://login.microsoftonline.com/{DefaultGraphApi.GetTenant(email)}/oauth2/v2.0/authorize?client_id={clientId}&response_type=code&redirect_uri={HttpUtility.UrlEncode(BindHandler.AppUrl)}&response_mode=query&scope={HttpUtility.UrlEncode(DefaultGraphApi.Scope)}";

            Assert.AreEqual(message, results.Item2);
        }

        [TestMethod]
        public async Task TestRegAppAsync()
        {
            string testResultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApiResults", "ValidApplicationResult.json");
            string json = File.ReadAllText(testResultPath);
            var mocks = Utils.CreateDefaultGraphApiMock(json);
            BotDbContext db = Utils.CreateMemoryDbContext();
            DefaultGraphApi defaultGraphApi = new DefaultGraphApi(db, mocks.Item1, mocks.Item2);

            BindHandler bindHandler = new BindHandler(db, defaultGraphApi);

            long userId = 123456;
            string userName = "Test Bot";
            string email = "test@onmicrosoft.com";
            Guid clientId = Guid.NewGuid();
            string clientSecret = "741852963";
            string appName = "app1";

            await bindHandler.RegAppAsync(userId, userName, email, clientId.ToString(), clientSecret, appName);
            await db.DisposeAsync();
            db = Utils.CreateMemoryDbContext();
            AzureApp azureApp = await db.AzureApps.Include(azureApp => azureApp.TelegramUser).FirstAsync();
            Assert.AreEqual(userId, azureApp.TelegramUserId);
            Assert.AreEqual(userName, azureApp.TelegramUser.UserName);
            Assert.AreEqual(email, azureApp .Email);
            Assert.AreEqual(clientId, azureApp.Id);
            Assert.AreEqual(clientSecret, azureApp.Secrets);
            Assert.AreEqual(appName, azureApp.Name);
        }

        [ExpectedException(typeof(InvalidOperationException))]
        [TestMethod]
        public async Task TestRegInvalidAppAsync()
        {
            string testResultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApiResults", "InvalidApplicationResult.json");
            string json = File.ReadAllText(testResultPath);
            var mocks = Utils.CreateDefaultGraphApiMock(json);
            BotDbContext db = Utils.CreateMemoryDbContext();
            DefaultGraphApi defaultGraphApi = new DefaultGraphApi(db, mocks.Item1, mocks.Item2);

            BindHandler bindHandler = new BindHandler(db, defaultGraphApi);

            long userId = 123456;
            string userName = "Test Bot";
            string email = "test@onmicrosoft.com";
            Guid clientId = Guid.NewGuid();
            string clientSecret = "741852963";
            string appName = "app1";

            await bindHandler.RegAppAsync(userId, userName, email, clientId.ToString(), clientSecret, appName);
        }

        [ExpectedException(typeof(InvalidOperationException))]
        [TestMethod]
        public async Task TestRegAppInvalidEmailAsync()
        {
            string testResultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApiResults", "InvalidApplicationResult.json");
            string json = File.ReadAllText(testResultPath);
            var mocks = Utils.CreateDefaultGraphApiMock(json);
            BotDbContext db = Utils.CreateMemoryDbContext();
            DefaultGraphApi defaultGraphApi = new DefaultGraphApi(db, mocks.Item1, mocks.Item2);

            BindHandler bindHandler = new BindHandler(db, defaultGraphApi);

            long userId = 123456;
            string userName = "Test Bot";
            string email = "onmicrosoft.com";
            Guid clientId = Guid.NewGuid();
            string clientSecret = "741852963";
            string appName = "app1";

            await bindHandler.RegAppAsync(userId, userName, email, clientId.ToString(), clientSecret, appName);
        }

        [ExpectedException(typeof(InvalidOperationException))]
        [TestMethod]
        public async Task TestRegAppInvalidClientIdAsync()
        {
            string testResultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApiResults", "InvalidApplicationResult.json");
            string json = File.ReadAllText(testResultPath);
            var mocks = Utils.CreateDefaultGraphApiMock(json);
            BotDbContext db = Utils.CreateMemoryDbContext();
            DefaultGraphApi defaultGraphApi = new DefaultGraphApi(db, mocks.Item1, mocks.Item2);

            BindHandler bindHandler = new BindHandler(db, defaultGraphApi);

            long userId = 123456;
            string userName = "Test Bot";
            string email = "test@onmicrosoft.com";
            string clientId = "1da2d3as3d1321ad3a";
            string clientSecret = "741852963";
            string appName = "app1";

            await bindHandler.RegAppAsync(userId, userName, email, clientId, clientSecret, appName);
        }

        [TestMethod]
        public async Task TestDeleteAppAsync()
        {
            await Utils.SetDefaultValueDbContextAsync();
            BotDbContext db = Utils.CreateMemoryDbContext();
            Guid clientId1 = await db.AzureApps.AsQueryable().Select(app => app.Id).FirstAsync();
            await db.DisposeAsync();
            db = Utils.CreateMemoryDbContext();

            BindHandler bindHandler = new BindHandler(db, null);
            string deleteUrl = await bindHandler.DeleteAppAsync(clientId1.ToString());

            Assert.AreEqual(deleteUrl, string.Format(BindHandler.DeleteUrl, clientId1.ToString()));
            Assert.AreEqual(await db.AzureApps.AsQueryable().CountAsync(), 2);
            Assert.AreEqual(await db.AppAuths.AsQueryable().CountAsync(), 1);
        }

        [TestMethod]
        public async Task TestAppCountAsync()
        {
            await Utils.SetDefaultValueDbContextAsync();
            BotDbContext db = Utils.CreateMemoryDbContext();

            BindHandler bindHandler = new BindHandler(db, null);
            Assert.AreEqual(2, await bindHandler.AppCountAsync(123456789));
        }

        [TestMethod]
        public async Task TestAuthCountAsync()
        {
            await Utils.SetDefaultValueDbContextAsync();
            BotDbContext db = Utils.CreateMemoryDbContext();

            BindHandler bindHandler = new BindHandler(db, null);
            Assert.AreEqual(1, await bindHandler.AuthCountAsync(123456789));
        }

        [TestMethod]
        public async Task TestGetAppsNameAsync()
        {
            await Utils.SetDefaultValueDbContextAsync();
            BotDbContext db = Utils.CreateMemoryDbContext();

            BindHandler bindHandler = new BindHandler(db, null);
            var appsInfo = (await bindHandler.GetAppsNameAsync(123456789)).ToList();
            Assert.AreEqual(appsInfo[0].Item2, "App1");
            Assert.AreEqual(appsInfo[1].Item2, "App2");
        }

        [TestMethod]
        public async Task TestGetAppInfoAsync()
        {
            await Utils.SetDefaultValueDbContextAsync();
            BotDbContext db = Utils.CreateMemoryDbContext();
            Guid clientId1 = await db.AzureApps.AsQueryable().Select(app => app.Id).FirstAsync();
            await db.DisposeAsync();
            db = Utils.CreateMemoryDbContext();

            BindHandler bindHandler = new BindHandler(db, null);
            var appsInfo = (await bindHandler.GetAppInfoAsync(clientId1.ToString()));
            Assert.AreEqual(appsInfo.Name, "App1");
            Assert.AreEqual(appsInfo.Email, "test@onmicrosoft.com");
            Assert.AreEqual(appsInfo.Secrets, string.Empty);
            Assert.AreEqual(appsInfo.TelegramUserId, 123456789);
            Assert.AreEqual(appsInfo.Id, clientId1);
        }

        [TestMethod]
        public async Task TestBindAuthAsync()
        {
            string token = await Utils.GetTestToken();
            string authResponsePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApiResults", "ValidAuthResponse.json");
            string authResponse = File.ReadAllText(authResponsePath);
            string testResultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApiResults", "GetTokenSuccessResult.json");
            string json = File.ReadAllText(testResultPath);
            JObject jObject = JObject.Parse(json);
            jObject["access_token"] = token;
            json = JsonConvert.SerializeObject(jObject);
            Guid clientId = Guid.NewGuid();
            var mocks = Utils.CreateDefaultGraphApiMock(json);
            await Utils.SetOneValueDbContextAsync(clientId);
            BotDbContext db = Utils.CreateMemoryDbContext();
            DefaultGraphApi defaultGraphApi = new DefaultGraphApi(db, mocks.Item1, mocks.Item2);

            string name = "test Bind";
            BindHandler bindHandler = new BindHandler(db, defaultGraphApi);
            await bindHandler.BindAuthAsync(clientId.ToString(), authResponse, name);
            await db.DisposeAsync();
            db = Utils.CreateMemoryDbContext();
            Assert.AreEqual(2, await db.AppAuths.AsQueryable().CountAsync());
            Assert.IsTrue(await db.AppAuths.AsQueryable().AnyAsync(appAuth => appAuth.Name == name));
        }

        [ExpectedException(typeof(InvalidOperationException))]
        [TestMethod]
        public async Task TestBindAutInvalidAuthAsync()
        {
            string authResponsePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApiResults", "InvalidAuthResponse.json");
            string authResponse = File.ReadAllText(authResponsePath);

            BindHandler bindHandler = new BindHandler(null, null);
            await bindHandler.BindAuthAsync(Guid.Empty.ToString(), authResponse, string.Empty);
        }

        [ExpectedException(typeof(InvalidOperationException))]
        [TestMethod]
        public async Task TestBindAutInvalidJsonAsync()
        {
            string authResponse = @"{ ""Hi"": ""123"" }";
            BindHandler bindHandler = new BindHandler(null, null);
            await bindHandler.BindAuthAsync(Guid.Empty.ToString(), authResponse, string.Empty);
        }

        [ExpectedException(typeof(InvalidOperationException))]
        [TestMethod]
        public async Task TestBindAutErrorFormatAsync()
        {
            BindHandler bindHandler = new BindHandler(null, null);
            await bindHandler.BindAuthAsync(Guid.Empty.ToString(), string.Empty, string.Empty);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await Utils.DeleteDBAsync();
        }
    }
}
