using Google.Protobuf;
using McsProto;
using Firebase.Cloud.Messaging.Fcm;
using Firebase.Cloud.Messaging.Gcm;
using Firebase.Cloud.Messaging.Utils;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Firebase.Cloud.Messaging
{
    public class PushNotificationService
    {
        private GcmRegistration gcmRegistration;
        private FcmRegistration fcmRegistration;
        private HashSet<string> persistentIds = new HashSet<string>();
        private Decryptor cryptography;

        public event EventHandler<string> MessageReceived;

        public async Task<string> Register(string senderId)
        {
            string appId = "wp:receiver.push.com#" + Guid.NewGuid().ToString();
            cryptography = new Decryptor();

            GcmClient gcmClient = new GcmClient();
            FcmClient fcmClient = new FcmClient();
            gcmRegistration = await gcmClient.Register(appId);
            fcmRegistration = await fcmClient.Register(senderId, gcmRegistration.Token, cryptography.PublicKey, cryptography.AuthSecret);

            return fcmRegistration.Token;
        }

        public async Task Run(CancellationToken cancellationToken)
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                using (FcmListener fcmListener = new FcmListener())
                {

                    try
                    {
                        fcmListener.MessageReceived += FcmListener_MessageReceived;

                        await fcmListener.ConnectAsync();
                        await fcmListener.LoginAsync(gcmRegistration.AndroidId, gcmRegistration.SecurityToken);
                        await fcmListener.ListenAsync();
                    }
                    catch(FcmListenerException ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                    catch(IOException ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                    finally
                    {
                        fcmListener.MessageReceived -= FcmListener_MessageReceived;
                    }
                }
            }
        }

        private void FcmListener_MessageReceived(object sender, IMessage message)
        {
            if(message is LoginResponse)
            {
                persistentIds = new HashSet<string>();
            }
            else if (message is DataMessageStanza dataMessageStanza)
            {
                OnDataMessage(dataMessageStanza);
            }
        }

        private void OnDataMessage(DataMessageStanza dataMessageStanza)
        {
            if(persistentIds.Contains(dataMessageStanza.PersistentId) == false)
            {
                persistentIds.Add(dataMessageStanza.PersistentId);
                DecriptData(dataMessageStanza);
            }
        }

        private void DecriptData(DataMessageStanza dataMessageStanza)
        {
            string cryptoKey = dataMessageStanza.AppData.Where(item => item.Key == "crypto-key").Select(item => item.Value).FirstOrDefault();
            string salt = dataMessageStanza.AppData.Where(item => item.Key == "encryption").Select(item => item.Value).FirstOrDefault();

            if (cryptoKey != null && salt != null)
            {
                byte[] cryptoKeyBytes = UrlSafeBase64Convertor.FromBase64(cryptoKey.Substring(3));
                byte[] saltBytes = UrlSafeBase64Convertor.FromBase64(salt.Substring(5));
                var decryptedBytes = cryptography.Decrypt(dataMessageStanza.RawData.ToByteArray(), cryptoKeyBytes, saltBytes);

                Volatile.Read(ref MessageReceived)?.Invoke(this, Encoding.UTF8.GetString(decryptedBytes));
            }
            else
            {
                if (cryptoKey != null)
                {
                    Debug.WriteLine("CryptoKey is missing");
                }

                if (salt != null)
                {
                    Debug.WriteLine("salt is missing");
                }
            }
        }
    }
}
