using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Firebase.Cloud.Messaging.Fcm
{
    public class FcmRegistration
    {
        public FcmRegistration(string token, string pushSet)
        {
            Token = token;
            PushSet = pushSet;
        }

        public string Token { get; }
        public string PushSet { get; }
    }
}
