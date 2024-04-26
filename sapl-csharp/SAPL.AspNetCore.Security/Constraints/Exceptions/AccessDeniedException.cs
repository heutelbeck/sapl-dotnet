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

namespace SAPL.AspNetCore.Security.Constraints.Exceptions
{
    /// <summary>
    /// Represents an exception thrown when access to a resource is denied.
    /// </summary>
    public class AccessDeniedException : Exception
    {
        /// <summary>
        /// Gets the detailed message describing the access denial.
        /// </summary>
        public string DetailMessage { get; }

        /// <summary>
        /// Gets the default message for access denial.
        /// </summary>
        public string DefaultMessage => "Access denied.";

        /// <summary>
        /// Gets the internal message associated with the access denial.
        /// </summary>
        private string InternalMessage { get; } = null!;

        /// <summary>
        /// Gets the internal exception that led to the access denial.
        /// </summary>
        public Exception? InternalException { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessDeniedException"/> class
        /// with the specified details.
        /// </summary>
        /// <param name="details">The details describing the access denial.</param>
        public AccessDeniedException(string details)
        {
            DetailMessage = $"{DefaultMessage} {details}";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessDeniedException"/> class
        /// with the default access denial message.
        /// </summary>
        public AccessDeniedException()
        {
            DetailMessage = DefaultMessage;
            InternalException = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessDeniedException"/> class
        /// with the specified internal exception.
        /// </summary>
        /// <param name="e">The internal exception associated with the access denial.</param>
        public AccessDeniedException(Exception? e)
        {
            DetailMessage = DefaultMessage;
            InternalException = e;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessDeniedException"/> class
        /// with the specified internal exception and details.
        /// </summary>
        /// <param name="e">The internal exception associated with the access denial.</param>
        /// <param name="details">The details describing the access denial.</param>
        public AccessDeniedException(Exception? e, string details)
        {
            DetailMessage = DefaultMessage;
            InternalMessage = details;
            InternalException = e;
        }
    }
}
