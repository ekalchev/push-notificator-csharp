using CheckinProto;
using Google.Protobuf;
using Firebase.Cloud.Messaging.Fcm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Firebase.Cloud.Messaging.Gcm
{
    class GcmClient
    {
        private const string registerUrl = "https://android.clients.google.com/c2dm/register3";
        private const string checkinUrl = "https://android.clients.google.com/checkin";
        private static readonly MediaTypeHeaderValue protobufContentType = new MediaTypeHeaderValue("application/x-protobuf");
        private static readonly MediaTypeHeaderValue formUrlencodedContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        private async Task<CheckinData> CheckIn(ulong androidId, ulong securityToken)
        {
            AndroidCheckinRequest request = new AndroidCheckinRequest()
            {
                UserSerialNumber = 0,
                Checkin = new AndroidCheckinProto()
                {
                    Type = DeviceType.DeviceChromeBrowser,
                    ChromeBuild = new ChromeBuildProto()
                    {
                        Platform = ChromeBuildProto.Types.Platform.Mac,
                        ChromeVersion = "63.0.3234.0",
                        Channel = ChromeBuildProto.Types.Channel.Stable
                    },
                },
                Version = 3,
                Id = Convert.ToInt64(androidId),
                SecurityToken = securityToken
            };

            using (var httpClient = new HttpClient())
            using (var memoryStream = new MemoryStream())
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.ConnectionClose = true;

                request.WriteTo(memoryStream);
                memoryStream.Position = 0;

                StreamContent content = new StreamContent(memoryStream);
                content.Headers.ContentType = protobufContentType;

                HttpResponseMessage response = await httpClient.PostAsync(checkinUrl, content).ConfigureAwait(false);

                using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    AndroidCheckinResponse result = AndroidCheckinResponse.Parser.ParseFrom(responseStream);
                    return new CheckinData(result.AndroidId, result.SecurityToken);
                }
            }
        }

        public async Task<GcmRegistration> Register(string appId)
        {
            var checkinData = await CheckIn(0, 0).ConfigureAwait(false);
            string token = await MakeRegisterRequest(checkinData, appId).ConfigureAwait(false);

            return new GcmRegistration(token, checkinData.AndroidId, checkinData.SecurityToken, appId);
        }

        private async Task<string> MakeRegisterRequest(CheckinData checkinData, string appId)
        {
            Dictionary<string, string> postData = new Dictionary<string, string>();
            postData.Add("app", "org.chromium.linux");
            postData.Add("X-subtype", appId);
            postData.Add("device", checkinData.AndroidId.ToString());
            postData.Add("sender", Server.Base64Key);

            string token = string.Empty;
            int maxNumRetries = 5;
            int numRetries = 0;

            using (var httpClient = new HttpClient())
            {
                while (numRetries <= maxNumRetries)
                {
                    using (var content = new FormUrlEncodedContent(postData))
                    {
                        httpClient.DefaultRequestHeaders.Clear();
                        httpClient.DefaultRequestHeaders.ExpectContinue = false;
                        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"AidLogin {checkinData.AndroidId}:{checkinData.SecurityToken}");
                        httpClient.DefaultRequestHeaders.ConnectionClose = true;

                        try
                        {
                            HttpResponseMessage response = await httpClient.PostAsync(registerUrl, content).ConfigureAwait(false);

                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                                {
                                    StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                                    var responseString = reader.ReadToEnd();
                                    var tokens = responseString.Split('=');

                                    if(tokens[0] != "Error")
                                    {
                                        token = tokens[1];
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                throw new InvalidOperationException("Cannot register to GCM");
                            }
                        }
                        catch(Exception ex)
                        {
                            if(numRetries >= maxNumRetries)
                            {
                                throw new InvalidOperationException("Cannot register to GCM", ex); ;
                            }

                            await Task.Delay(1000);
                        }
                    }

                    numRetries++;
                }
            }

            return token;
        }

        private class CheckinData
        {
            public CheckinData(ulong androidId, ulong securityToken)
            {
                AndroidId = androidId;
                SecurityToken = securityToken;
            }

            public ulong AndroidId { get; }
            public ulong SecurityToken { get; }
        }
    }
}
