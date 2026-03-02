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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SAPL.PDP.Api
{
    ///<summary>
    /// Container for a decision
    ///</summary>
    public class AuthorizationDecision : INotifyDecisionChanged, IEquatable<AuthorizationDecision>
    {
        /// <summary>
        /// A simple PERMIT decision.
        /// </summary>
        public static readonly AuthorizationDecision PERMIT = new AuthorizationDecision(Decision.PERMIT);

        /// <summary>
        /// A simple DENY decision.
        /// </summary>
        public static readonly AuthorizationDecision DENY = new AuthorizationDecision(Decision.DENY);

        /// <summary>
        /// A simple INDETERMINATE decision.
        /// </summary>
        public static readonly AuthorizationDecision INDETERMINATE = new AuthorizationDecision(Decision.INDETERMINATE);

        /// <summary>
        /// A simple NOT_APPLICABLE decision.
        /// </summary>
        public static readonly AuthorizationDecision NOT_APPLICABLE = new AuthorizationDecision(Decision.NOT_APPLICABLE);



        Decision decision;
        public Decision Decision
        {
            get => decision;
            set
            {

                if (decision != value)
                {
                    decision = value;
                    OnPropertyChanged();
                }
            }
        }
        public string DecisionString { get => decision.ToString(); }

        //todo:
        //- C#-Alternative für Optional<T> finden (evtl nullable?)
#nullable enable
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        JObject? resource;
        public JObject? Resource { get => resource; private set { } }
        //Optional<JsonNode> resource = Optional.empty();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        JArray? obligations;
        public JArray? Obligations { get => obligations; private set { } }
        //Optional<ArrayNode> obligations = Optional.empty();

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        JArray? advice;
        public JArray? Advice { get => advice; private set { } }
        //Optional<ArrayNode> advice = Optional.empty();
#nullable disable

        public AuthorizationDecision() { }

        public AuthorizationDecision(Decision decision)
        {
            this.decision = decision;
            OnPropertyChanged(nameof(Decision));
            OnPropertyChanged(nameof(DecisionString));
        }


        /// <summary>
        /// @param decision Creates an immutable authorization decision with 'decision'
        /// as value, and without any resource, advice, or obligations.
        /// Must not be null.
        /// </summary>
#nullable enable
        public AuthorizationDecision(Decision decision, JObject? resource = null, JArray? obligations = null, JArray? advice = null)
        {
            this.decision = decision;
            this.resource = resource;
            this.obligations = obligations;
            this.advice = advice;
        }
#nullable disable
        /// <summary>
        /// @param newObligations a JSON array containing obligations.
        /// @return new immutable decision object, replacing the obligations of the
        /// original object with newObligations.If the array is empty, no
        /// obligations will be present, not even an empty array.
        ///
        /// </summary>
        public AuthorizationDecision WithObligations(JArray newObligations)
        {
            //List<JArray> newOb = new List<JArray>();

            //if (newObligations != null) newOb.Add(newObligations); 

            return new AuthorizationDecision(decision, resource,
                    newObligations, advice);
        }

        /// <summary>
        /// @param newAdvice a JSON array containing advice.
        /// @return new immutable decision object, replacing the advice of the original
        ///         object with newAdvice. If the array is empty, no advice will be
        ///         present, not even an empty array.
        /// </summary>
        public AuthorizationDecision WithAdvice(JArray newAdvice)
        {
            //List<JArray> newAd = new List<JArray>();

            //if (newAdvice != null) newAd.Add(newAdvice);

            return new AuthorizationDecision(decision, resource, obligations,
                    newAdvice);
        }

        /// <summary>
        /// @param newResource a JSON object, must nor be null.
        /// @return new immutable decision object, replacing the resource with
        ///         newResource.
        /// </summary>
        public AuthorizationDecision WithResource(JObject newResource)
        {
            //List<JObject> newRes = new List<JObject>();

            //if (newResource != null) newRes.Add(newResource);

            return new AuthorizationDecision(decision, newResource, obligations, advice);
        }

        /// <summary>
        /// @param newDecision a Decision value.
        /// @return new immutable decision object, replacing the resource with
        ///         newResource.
        /// </summary>
        public AuthorizationDecision WithDecision(Decision newDecision)
        {
            return new AuthorizationDecision(newDecision, resource, obligations, advice);
        }

        public bool HasResourceReplacement
        {
            get
            {
                return Resource != null;
            }
        }

        #region implements INotifyPropertyChanged
#nullable enable
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
#nullable disable
        #endregion


        public bool Equals(AuthorizationDecision other)
        {
            bool ist = JToken.DeepEquals(other!.Resource, Resource) &&
                       JToken.DeepEquals(other.Advice, Advice) &&
                       JToken.DeepEquals(other.Obligations, Obligations);
            return ist;
        }
    }
}
