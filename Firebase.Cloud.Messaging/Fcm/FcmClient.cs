using Firebase.Cloud.Messaging.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Firebase.Cloud.Messaging.Fcm
{
    class FcmClient
    {
        private const string subscribeUrl = "https://fcm.googleapis.com/fcm/connect/subscribe";
        private const string sendUrl = "https://fcm.googleapis.com/fcm/send";

        public async Task<FcmRegistration> Register(ulong senderId, string token, byte[] publicKey, byte[] authSecret)
        {
            byte[] randomBytes = new byte[16]; ;
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(randomBytes);
            }

            Dictionary<string, string> postData = new Dictionary<string, string>();
            postData.Add("authorized_entity", senderId.ToString());
            postData.Add("endpoint", $"{sendUrl}/{token}");
            postData.Add("encryption_key", UrlSafeBase64Convertor.ToBase64(publicKey));
            postData.Add("encryption_auth", UrlSafeBase64Convertor.ToBase64(authSecret));

            JObject jsonData = null;


            using (var httpClient = new HttpClient())
            using (var content = new FormUrlEncodedContent(postData))
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.ExpectContinue = false;
                httpClient.DefaultRequestHeaders.ConnectionClose = true;

                HttpResponseMessage response = await httpClient.PostAsync(subscribeUrl, content).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        jsonData = (JObject)DeserializeFromStream(stream);
                    }
                }
                else
                {
                    throw new InvalidOperationException("Cannot register to FCM");
                }
            }

            return new FcmRegistration((string)((JValue)jsonData["token"]).Value, (string)((JValue)jsonData["pushSet"]).Value);
        }

        public static object DeserializeFromStream(Stream stream)
        {
            var serializer = new JsonSerializer();

            using (var sr = new StreamReader(stream))
            using (var jsonTextReader = new JsonTextReader(sr))
            {
                return serializer.Deserialize(jsonTextReader);
            }
        }
    }
}
