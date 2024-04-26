/*
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Newtonsoft.Json.Linq;

namespace SAPL.AspNetCore.Security.Subscription
{
    /// <summary>
    /// Provides utility methods for reading content from JTokens representing SAPL obligations.
    /// </summary>
    public static class ObligationContentReaderUtil
    {
        /// <summary>
        /// Retrieves a property from a JToken.
        /// </summary>
        /// <param name="token">The JToken from which to extract the property.</param>
        /// <param name="propertyName">The name of the property to extract.</param>
        /// <returns>The extracted JToken property or null if not found.</returns>
        public static JToken? GetProperty(JToken token, string propertyName)
        {
            JToken? property;
            try
            {
                property = token[propertyName];
            }
            catch (Exception)
            {
                property = null;
            }
            return property;
        }

        /// <summary>
        /// Retrieves a value of a specific type from a JToken.
        /// </summary>
        /// <typeparam name="T">The type of value to extract.</typeparam>
        /// <param name="token">The JToken from which to extract the value.</param>
        /// <returns>The value of the specified type.</returns>
        public static T? GetValue<T>(JToken token)
        {
            return token.Value<T>();
        }

        /// <summary>
        /// Extracts the "type" value from an obligation JToken.
        /// </summary>
        /// <param name="token">The JToken representing an obligation.</param>
        /// <returns>The "type" value of the obligation.</returns>
        public static string? GetObligationType(JToken token)
        {
            var obligationFromJson = GetProperty(token, "obligation");
            if (obligationFromJson != null)
            {
                var obligationTypeFromJson = GetProperty(obligationFromJson, "type");
                if (obligationTypeFromJson != null)
                {
                    var obligationTypeFromJsonvalue = GetValue<string>(obligationTypeFromJson);
                    return obligationTypeFromJsonvalue;
                }
            }
            else
            {
                var obligationTypeFromJson = GetProperty(token, "type");
                if (obligationTypeFromJson != null)
                {
                    var obligationTypeFromJsonvalue = GetValue<string>(obligationTypeFromJson);
                    return obligationTypeFromJsonvalue;
                }
            }
            return null;
        }

        /// <summary>
        /// Extracts an array of "conditions" from an obligation JToken.
        /// </summary>
        /// <param name="obligation">The JToken representing an obligation.</param>
        /// <returns>An array of conditions if present; otherwise, null.</returns>
        public static JArray? GetConditionsFromObligation(JToken obligation)
        {
            var conditions = ObligationContentReaderUtil.GetProperty(obligation, "conditions");
            if (conditions != null)
            {
                var conditionsArray = JArray.FromObject(conditions);
                return conditionsArray;
            }
            return null;
        }

        /// <summary>
        /// Retrieves the value of a specific property as a string from a JToken.
        /// </summary>
        /// <param name="token">The JToken from which to extract the property value.</param>
        /// <param name="propertyName">The name of the property whose value is to be extracted.</param>
        /// <returns>The string value of the property.</returns>
        public static string? GetPropertyValue(JToken token, string propertyName)
        {
            var property = GetProperty(token, propertyName);
            if (property != null)
            {
                return GetValue<string>(property);
            }
            return null;
        }
    }
}
