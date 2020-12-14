using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

static class StreamExtensions
{
    public static long UnreadBytesCount(this Stream stream)
    {
        return stream.Length - stream.Position;
    }
}
