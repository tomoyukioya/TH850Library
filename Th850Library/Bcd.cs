using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Th850Library
{
    internal class Bcd
    {
        public static string ToString(byte[] data)
        {
            var result = "";
            foreach (var b in data)
            {
                result += (b >> 4) & 0xf;
                result += b & 0xf;
            }
            return result;
        }
    }
}
