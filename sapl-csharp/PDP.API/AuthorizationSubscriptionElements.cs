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

using Newtonsoft.Json;

namespace SAPL.PDP.Api
{
    /// <summary>
    /// Data structure holding IDs for the elements of an <see cref="AuthorizationSubscription"/>
    /// SAPL authorization subscription.
    /// </summary>
    public class AuthorizationSubscriptionElements
    {

        [JsonProperty(Required = Required.Always)]
        public int? SubjectId { get; set; }

        [JsonProperty(Required = Required.Always)]
        public int? ActionId { get; set; }

        [JsonProperty(Required = Required.Always)]
        public int? ResourceId { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? EnvironmentId { get; set; }

        public AuthorizationSubscriptionElements()
        {

        }

        public AuthorizationSubscriptionElements(int? subjectId, int? actionId, int? resourceId, int? environmentId)
        {
            this.SubjectId = subjectId;
            this.ActionId = actionId;
            this.ResourceId = resourceId;
            this.EnvironmentId = environmentId;
        }

    }
}
