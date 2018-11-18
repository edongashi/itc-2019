using System;
using System.Xml.Linq;

namespace Timetabling.Common.Utils
{
    internal static class XmlUtilities
    {
        public static string RequiredAttribute(this XElement element, string name)
        {
            var attr = element.Attribute(name);
            var val = attr?.Value;
            if (attr == null || string.IsNullOrEmpty(val))
            {
                throw new InvalidOperationException($"Required attribute '{name}' is missing.");
            }

            return val;
        }

        public static string OptionalAttribute(this XElement element, string name)
        {
            var attr = element.Attribute(name);
            var val = attr?.Value;
            if (attr == null || string.IsNullOrEmpty(val))
            {
                return null;
            }

            return val;
        }

        public static int RequiredId(this XElement element)
        {
            return RequiredInteger(element, "id") - 1;
        }

        public static int RequiredInteger(this XElement element, string name)
        {
            return int.Parse(element.RequiredAttribute(name));
        }

        public static int OptionalInteger(this XElement element, string name, int defaultValue)
        {
            var value = element.OptionalAttribute(name);
            return value != null ? int.Parse(value) : defaultValue;
        }

        public static uint RequiredBinary(this XElement element, string name)
        {
            return UIntFromBinary(element.RequiredAttribute(name));
        }

        private static uint UIntFromBinary(string str)
        {
            return Convert.ToUInt32(str, 2);
        }
    }
}
