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
using Newtonsoft.Json.Linq;

namespace SAPL.PDP.Api
{
    /// <summary>
    /// The authorization subscription object defines the tuple of objects constituting a SAPL
    ///  authorization subscription. Each authorization subscription consists of:
    ///  <para>- a subject describing the entity which is requesting permission</para>
    ///  <para>- an action describing for which activity the subject is requesting permission</para>
    ///  <para>- a resource describing or even containing the resource for which the subject is
    ///  requesting the permission to execute the action</para>
    ///  <para>- an environment object describing additional contextual information from the
    ///  environment which may be required for evaluating the underlying policies.</para>	
    /// </summary>
    public class AuthorizationSubscription : IEquatable<AuthorizationSubscription>
    {

        //private static final ObjectMapper MAPPER = new ObjectMapper().registerModule(new Jdk8Module());
        [JsonProperty(Required = Required.Always, NullValueHandling = NullValueHandling.Ignore)]
        private JToken subject = null!;
        [JsonIgnore]
        public JToken Subject { get => subject; set { if (value != subject) subject = value; } }

        [JsonProperty(Required = Required.Always, NullValueHandling = NullValueHandling.Ignore)]
        private JToken action = null!;
        [JsonIgnore]
        public JToken Action { get => action; set { if (value != action) action = value; } }

        [JsonProperty(Required = Required.Always, NullValueHandling = NullValueHandling.Ignore)]
        private JToken resource = null!;
        [JsonIgnore]
        public JToken Resource { get => resource; set { if (value != resource) resource = value; } }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        private JToken environment = null!;
        [JsonIgnore]
        public JToken Environment { get => environment; set { if (value != environment) environment = value; } }

        public AuthorizationSubscription()
        {

        }

        public AuthorizationSubscription(JToken subject, JToken action, JToken resource, JToken environment)
        {
            this.Subject = subject;
            this.Action = action;
            this.Resource = resource;
            this.Environment = environment;
        }

        /// <summary>
        /// Creates an AuthorizationSubscription, containing the supplied objects marshaled to
        /// JSON by a default ObjectMapper with Jdk8Module registered. Environment will be
        /// omitted.
        /// @param subject an object describing the subject.
        /// @param action an object describing the action.
        /// @param resource an object describing the resource.
        /// @return an AuthorizationSubscription for subscribing to a SAPL PDP
        /// </summary>
        public static AuthorizationSubscription Of(Object subject, Object action, Object resource)
        {
            return Of(subject, action, resource, null!);
        }


        /// <summary>
        /// Creates an AuthorizationSubscription, containing the supplied objects marshaled the
        /// supplied ObjectMapper.
        /// @param subject an object describing the subject.
        /// @param action an object describing the action.
        /// @param resource an object describing the resource.
        /// @param environment an object describing the environment.
        /// @param mapper the ObjectMapper to be used for marshaling.
        /// @return an AuthorizationSubscription for subscribing to a SAPL PDP
        /// </summary>

        public static AuthorizationSubscription Of(Object subject, Object action, Object resource, Object environment)
        {
            //return new AuthorizationSubscription(mapper.valueToTree(subject), mapper.valueToTree(action),
            //		mapper.valueToTree(resource), environment == null ? null : mapper.valueToTree(environment));
            //JValue? e = null;

            return new AuthorizationSubscription(JToken.FromObject(subject), JToken.FromObject(action),
                    JToken.FromObject(resource), (environment == null ? null : JToken.FromObject(environment))!);
        }

        public bool Equals(AuthorizationSubscription other)
        {

            bool ist = JToken.DeepEquals(other.Subject, Subject) &&
                   JToken.DeepEquals(other.Action, Action) &&
                   JToken.DeepEquals(other.Resource, Resource) &&
                   JToken.DeepEquals(other.Environment, Environment);
            return ist;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(subject, Subject, action, Action, resource, Resource, environment, Environment);
        }
    }
}

