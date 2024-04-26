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

using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace SAPL.AspNetCore.Security.Test.TestConfiguration
{
    /// <summary>
    /// Is needed for integration tests with an in memory-testserver
    /// Excludes all controller except given type
    /// </summary>
    public class ControllerTypeResolver
        : IApplicationFeatureProvider<ControllerFeature>
    {
        private readonly Type m_type;

        public ControllerTypeResolver(Type controllerType)
        {
            m_type = controllerType;
        }
        public void PopulateFeature(
            IEnumerable<ApplicationPart> parts,
            ControllerFeature feature)
        {
            var controllers = feature.Controllers.ToList();
            foreach (var controller in controllers)
            {
                if (!controller.Name.Equals(m_type.GetTypeInfo().Name))
                {
                    feature.Controllers.Remove(controller);
                }
            }
        }
    }

}
