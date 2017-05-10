using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQBStatic
{
    public static class StringExtension
    {
        /// <summary>
        /// With ";" + NewLine as separator.
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public static string FlattenStringsList(this IEnumerable<string> list)
        {
            string result = "";
            foreach (string str in list)
                result = $"{result} ; {Environment.NewLine} {str}";
            return result;
        }
        public static string Append(this string str, params string[] strings)
        {
            return string.Concat(str, string.Concat(strings));
        }
        public static string PutAhead(this string str, params string[] strings)
        {
            return string.Concat(string.Concat(strings), str);
        }

        public static string RemoveOrThis(this string str, int startIndex, int count)
        {
            if (startIndex < 0
                || startIndex > str.Length
                || (startIndex + count) > str.Length)
                return str;
            else return str.Remove(startIndex, count);
        }
    }
}
