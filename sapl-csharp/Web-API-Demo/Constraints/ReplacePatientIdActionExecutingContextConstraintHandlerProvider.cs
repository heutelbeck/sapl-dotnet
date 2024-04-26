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

using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Constraints;
using SAPL.AspNetCore.Security.Constraints.api;

namespace Web_API_Demo.Constraints
{
    /// <summary>
    /// Provides a constraint handler for replacing the patient ID in the context of an action being executed.
    /// It acts on specific constraints that match the criteria defined in 'responsibleFor'.
    /// </summary>
    public class ReplacePatientIdActionExecutingContextConstraintHandlerProvider : IActionExecutingContextConstraintHandlerProvider
    {
        private int id;

        private List<ResponsibleItem> resposibleFor = new()
            { new ResponsibleItem("manipulating", "changePatientId", new List < ResponsibleItem >()) };

        /// <summary>
        /// Specifies the signal on which this handler operates.
        /// </summary>
        /// <returns>The signal for the handler.</returns>
        public ISignal.Signal GetSignal()
        {
            return ISignal.Signal.ON_DECISION;
        }

        /// <summary>
        /// Determines if this handler is responsible for the given constraint.
        /// </summary>
        /// <param name="constraint">The JSON token representing the constraint.</param>
        /// <returns>True if responsible, false otherwise.</returns>
        public bool IsResponsible(JToken constraint)
        {
            if (resposibleFor.Any(r => r.IsMatch(constraint)))
            {
                id = GetId(constraint, nameof(id));
                return true;
            }
            return false;
        }

        public Action<ActionExecutingContext> Accept()
        {
            return Manipulate;
        }

        /// <summary>
        /// Manipulates the patient ID in the action executing context.
        /// Specifically, replaces the ID with the new specified ID.
        /// </summary>
        /// <param name="context">The action executing context containing action arguments.</param>
        private void Manipulate(ActionExecutingContext context)
        {
            IDictionary<string, object?> originalArguments = context.ActionArguments;
            IDictionary<string, object?> newArguments = new Dictionary<string, object?>(originalArguments);

            foreach (KeyValuePair<string, object?> argument in newArguments)
            {
                if (argument.Value is int value)
                {
                    if (value != id)
                    {
                        newArguments[argument.Key] = id;
                    }
                }
            }
            context.ActionArguments.Clear();
            foreach (var (key, value) in newArguments)
            {
                context.ActionArguments.Add(key, value);
            }
        }

        /// <summary>
        /// Provides the handler for the provided constraint.
        /// </summary>
        /// <param name="constraint">The JSON token representing the constraint.</param>
        /// <returns>The consumer delegate handler.</returns>
        public IConsumerDelegate<ActionExecutingContext> GetHandler(JToken constraint)
        {
            return this;
        }

        /// <summary>
        /// Extracts the ID from the given constraint based on a specified key.
        /// </summary>
        /// <param name="constraint">The JSON token representing the constraint.</param>
        /// <param name="key">The key to look for in the constraint.</param>
        /// <returns>The extracted ID as an integer.</returns>
        public int GetId(JToken constraint, string key)
        {
            if (constraint.Children().Any())
            {
                foreach (JToken item in constraint.Children())
                {
                    JEnumerable<JToken> itemProperties = item.Children<JToken>();

                    foreach (JToken itemProperty in itemProperties)
                    {
                        var property = item.ToObject<JProperty>();
                        var value = itemProperty.ToObject<string>();

                        if (value != null && key.Equals(property?.Name))
                        {
                            return Convert.ToInt32(value);
                        }
                    }
                }
            }
            return 0;
        }
    }
}
