namespace Timetabling.Common.Utils
{
    public static class Utilities
    {
        public static int BitCount(uint v)
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
    }
}
