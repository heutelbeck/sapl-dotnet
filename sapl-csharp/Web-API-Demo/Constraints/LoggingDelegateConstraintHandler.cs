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

using System.Diagnostics;
using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Constraints;
using SAPL.AspNetCore.Security.Constraints.api;

namespace Web_API_Demo.Constraints
{
    /// <summary>
    /// Provides a handler for logging related constraints, acting on specified signals.
    /// This class can handle different logging actions like logging access or informing an admin.
    /// </summary>
    public class LoggingDelegateConstraintHandler : IRunnableDelegateConstraintHandlerProvider
    {
        private ILoggerFactory loggerFactory;
        private ILogger logger;

        /// <summary>
        /// Constructor initializing the logger factory and logger.
        /// </summary>
        /// <param name="loggerFactory">Factory to create logger instances.</param>
        public LoggingDelegateConstraintHandler(ILoggerFactory loggerFactory)
        {
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger<LoggingDelegateConstraintHandler>();
        }

        // List of responsible items (logging actions) this handler can manage.
        private readonly List<ResponsibleItem> resposibleFor = new List<ResponsibleItem>()
        {
           new ResponsibleItem("logging","log_access",new List<ResponsibleItem>()),
           new ResponsibleItem("logging", "inform_admin", new List<ResponsibleItem>())

        };

        /// <summary>
        /// Provides a specific handler based on the constraint.
        /// </summary>
        /// <param name="constraint">The JSON token representing the constraint.</param>
        /// <returns>A runnable delegate handling the specific logging action.</returns>
        public IRunnableDelegate GetHandler(JToken constraint)
        {
            if (resposibleFor.ElementAt(1).IsMatch(constraint))
            {
                return new InformAdminConstraintHandler(loggerFactory);
            }
            return this;
        }


        /// <summary>
        /// Specifies the signal on which this handler operates.
        /// </summary>
        /// <returns>The signal for the handler.</returns>
        public ISignal.Signal GetSignal()
        {
            return ISignal.Signal.ON_DECISION;
        }

        /// <summary>
        /// Determines if this handler is responsible for the given constraint.
        /// </summary>
        /// <param name="constraint">The JSON token representing the constraint.</param>
        /// <returns>True if responsible, false otherwise.</returns>
        public bool IsResponsible(JToken constraint)
        {
            return resposibleFor.Any(r => r.IsMatch(constraint));
        }


        /// <summary>
        /// Returns the action delegate to be executed for logging.
        /// </summary>
        /// <returns>An action for logging.</returns>
        public Action Run()
        {
            return Log;
        }


        /// <summary>
        /// Logs a generic message indicating successful logging.
        /// </summary>
        private void Log()
        {
            if (logger != null)
            {
                logger.Log(LogLevel.Information, "Logged successfully");
            }
            else
            {
                Debug.WriteLine($"{nameof(LoggingDelegateConstraintHandler)} logged successfully");
            }
        }
    }

    /// <summary>
    /// Specific constraint handler for logging an 'inform admin' action.
    /// </summary>
    public class InformAdminConstraintHandler : IRunnableDelegate
    {
        private readonly ILogger logger;

        /// <summary>
        /// Initializes the handler with a logger.
        /// </summary>
        /// <param name="loggerFactory">Factory to create logger instances.</param>
        public InformAdminConstraintHandler(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<InformAdminConstraintHandler>(); ;
        }

        /// <summary>
        /// Provides the action to log an 'inform admin' message.
        /// </summary>
        /// <returns>An action for logging 'inform admin'.</returns>
        public Action Run()
        {
            return LogInformAdmin;
        }

        /// <summary>
        /// Logs a specific message for the 'inform admin' action.
        /// </summary>
        private void LogInformAdmin()
        {
            if (logger != null)
            {
                logger.Log(LogLevel.Information, "Logged 'Inform admin' successfully");
            }
            else
            {
                Debug.WriteLine($"{nameof(LoggingDelegateConstraintHandler)} logged 'Inform admin' successfully");
            }
        }
    }
}
