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

namespace SAPL.AspNetCore.Security.Filter.Metadata;

/// <summary>
/// Defines the contract for an attribute that enforces security policies before an action method is executed.
/// This interface extends ISaplAttribute for SAPL-based authorization and IAsyncActionFilter for asynchronous action filtering.
/// Implementing this interface allows for defining custom pre-execution logic in an ASP.NET Core application.
/// </summary>
public interface IPreEnforce : ISaplAttribute, IAsyncActionFilter
{
}
