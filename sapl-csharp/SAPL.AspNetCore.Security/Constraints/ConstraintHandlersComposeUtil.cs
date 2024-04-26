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

using System.Composition.Hosting;
using System.Reflection;
using SAPL.AspNetCore.Security.Constraints.api;

namespace SAPL.AspNetCore.Security.Constraints
{
    /// <summary>
    /// Utility class for composing constraint handler providers from specified assemblies.
    /// It uses Managed Extensibility Framework (MEF) for dynamic discovery and composition.
    /// </summary>
    public class ConstraintHandlersComposeUtil
    {
        /// <summary>
        /// Searches and composes instances of IRunnableDelegateConstraintHandlerProvider from the specified assemblies.
        /// </summary>
        /// <param name="locations">List of assemblies to search for providers.</param>
        /// <returns>A list of composed IRunnableDelegateConstraintHandlerProviders.</returns>
        public List<IRunnableDelegateConstraintHandlerProvider> SearchRunnableConstraintHandlerProviders(List<Assembly> locations)
        {
            List<IRunnableDelegateConstraintHandlerProvider>? providers = new List<IRunnableDelegateConstraintHandlerProvider>();

            // Loop through each assembly and use MEF to compose providers.
            foreach (Assembly assembly in locations)
            {
                var configuration = new ContainerConfiguration()
                    .WithAssembly(assembly);
                using var container = configuration.CreateContainer();
                providers.AddRange(container.GetExports<IRunnableDelegateConstraintHandlerProvider>());
            }
            return providers;
        }

        /// <summary>
        /// Searches and composes instances of IConsumerDelegateConstraintHandlerProvider from the specified assemblies.
        /// </summary>
        /// <param name="locations">List of assemblies to search for providers.</param>
        /// <returns>A list of composed IConsumerDelegateConstraintHandlerProviders.</returns>
        public List<IConsumerDelegateConstraintHandlerProvider<string>> SearchIConsumerConstraintHandlerProviders(List<Assembly> locations)
        {
            List<IConsumerDelegateConstraintHandlerProvider<string>>? providers = new List<IConsumerDelegateConstraintHandlerProvider<string>>();

            // Loop through each assembly and use MEF to compose providers.
            foreach (Assembly assembly in locations)
            {
                var configuration = new ContainerConfiguration()
                    .WithAssembly(assembly);
                using var container = configuration.CreateContainer();
                providers.AddRange(container.GetExports<IConsumerDelegateConstraintHandlerProvider<string>>());
            }
            return providers;
        }

        /// <summary>
        /// Searches and composes instances of IActionExecutingContextConstraintHandlerProvider from the specified assemblies.
        /// </summary>
        /// <param name="locations">List of assemblies to search for providers.</param>
        /// <returns>A list of composed IActionExecutingContextConstraintHandlerProviders.</returns>
        public List<IActionExecutingContextConstraintHandlerProvider>? SearchIActionActionExecutingContextConstraintHandlerProvider(List<Assembly> locations)
        {
            List<IActionExecutingContextConstraintHandlerProvider>? providers = new List<IActionExecutingContextConstraintHandlerProvider>();

            // Loop through each assembly and use MEF to compose providers.
            foreach (Assembly assembly in locations)
            {
                var configuration = new ContainerConfiguration()
                    .WithAssembly(assembly);
                using var container = configuration.CreateContainer();
                providers.AddRange(container.GetExports<IActionExecutingContextConstraintHandlerProvider>());
            }
            return providers;
        }
    }
}
