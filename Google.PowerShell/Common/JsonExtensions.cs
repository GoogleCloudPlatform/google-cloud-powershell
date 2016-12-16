using Newtonsoft.Json.Linq;
using System;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// Static class that contains extension static to manipulate JToken.
    /// </summary>
    public static class JsonExtensions
    {
        /// <summary>
        /// Search the JToken and its children recursively for a key that matches the given key.
        /// If such a key is found, set the value to the ref variable value and returns true.
        /// Otherwise, returns false.
        /// </summary>
        public static bool TryGetPropertyValue(this JToken propertiesJson, string key, ref string value)
        {
            if (propertiesJson == null)
            {
                return false;
            }

            if (propertiesJson.Type == JTokenType.Array)
            {
                foreach (JToken childToken in propertiesJson.Children())
                {
                    if (childToken.TryGetPropertyValue(key, ref value))
                    {
                        return true;
                    }
                }
            }
            else if (propertiesJson.Type == JTokenType.Object)
            {
                // We iterate through each child token of type JProperty, if the child token has
                // the same name as key, then we are done. Otherwise, recursively call this method
                // with the child token to continue the search.
                foreach (JProperty childProperty in propertiesJson.Children<JProperty>())
                {
                    if (string.Equals(childProperty.Name, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = childProperty.Value?.Value<string>();
                        return true;
                    }
                    if (childProperty.Value.TryGetPropertyValue(key, ref value))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
