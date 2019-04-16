using System;

namespace Timetabling.Internal.Utils
{
    internal static class Utilities
    {
        private const int WordBits = 32;

        internal static uint FloorLog2(uint x)
        {
            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);

            return (uint)(BitCount(x) - 1);
        }

        internal static uint CeilingLog2(uint x)
        {
            int y = (int)(x & (x - 1));

            y |= -y;
            y >>= (WordBits - 1);
            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);

            return (uint)(BitCount(x) - 1 - y);
        }

        internal static int BitCount(uint v)
        {
            var c = v - ((v >> 1) & 0x55555555u);
            c = ((c >> 2) & 0x33333333u) + (c & 0x33333333u);
            c = ((c >> 4) + c) & 0x0F0F0F0Fu;
            c = ((c >> 8) + c) & 0x00FF00FFu;
            c = ((c >> 16) + c) & 0x0000FFFFu;
            unchecked
            {
                return (int)c;
            }
        }

        internal static string ToBinary(this uint value, int length)
        {
            var val = Convert.ToString(value, 2);
            if (val.Length < length)
            {
                return val.PadLeft(length, '0');
            }

            if (val.Length > length)
            {
                return val.Substring(val.Length - length);
            }

            return val;
        }

        internal static int[] Shuffle(int n, Random random)
        {
            var result = new int[n];
            for (var i = 0; i < n; i++)
            {
                var j = random.Next(0, i + 1);
                if (i != j)
                {
                    result[i] = result[j];
                }

                result[j] = i;
            }

            return result;
        }
    }
}
