using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Firebase.Cloud.Messaging.Utils
{
    static class ByteArray
    {
        // TODO: optimize this method to use pointers or bit shifts
        // check if it is working with big endian
        public static ulong ReadUInt64(byte[] bytes, int startIndex, int length)
        {
            byte[] buffer = new byte[8];
            Buffer.BlockCopy(bytes, startIndex, buffer, 8 - length, length);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }

            return BitConverter.ToUInt64(buffer, 0);
        }

        // TODO: check if it is working with big endian
        public static void WriteUInt64(byte[] source, ulong value, int startIndex, int length)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            Buffer.BlockCopy(source, startIndex, buffer, 0, length);
        }

        public static byte[] Slice(byte[] source, int startIndex, int endIndex)
        {
            Debug.Assert(startIndex < endIndex);

            int length = endIndex - startIndex;
            byte[] result = new byte[length];
            Buffer.BlockCopy(source, startIndex, result, 0, length);
            return result;
        }

        public static byte[] Concat(byte[] first, byte[] second)
        {
            byte[] ret = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            return ret;
        }
    }
}
