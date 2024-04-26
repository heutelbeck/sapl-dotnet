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

using Microsoft.AspNetCore.Http;
using SAPL.PDP.Api;

namespace SAPL.AspNetCore.Security.Extensions
{
    public static class HttpContextExtensions
    {
        /// <summary>
        /// Adds a new item to the HttpContext, using a specified name for primitive types.
        /// </summary>
        /// <param name="context">The HttpContext to add the item to.</param>
        /// <param name="value">The object to be stored.</param>
        /// <param name="itemName">The name to be used as a key, mandatory for primitive types.</param>
        /// <exception cref="ArgumentException">Thrown if itemName is not provided for primitive types.</exception>
        public static void AddItem(this HttpContext context, object value, string? itemName = null)
        {
            var type = value.GetType();
            string name = string.Empty;
            if (type.IsPrimitive || type == typeof(decimal) || type == typeof(string))
            {
                if (string.IsNullOrEmpty(itemName))
                {
                    throw new ArgumentException($"The type of the {nameof(value)} is primitive. The {nameof(itemName)} has to be set");
                }
                name = itemName;
            }
            else
            {
                name = type.Name;
            }
            context.RemoveItem(name);
            context.Items.Add(name, value);
        }


        /// <summary>
        /// Removes an item from the HttpContext based on its name.
        /// </summary>
        /// <param name="context">The HttpContext to remove the item from.</param>
        /// <param name="itemName">The name of the item to remove.</param>
        public static void RemoveItem(this HttpContext context, string itemName)
        {
            if (context.Items.ContainsKey(itemName))
            {
                context.Items.Remove(itemName);
            }
        }


        /// <summary>
        /// Retrieves a named item from the HttpContext.
        /// </summary>
        /// <param name="context">The HttpContext to get the item from.</param>
        /// <param name="itemName">The name of the item to retrieve.</param>
        /// <returns>The item as a string, or an empty string if not found.</returns>
        public static string? GetItem(this HttpContext context, string itemName)
        {
            if (context.Items.ContainsKey(itemName))
            {
                return context.Items[itemName] as string;
            }
            return string.Empty;
        }

        /// <summary>
        /// Retrieves the AuthorizationSubscription object from the HttpContext.
        /// </summary>
        /// <param name="context">The HttpContext to retrieve the object from.</param>
        /// <returns>The AuthorizationSubscription object if present; otherwise, null.</returns>
        public static AuthorizationSubscription? AuthorizationSubscription(this HttpContext context)
        {
            if (context.Items.ContainsKey(nameof(AuthorizationSubscription)))
            {
                return context.Items[nameof(AuthorizationSubscription)] as AuthorizationSubscription;
            }
            return null;
        }

        /// <summary>
        /// Retrieves the AuthorizationDecision object from the HttpContext.
        /// </summary>
        /// <param name="context">The HttpContext to retrieve the object from.</param>
        /// <returns>The AuthorizationDecision object if present; otherwise, null.</returns>
        public static AuthorizationDecision? AuthorizationDecision(this HttpContext context)
        {
            if (context.HasAuthorizationAuthorizationPublisher())
            {
                return context.AuthorizationAuthorizationPublisher()?.AuthorizationDecision;
            }
            if (context.Items.ContainsKey(nameof(AuthorizationDecision)))
            {
                return context.Items[nameof(AuthorizationDecision)] as AuthorizationDecision;
            }
            return null;
        }

        /// <summary>
        /// Retrieves the AuthorizationPublisher object from the HttpContext.
        /// </summary>
        /// <param name="context">The HttpContext to retrieve the object from.</param>
        /// <returns>The AuthorizationPublisher object if present; otherwise, null.</returns>
        public static AuthorizationPublisher? AuthorizationAuthorizationPublisher(this HttpContext context)
        {
            if (context.Items.ContainsKey(nameof(AuthorizationPublisher)))
            {
                return context.Items[nameof(AuthorizationPublisher)] as AuthorizationPublisher;
            }
            return null;
        }

        /// <summary>
        /// Determines whether an AuthorizationPublisher object is present in the HttpContext.
        /// </summary>
        /// <param name="context">The HttpContext to check.</param>
        /// <returns>True if an AuthorizationPublisher object is present; otherwise, false.</returns>
        public static bool HasAuthorizationAuthorizationPublisher(this HttpContext context)
        {
            return AuthorizationAuthorizationPublisher(context) != null;
        }

    }
}
