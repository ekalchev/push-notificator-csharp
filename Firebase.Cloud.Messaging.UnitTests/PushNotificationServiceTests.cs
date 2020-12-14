using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
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
        [Test]
        public async Task Test1()
        {
            PushNotificationService service = new PushNotificationService();

            // this value comes from FCM clould messaging console.
            string serverKey = "AAAAgEVyvhc:APA91bEHA0m7aafyv6swqqKJJlTXPkFfuRhvQWfN_TE87gAwz2tAJkMdoVeFpfrRi6bLIYHeF73yNCi_lEnNKDbuUiDnaW8S4qz39lP0VP2XSWZQK9JKJq7HVy6xt6Rp9cbBGs96wXXF";
            var token = await service.Register(serverKey);

            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();

            service.MessageReceived += (s, msg) => 
            {
                tcs.TrySetResult(msg);
            };

            var serviceRunTask = service.Run(default);
            //await SendMessageAsync(token);

            await Task.WhenAll(Task.Delay(5000), tcs.Task);

            var jObject = JObject.Parse(tcs.Task.GetAwaiter().GetResult());

            Thread.Sleep(1000000000);
            //Assert.AreEqual()
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
