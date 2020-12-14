using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Firebase.Cloud.Messaging.Gcm
{
    class GcmRegistration
    {
        public GcmRegistration(string token, ulong androidId, ulong securityToken)
        {
            Token = token;
            AndroidId = androidId;
            SecurityToken = securityToken;
        }

        public string Token { get; }
        public ulong AndroidId { get; }
        public ulong SecurityToken { get; }
    }
}
