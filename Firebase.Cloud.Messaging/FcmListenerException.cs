using System;
using System.Collections.Generic;
using System.Text;

namespace Firebase.Cloud.Messaging
{
    class FcmListenerException : Exception
    {
        public FcmListenerException(string message) : base(message)
        {
        }

        public FcmListenerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
