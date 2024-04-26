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

using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Subscription;

namespace SAPL.AspNetCore.Security.Constraints.Providers.Utils;

/// <summary>
/// Utility class for filtering JSON content based on constraints.
/// </summary>
public static class JsonContentFilterUtil
{

    /// <summary>
    /// Gets the handler for filtering JSON content.
    /// </summary>
    /// <typeparam name="T">The type of the object to filter.</typeparam>
    /// <typeparam name="TU">The type of the filtered object.</typeparam>
    /// <param name="constraint">The constraint for filtering.</param>
    /// <returns>The handler for filtering JSON content.</returns>
    public static Func<T, TU?> GetHandler<T, TU>(JToken constraint) where TU : class
    {
        return x =>
        {
            string original = JsonConvert.SerializeObject(x);
            JObject originalJsonElement = null!;
            JArray originalJsonArray = null!;
            try
            {
                originalJsonElement = JObject.Parse(original);

            }
            catch (Exception)
            {
                try
                {
                    originalJsonArray = JArray.Parse(original);
                }
                catch (Exception ec)
                {
                    throw new JsonSerializationException(ec.Message);
                }
            }


            var conditionToken = ObligationContentReaderUtil.GetProperty(constraint, ContentFilterUtil.Conditions);
            JArray? conditions = (JArray)conditionToken!;
            foreach (var condition in conditions)
            {
                var path = ObligationContentReaderUtil.GetPropertyValue(condition, ContentFilterUtil.Path);
                var type = ObligationContentReaderUtil.GetPropertyValue(condition, ContentFilterUtil.Type);
                var value = ObligationContentReaderUtil.GetPropertyValue(condition, ContentFilterUtil.Value);

                JToken filteredToken;

                var actionToken = ObligationContentReaderUtil.GetProperty(condition, ContentFilterUtil.Actions);
                JArray? actions = (JArray)actionToken!; //Actions
                if (originalJsonElement != null)
                {
                    filteredToken = originalJsonElement.SelectToken(path!)!;
                    foreach (var action in actions)
                    {
                        ApplyAction<T, TU>(action, filteredToken);
                    }
                }
                else
                {


                    try
                    {
                        filteredToken = originalJsonArray.SelectToken(path!)!;
                        foreach (var action in actions)
                        {
                            ApplyAction<T, TU>(action, filteredToken);
                        }
                    }
                    catch (Exception)
                    {
                        IEnumerable<JToken> filteredTokens = originalJsonArray.SelectTokens(path!);
                        foreach (JToken jToken in filteredTokens)
                        {
                            foreach (var action in actions)
                            {
                                ApplyAction<T, TU>(action, jToken);
                            }
                        }
                    }
                }
            }
            if (originalJsonElement != null)
            {
                return JsonConvert.DeserializeObject<TU>(originalJsonElement.ToString());
            }

            return JsonConvert.DeserializeObject<TU>(originalJsonArray.ToString());
        };
    }

    private static void ApplyAction<T, TU>(JToken action, JToken? filtered) where TU : class
    {
        var actionPath = ObligationContentReaderUtil.GetPropertyValue(action, ContentFilterUtil.Path);
        var actionType = ObligationContentReaderUtil.GetPropertyValue(action, ContentFilterUtil.Type);
        var replaceValue = ObligationContentReaderUtil.GetPropertyValue(action, ContentFilterUtil.Replacement);

        if (ContentFilterUtil.Replace.Equals(actionType))
        {
            ReplaceToken<T, TU>(filtered, action);
            return;
        }

        if (ContentFilterUtil.Blacken.Equals(actionType))
        {
            Blacken<T, TU>(action, filtered);
        }

        if (ContentFilterUtil.Delete.Equals(actionType))
        {
            if (!string.IsNullOrEmpty(actionPath))
            {
                var tokenToReplace = filtered?.SelectToken(actionPath);
                if (tokenToReplace != null)
                {
                    tokenToReplace.Remove();
                }
            }
            else
            {
                filtered?.Remove();
            }
        }
    }

    private static void Blacken<T, TU>(JToken action, JToken? filtered)
        where TU : class
    {
        var actionPath = ObligationContentReaderUtil.GetPropertyValue(action, ContentFilterUtil.Path);
        var replacementString = DetermineReplacementString(action);
        var discloseRight = GetIntegerValueOfActionKeyOrDefaultToZero(action, ContentFilterUtil.DiscloseRight);
        var discloseLeft = GetIntegerValueOfActionKeyOrDefaultToZero(action, ContentFilterUtil.DiscloseLeft);
        var tokenToReplace = filtered?.SelectToken(actionPath!);
        string valueToReplace = string.Empty;
        if (tokenToReplace != null)
        {
            valueToReplace = tokenToReplace.Value<string>()!;
        }

        BlackenText(valueToReplace, replacementString, discloseRight, discloseLeft);
        ReplaceToken<T, TU>(filtered, action);
    }

    private static void ReplaceToken<T, TU>(JToken? filtered, JToken action) where TU : class
    {
        var actionPath = ObligationContentReaderUtil.GetPropertyValue(action, ContentFilterUtil.Path);
        var replaceValue = ObligationContentReaderUtil.GetPropertyValue(action, ContentFilterUtil.Replacement);

        var tokenToReplace = filtered?.SelectToken(actionPath!);
        JTokenWriter writer = new JTokenWriter();
        tokenToReplace?.WriteTo(writer);
        writer.WriteValue(replaceValue);
        var newToken = writer.Token;
        if (tokenToReplace != null && newToken != null)
        {
            tokenToReplace.Replace(newToken);
        }
    }

    private static int GetIntegerValueOfActionKeyOrDefaultToZero(JToken action, string key)
    {
        var value = ObligationContentReaderUtil.GetPropertyValue(action, key);
        if (value == null)
        {
            return 0;
        }

        if (int.TryParse(value, out int numValue))
        {
            return numValue;
        }

        throw new ArgumentException(ContentFilterUtil.ValueNotIntegerS);
    }

    private static string DetermineReplacementString(JToken action)
    {
        var replacementNode = ObligationContentReaderUtil.GetPropertyValue(action, ContentFilterUtil.Replacement);

        if (replacementNode == null)
            return ContentFilterUtil.BlackSquare;

        if (!string.IsNullOrEmpty(replacementNode))
            return replacementNode;

        throw new ArgumentException(ContentFilterUtil.ReplacementNotTextual);
    }

    private static string BlackenText(string originalString, string? replacement, int discloseRight, int discloseLeft)
    {
        if (string.IsNullOrEmpty(originalString))
        {
            return originalString;
        }
        if (discloseLeft + discloseRight >= originalString.Length)
            return originalString;

        var result = new StringBuilder();
        if (discloseLeft > 0)
            result.Append(originalString, 0, discloseLeft);

        var numberOfReplacedChars = originalString.Length - discloseLeft - discloseRight;
        for (int i = 0; i < numberOfReplacedChars; i++)
        {
            result.Append(replacement);
        }

        if (discloseRight > 0)
            result.Append(originalString.Substring(discloseLeft + numberOfReplacedChars));

        return result.ToString();
    }
}