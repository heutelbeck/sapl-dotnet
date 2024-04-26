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

namespace SAPL.AspNetCore.Security.Constraints.Providers.Utils;

public class ContentFilterUtil
{
    internal static string DiscloseLeft = "discloseLeft"; // Specifies the action to disclose content from the left side
    internal static string DiscloseRight = "discloseRight"; // Specifies the action to disclose content from the right side
    internal static string Replacement = "replacement"; // Indicates replacement of content
    internal static string Replace = "replace"; // Specifies replacement of content
    internal static string Blacken = "blacken"; // Specifies blackening out of content
    public static string Delete = "delete"; // Indicates deletion of content
    internal static string Type = "type"; // Specifies the type of action
    internal static string Path = "path"; // Specifies the path of the content to be filtered
    internal static string Value = "value"; // Specifies the value of the action
    internal static string Conditions = "conditions"; // Specifies the conditions for applying the action
    internal static string Actions = "actions"; // Specifies the actions to be performed
    internal static string ReplacementNotTextual = "'replacement' of 'blacken' action is not textual."; // Error message for non-textual replacement
    internal static string BlackSquare = "\u2588"; // Unicode black square character
    internal static string ValueNotIntegerS = "An action's '%s' is not an integer."; // Error message for non-integer value in action
}
