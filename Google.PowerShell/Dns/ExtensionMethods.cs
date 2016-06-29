using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Google.PowerShell.Dns.ExtensionMethods
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// Check if an array is null or empty.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <returns>Returns boolean value of whether the array is null/empty (true) or not (false)</returns>
        /// <citation>
        /// Retrieved and very slightly modified on 6.28.16 from:
        /// http://stackoverflow.com/questions/8560106/isnullorempty-equivalent-for-array-c-sharp, Danyal Aytekin
        /// </citation>
        public static bool IsNullOrEmpty<T>(this T[] array)
        {
            return (array == null || array.Length == 0);
        }
    }
}
