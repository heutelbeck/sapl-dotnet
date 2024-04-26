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

namespace SAPL.AspNetCore.Security.Constraints
{
    /// <summary>
    /// Represents an item responsible for handling specific constraints.
    /// Contains a key-value pair and a list of nested properties, if any.
    /// </summary>
    public class ResponsibleItem
    {
        /// <summary>
        /// Constructs a new ResponsibleItem with specified key, value and nested properties.
        /// </summary>
        /// <param name="key">The key of the item.</param>
        /// <param name="value">The value associated with the key.</param>
        /// <param name="properties">Nested responsible items representing more detailed properties.</param>
        public ResponsibleItem(string key, string value, List<ResponsibleItem> properties)
        {
            Key = key;
            Value = value;
            Properties = properties;
        }

        /// <summary>
        /// Gets the key of the responsible item.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the value associated with the key.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets a combined key-value string representation of the responsible item.
        /// </summary>
        public string KeyValue
        {
            get
            {
                return $"{Key}:{Value}";
            }
        }

        /// <summary>
        /// Gets the list of nested responsible items.
        /// </summary>
        public List<ResponsibleItem> Properties { get; }

        /// <summary>
        /// Determines whether the responsible item matches a specified JSON token, representing a constraint.
        /// </summary>
        /// <param name="constraint">The JSON token to match against.</param>
        /// <returns>True if the item matches the constraint; otherwise, false.</returns>
        public bool IsMatch(JToken constraint)
        {
            // Check if the constraint has children and attempt to match them.
            if (constraint.Children().Any())
            {
                foreach (JToken item in constraint.Children())
                {
                    JEnumerable<JToken> itemProperties = item.Children<JToken>();

                    foreach (JToken itemProperty in itemProperties)
                    {
                        var property = item.ToObject<JProperty>();
                        var value = itemProperty.ToObject<string>();
                        return Key.Equals(property?.Name) && Value.Equals(value);
                    }
                }
            }
            // Fall back to matching the whole constraint as a simple key-value pair.
            return KeyValue.Equals(constraint.Value<string>());
        }
    }
}
