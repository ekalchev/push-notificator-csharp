using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using McsProto;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Firebase.Cloud.Messaging.UnitTests
{
    class PushNotificationServiceTests
    {
        //[Test]
        //public async Task TestSetupForActualServerCommunication()
        //{
        //    PushNotificationService service = new PushNotificationService();

        //    // this value comes from FCM clould messaging console.
        //    string serverKey = "AAAAgEVyvhc:APA91bEHA0m7aafyv6swqqKJJlTXPkFfuRhvQWfN_TE87gAwz2tAJkMdoVeFpfrRi6bLIYHeF73yNCi_lEnNKDbuUiDnaW8S4qz39lP0VP2XSWZQK9JKJq7HVy6xt6Rp9cbBGs96wXXF";
        //    var token = await service.Register(serverKey);
        //    Debug.WriteLine(token);

        //    TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();

        //    service.MessageReceived += (s, msg) =>
        //    {
        //        tcs.TrySetResult(msg);
        //    };

        //    var serviceRunTask = service.Run(default);
        //    //await SendMessageAsync(token);
        //    await serviceRunTask;

        //    await Task.WhenAll(Task.Delay(5000), tcs.Task);

        //    var jObject = JObject.Parse(tcs.Task.GetAwaiter().GetResult());

        //    Thread.Sleep(1000000000);
        //    //Assert.AreEqual()
        //}

        [Test]
        public async Task WhenReceiveHeartBeatPing_WhileListeningForPushNotification_ShouldSendBackHeartBeatAck()
        {
            FakeFcmConnection connection = null;
            bool sentHeartBeatAck = false;
            bool expectHeartBeatAck = true;

            connection = new FakeFcmConnection((ctoken) =>
            {
                byte[] buffer = new byte[2048];
                byte[] fakeData;

                fakeData = new byte[] { 41 };
                connection.SimulateDataReceived(fakeData);
                fakeData = new byte[] { 3, 62, 10, 18, 99, 104, 114, 111, 109, 101, 45, 54, 51, 46, 48, 46, 51, 50, 51, 52, 46, 48, 18, 31, 117, 115, 101, 114, 64, 102, 105, 114, 101, 98, 97, 115, 101, 46, 99, 111, 109, 47, 110, 111, 116, 105, 102, 105, 99, 97, 116, 105, 111, 110, 115, 48, 1, 64, 192, 216, 151, 177, 230, 46,
 };
                connection.SimulateDataReceived(fakeData);

                fakeData = new byte[] { 7, 10, 16, 1, 26, 0, 58, 4, 8, 12, 18, 0, };
                connection.SimulateDataReceived(fakeData);

                expectHeartBeatAck = true;
                fakeData = new byte[] { 0, 0 };
                connection.SimulateDataReceived(fakeData);
                expectHeartBeatAck = false;

                return Task.CompletedTask;
            });

            connection.DataSent += (s, data) =>
            {
                if (expectHeartBeatAck == true)
                {
                    using (var memoryStream = new MemoryStream(data))
                    {
                        MessageTag currentTag = (MessageTag)memoryStream.ReadByte(); // read messageTag byte

                        if (currentTag == MessageTag.kHeartbeatAckTag)
                        {
                            var instance = HeartbeatAck.Parser.ParseDelimitedFrom(memoryStream);
                            if (instance != null)
                            {
                                sentHeartBeatAck = true;
                            }
                        }
                    }
                }
            };

            FcmListener listener = new FcmListener(connection);
            await listener.ConnectAsync();
            await listener.LoginAsync(0, 0);
            await listener.ListenAsync();
            Assert.IsTrue(sentHeartBeatAck);
        }

        [Test]
        public async Task FcmListenerConnect_LoginRequest_ShouldBeTheFirstDataSentToTheServer()
        {
            FakeFcmConnection connection = null;
            bool expectLoginRequest = true;
            int expectedVersion = 41;
            int version = 0;
            bool sentLoginRequest = false;

            connection = new FakeFcmConnection((ctoken) => Task.CompletedTask);

            connection.DataSent += (s, data) =>
            {
                if (expectLoginRequest == true)
                {
                    expectLoginRequest = false;

                    using (var memoryStream = new MemoryStream(data))
                    {
                        version = memoryStream.ReadByte();
                        Assert.AreEqual(MessageTag.kLoginRequestTag, (MessageTag)memoryStream.ReadByte());

                        var instance = LoginRequest.Parser.ParseDelimitedFrom(memoryStream);
                        if (instance != null)
                        {
                            sentLoginRequest = true;
                        }
                    }
                }
            };

            FcmListener listener = new FcmListener(connection);
            await listener.ConnectAsync();
            await listener.LoginAsync(0, 0);
            await listener.ListenAsync();

            Assert.AreEqual(expectedVersion, version);
            Assert.IsTrue(sentLoginRequest);
        }

        private async Task SendMessageAsync(string token)
        {
            // google-credentials.json is downloaded from FCM clould messaging console
            string credentials = ReadManifestData("google-credentials.json");

            var app = FirebaseApp.Create(new AppOptions()
            {
                Credential = GoogleCredential.FromJson(credentials)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging")
            });

            var messaging = FirebaseMessaging.GetMessaging(app);

            var message = new Message()
            {
                Token = token,
            };

            await messaging.SendAsync(message);
        }

        public static string ReadManifestData(string embeddedFileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().First(s => s.EndsWith(embeddedFileName, StringComparison.CurrentCultureIgnoreCase));

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Could not load manifest resource stream.");
                }
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
