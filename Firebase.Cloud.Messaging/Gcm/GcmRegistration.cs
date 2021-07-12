using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Firebase.Cloud.Messaging.Gcm
{
    public class GcmRegistration
    {
        public GcmRegistration(string token, ulong androidId, ulong securityToken, string appId)
        {
            Token = token;
            AndroidId = androidId;
            SecurityToken = securityToken;
            AppId = appId;
        }

        public string Token { get; }
        public ulong AndroidId { get; }
        public ulong SecurityToken { get; }
        public string AppId { get; }
    }
}
