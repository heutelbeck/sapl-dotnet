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
using System;

namespace csharp.sapl.constraint.ServerEventHandling
{
    /// <summary>
    /// Passes through the response from the server
    /// </summary>
    class EventStreamMessageEventArgs : EventArgs
    {
        /// <summary>
        /// ID of the event, empty string if not present
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Event type, empty string if not present
        /// </summary>
        public string Event { get; private set; }

        /// <summary>
        /// Response from Server
        /// </summary>
        public string Message { get; private set; }

        internal EventStreamMessageEventArgs(string data, string type, string id)
        {
            Message = data;
            Event = type;
            Id = id;
        }
    }
}
