using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Firebase.Cloud.Messaging
{
    static class CodedInputStreamInternals
    {
        /// <summary>
        /// This is copied from the source code of protobuf, since it is internal method and there is no public alternative
        /// https://chromium.googlesource.com/external/github.com/google/protobuf/+/HEAD/csharp/src/Google.Protobuf/CodedInputStream.cs
        /// if you are updating protobuf version make sure you check for changes in the implementation
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static uint ReadRawVarint32(Stream input)
        {
            int result = 0;
            int offset = 0;
            for (; offset < 32; offset += 7)
            {
                int b = input.ReadByte();
                if (b == -1)
                {
                    throw new InvalidOperationException("InvalidProtocolBufferException.TruncatedMessage");
                }
                result |= (b & 0x7f) << offset;
                if ((b & 0x80) == 0)
                {
                    return (uint)result;
                }
            }
            // Keep reading up to 64 bits.
            for (; offset < 64; offset += 7)
            {
                int b = input.ReadByte();
                if (b == -1)
                {
                    throw new InvalidOperationException("InvalidProtocolBufferException.TruncatedMessage");
                }
                if ((b & 0x80) == 0)
                {
                    return (uint)result;
                }
            }
            throw new InvalidOperationException("InvalidProtocolBufferException.TruncatedMessage");
        }
    }
}
