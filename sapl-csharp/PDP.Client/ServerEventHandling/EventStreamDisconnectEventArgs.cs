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
    /// Is Invoked when the connection has to be renewed
    /// </summary>
    class EventStreamDisconnectEventArgs : EventArgs
    {
        /// <summary>
        /// Reconnect delay requested by server
        /// </summary>
        public int ReconnectDelay { get; private set; }

        /// <summary>
        /// Exception why disconnected
        /// </summary>
        public Exception Exception { get; private set; }

        internal EventStreamDisconnectEventArgs(int reconnectDelay, Exception exception)
        {
            ReconnectDelay = reconnectDelay;
            Exception = exception;
        }
    }
}
