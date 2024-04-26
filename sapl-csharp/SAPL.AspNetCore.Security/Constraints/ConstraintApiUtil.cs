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

using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Constraints.Providers;

namespace SAPL.AspNetCore.Security.Constraints
{
    /// <summary>
    /// Provides utility methods for creating constraint handler bundles and filtering providers.
    /// </summary>
    internal static class ConstraintApiUtil
    {
        /// <summary>
        /// Creates an instance of a blocking post-enforcement bundle for a collection.
        /// </summary>
        /// <param name="listType">The type of the list.</param>
        /// <param name="elementType">The element type in the list.</param>
        /// <param name="list">The list object.</param>
        /// <returns>A blocking post-enforce constraint handler bundle.</returns>
        internal static IBlockingPostEnforceConstraintHandlerBundle? CreaInstanceOfBlockingPostEnforcementbundle(Type listType, Type elementType, object? list)
        {
            // Define types for the generic parameters.
            Type[] types = new Type[2] { listType, elementType };

            // Get the generic type of the bundle.
            Type blockingPostEnforceBundleType = typeof(BlockingPostEnforceConstraintHandlerBundleForCollection<,>);

            // Construct the generic type and create an instance.
            Type constructed = blockingPostEnforceBundleType.MakeGenericType(types);
            var instance = Activator.CreateInstance(constructed);

            // Cast the instance to the desired type and set the result value.
            var bundle = instance as IBlockingPostEnforceConstraintHandlerBundle;
            if (bundle != null)
            {
                bundle.ResultValue = list;
            }
            return bundle;
        }

        /// <summary>
        /// Creates an instance of a JSON content filtering provider.
        /// </summary>
        /// <param name="listType">The type of the list to be filtered.</param>
        /// <returns>A JSON content filtering provider.</returns>
        internal static IJsonContentFilteringProvider? CreaInstanceOfJsonContentFilteringProvider(Type listType)
        {
            // Define types for the generic parameters.
            Type[] types = new Type[1] { listType };

            // Get the generic type of the constraint handler.
            Type constraintHandlerType = typeof(JsonContentFilteringProvider<>);

            // Construct the generic type and create an instance.
            Type constructed = constraintHandlerType.MakeGenericType(types);
            var instance = Activator.CreateInstance(constructed);

            // Cast the instance to the desired type.
            var constraintHandlerProvider = instance as IJsonContentFilteringProvider;
            return constraintHandlerProvider;
        }

        /// <summary>
        /// Retrieves the element type of any IEnumerable or Array.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>The element type if the type is an IEnumerable or Array, otherwise returns the original type.</returns>
        public static Type GetAnyElementTypeOfGenericList(Type type)
        {
            // Handle array types.
            if (type.IsArray)
                return type.GetElementType()!;

            // Handle generic IEnumerable types.
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];

            // Handle types that implement or extend IEnumerable<T>.
            var enumType = type.GetInterfaces()
                .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .Select(t => t.GenericTypeArguments[0]).FirstOrDefault();

            // Return the element type or the original type if not found.
            return enumType ?? type;
        }

        /// <summary>
        /// Creates an instance of a blocking post-enforcement bundle for a single object.
        /// </summary>
        /// <param name="objectType">The type of the object.</param>
        /// <param name="instance">The object instance.</param>
        /// <returns>A blocking post-enforce constraint handler bundle.</returns>
        internal static IBlockingPostEnforceConstraintHandlerBundle? CreaInstanceOfBlockingPostEnforcementbundle(Type objectType, object? instance)
        {
            // Define types for the generic parameters.
            Type[] types = new Type[1] { objectType };

            // Get the generic type of the bundle.
            Type blockingPostEnforceBundleType = typeof(BlockingPostEnforceConstraintHandlerConstraintHandlerBundleForElement<>);

            // Construct the generic type and create an instance.
            Type constructed = blockingPostEnforceBundleType.MakeGenericType(types);
            var instanceOfBundle = Activator.CreateInstance(constructed);

            // Cast the instance to the desired type and set the result value.
            var bundle = instanceOfBundle as IBlockingPostEnforceConstraintHandlerBundle;
            if (bundle != null)
            {
                bundle.ResultValue = instance;
            }
            return bundle;
        }
    }
}
