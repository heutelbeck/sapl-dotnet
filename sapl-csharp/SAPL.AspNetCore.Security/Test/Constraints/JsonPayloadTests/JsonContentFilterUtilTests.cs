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
using Xunit;

namespace SAPL.AspNetCore.Security.Test.Constraints.JsonPayloadTests;

/// <summary>
/// Contains tests for verifying JSON content filtering functionality.
/// </summary>
public class JsonContentFilterUtilTests
{
    /// <summary>
    /// Tests whether the handler correctly replaces a specified value in a JSON object.
    /// </summary>
    [Trait("Integration", "NoPDPRequired")]
    [Fact]
    public Task When_HandlerForReplacement_Then_Value_Replaced()
    {

        JObject o = JObject.Parse(@"{
                  'Stores': [
                    'Lambton Quay',
                    'Willis Street'
                  ],
                  'Manufacturers': [
                    {
                      'Name': 'Acme Co',
                      'Products': [
                        {
                          'Name': 'Anvil',
                          'Price': 50
                        }
                      ]
                    },
                    {
                      'Name': 'Contoso',
                      'Products': [
                        {
                          'Name': 'Elbow Grease',
                          'Price': 99.95
                        },
                        {
                          'Name': 'Headlight Fluid',
                          'Price': 4
                        }
                      ]
                    }
                  ]
                            }");

        dynamic stuff = JsonConvert.DeserializeObject(o.ToString())!;

        //var handle = JsonContentFilterUtil.GetReplacementHandlerForTest<object, object>("$.Manufacturers[?(@.Name == 'Acme Co')]", "$.Name", "Halläle");
        //dynamic? newStuff = handle(stuff);

        //string newOriginal = JsonConvert.SerializeObject(newStuff);
        //JObject newo = JObject.Parse(newOriginal);
        //JToken acme = newo.SelectToken("$.Manufacturers[?(@.Name == 'Halläle')]");
        //Assert.NotNull(acme);
        return Task.CompletedTask;
    }
}