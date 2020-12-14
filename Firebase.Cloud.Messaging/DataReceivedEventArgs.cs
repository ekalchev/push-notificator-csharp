using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Firebase.Cloud.Messaging
{
    class DataReceivedEventArgs
    {
        public DataReceivedEventArgs(byte[] buffer, int length)
        {
            Buffer = buffer;
            Length = length;
        }

        public byte[] Buffer { get; }
        public int Length { get; }
    }
}
