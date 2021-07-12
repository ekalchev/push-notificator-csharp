using Firebase.Cloud.Messaging.Fcm;
using Firebase.Cloud.Messaging.Gcm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Firebase.Cloud.Messaging
{

    public class PushSettings
    {
        [JsonConstructor]
        public PushSettings(CryptoSettings cryptoSettings, GcmRegistration gcmRegistration, FcmRegistration fcmRegistration, HashSet<string> persistentIds)
        {
            CryptoSettings = cryptoSettings;
            GcmRegistration = gcmRegistration;
            FcmRegistration = fcmRegistration;
            PersistentIds = persistentIds;
        }

        public PushSettings(ulong senderId, string appId = null)
        {
            if (string.IsNullOrEmpty(appId))
            {
                appId = $"wp:receiver.push.com#{Guid.NewGuid()}";
            }
            CryptoSettings = Decryptor.GenerateCryptoSettings();
            GcmClient gcmClient = new GcmClient();
            FcmClient fcmClient = new FcmClient();
            GcmRegistration = gcmClient.Register(appId).Result;
            FcmRegistration = fcmClient.Register(senderId, GcmRegistration.Token, CryptoSettings.PublicKey, CryptoSettings.AuthSecret).Result;
            PersistentIds = new HashSet<string>();
        }

        public CryptoSettings CryptoSettings { get; private set; }

        public GcmRegistration GcmRegistration { get; private set; }

        public FcmRegistration FcmRegistration { get; private set; }

        public HashSet<string> PersistentIds { get; private set; }
    }
}
