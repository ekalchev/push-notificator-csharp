using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Firebase.Cloud.Messaging.Utils
{
    static class UrlSafeBase64Convertor
    {
        public static string ToBase64(byte[] bytes)
        {
            // this is url safe base64 encoding
            var base64string = Convert.ToBase64String(bytes);
            return base64string.Replace("=", string.Empty).Replace('+','-').Replace('/','_'); // not very efficient way for replacing, try to find a way to do it without generating intermediate strings
        }

        public static string ToBase64(string text)
        {
            return ToBase64(Encoding.UTF8.GetBytes(text));
        }

        public static byte[] FromBase64(string base64String)
        {
            string incoming = base64String.Replace('_', '/').Replace('-', '+');
            switch (base64String.Length % 4)
            {
                case 2: incoming += "=="; break;
                case 3: incoming += "="; break;
            }

            return Convert.FromBase64String(incoming);
        }
    }
}
