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

using System.Composition;
using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Constraints;
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.PDP.Api;
using Xunit;

namespace SAPL.AspNetCore.Security.Test.Tests.Providers
{
    /// <summary>
    /// Test for finding implemented Constrainthandler in given Assemblies an check for unhandled or handled Obligations
    /// </summary>
    public class ConstraintEnforcementServiceCompositionTests
    {
        private List<IResponsibleConstraintHandlerProvider>? responsibles;
        private List<IErrorHandlerProvider>? globalErrorHandlerProviders;

        public IEnumerable<IRunnableDelegateConstraintHandlerProvider>? RunnableProviders { get; private set; }

        public IEnumerable<IConsumerDelegateConstraintHandlerProvider<string>>? GlobalConsumerProviders { get; private set; }

        public List<IActionExecutingContextConstraintHandlerProvider>? ActionExecutingContextProviders
        {
            get;
            private set;
        }

        private void FindAllProvidersInTest()
        {
            globalErrorHandlerProviders = new List<IErrorHandlerProvider>();
            var util = new ConstraintHandlersComposeUtil();
            var assemliesToSearch = new List<Assembly>
            {
                Assembly.GetExecutingAssembly()
            };
            RunnableProviders = util.SearchRunnableConstraintHandlerProviders(assemliesToSearch);
            GlobalConsumerProviders = util.SearchIConsumerConstraintHandlerProviders(assemliesToSearch);
            ActionExecutingContextProviders =
                util.SearchIActionActionExecutingContextConstraintHandlerProvider(assemliesToSearch);
            responsibles = new List<IResponsibleConstraintHandlerProvider>();
            globalErrorHandlerProviders.ForEach(p => responsibles.Add(p));
            RunnableProviders.ToList()?.ForEach(p => responsibles.Add(p));
            GlobalConsumerProviders.ToList()?.ForEach(p => responsibles.Add(p));
            ActionExecutingContextProviders?.ForEach(p => responsibles.Add(p));
        }

        private ConstraintEnforcementService BuildConstraintEnforcementService()
        {
            FindAllProvidersInTest();

            return new ConstraintEnforcementService(responsibles!);
            //return new ConstraintEnforcementService(globalErrorHandlerProviders, RunnableProviders,
            //    GlobalConsumerProviders, ActionExecutingContextProviders);
        }


        [Fact]
        public Task Search()
        {
            var util = new ConstraintHandlersComposeUtil();
            var assemliesToSearch = Enumerable.Empty<Assembly>().ToList();
            var providers = util.SearchIConsumerConstraintHandlerProviders(assemliesToSearch);
            Assert.Equal(providers.Count(), 0);
            return Task.CompletedTask;
        }


        #region Tests for searching  and finding providers in given assembly

        [Fact]
        public Task When_ConsumerConstraintHandlerProvider_In_Assembly_then_Available()
        {
            var util = new ConstraintHandlersComposeUtil();
            var assemliesToSearch = new List<Assembly>
            {
                Assembly.GetExecutingAssembly()
            };
            var providers = util.SearchIConsumerConstraintHandlerProviders(assemliesToSearch);
            Assert.Equal(providers.Count(), 1);
            return Task.CompletedTask;
        }

        [Fact]
        public Task When_RunnableConstraintHandler_In_Assembly_then_Available()
        {
            var util = new ConstraintHandlersComposeUtil();
            var assemliesToSearch = new List<Assembly>
            {
                Assembly.GetExecutingAssembly()
            };
            var providers = util.SearchRunnableConstraintHandlerProviders(assemliesToSearch);
            Assert.Equal(providers.Count(), 1);
            return Task.CompletedTask;
        }

        [Fact]
        public Task When_ActionActionExecutingContextConstraintHandlerProvider_In_Assembly_then_Available()
        {
            var util = new ConstraintHandlersComposeUtil();
            var assemliesToSearch = new List<Assembly>
            {
                Assembly.GetExecutingAssembly()
            };
            var providers = util.SearchIActionActionExecutingContextConstraintHandlerProvider(assemliesToSearch);
            Assert.NotNull(providers);
            Assert.Equal(providers.Count(), 1);
            return Task.CompletedTask;
        }



        [Fact]
        public Task When_All_Providers_For_Test_Found_Other_Tests_Can_Be_Made()
        {
            var service = BuildConstraintEnforcementService();
            Assert.True(service.RunnableProviders != null
                         && service.ActionExecutingContextProviders != null
                         && service != null
                         && service.ActionExecutingContextProviders.Any()
                         && service.RunnableProviders.Any()
                         && (service.UnhandledObligations == null || !service.UnhandledObligations.Any()));
            return Task.CompletedTask;
        }


        #endregion


        [Fact]
        public Task When_noConstraints_then_No_Unhandled_Obligations_In_Assemblies()
        {
            var service = BuildConstraintEnforcementService();
            var decision = AuthorizationDecision.PERMIT;

            //Act
            var exception = Record.Exception(() =>
            {
                var bundle = service.BlockingPreEnforceBundleFor(decision);
                bundle?.HandleOnDecisionConstraints();
                Assert.True(service.UnhandledObligations == null || !service.UnhandledObligations.Any());
            });
            //Assert
            Assert.Null(exception);
            return Task.CompletedTask;
        }


        [Fact]
        public Task When_obligation_and_noHandler_then_UnhandledObligations_In_Assemblies()
        {
            var service = BuildConstraintEnforcementService();
            var constraint = new JArray { "constraint" };

            var decision = AuthorizationDecision.PERMIT.WithObligations(constraint);

            var exception = Record.Exception(() =>
            {
                var bundle = service.BlockingPreEnforceBundleFor(decision);
                bundle?.HandleOnDecisionConstraints();
                Assert.True(service.UnhandledObligations != null && service.UnhandledObligations.Any());
            });
            Assert.NotNull(exception);
            return Task.CompletedTask;
        }




        [Export(typeof(IRunnableDelegateConstraintHandlerProvider))]
        public class LoggingDelegateConstraintHandler : IRunnableDelegateConstraintHandlerProvider
        {


            private readonly List<ResponsibleItem> resposibleFor = new List<ResponsibleItem>()
            {
                new ResponsibleItem("logging", "log_access", new List<ResponsibleItem>()),
                new ResponsibleItem("logging", "inform_admin", new List<ResponsibleItem>())

            };


            public IRunnableDelegate GetHandler(JToken constraint)
            {
                if (resposibleFor.ElementAt(1).KeyValue.Equals(constraint.Value<string>()!))
                {
                    return new InformAdminConstraintHandler();
                }

                return this;
            }

            public ISignal.Signal GetSignal()
            {
                return ISignal.Signal.ON_DECISION;
            }

            public bool IsResponsible(JToken constraint)
            {
                return resposibleFor.Any(r => r.IsMatch(constraint));
            }

            public Action Run()
            {
                return Log;
            }

            private void Log()
            {
                Debug.WriteLine($"{nameof(LoggingDelegateConstraintHandler)} logged successfully");
            }
        }

        public class InformAdminConstraintHandler : IRunnableDelegate
        {
            public Action Run()
            {
                return LogInformAdmin;
            }

            private void LogInformAdmin()
            {
                Debug.WriteLine($"{nameof(LoggingDelegateConstraintHandler)} logged 'Inform admin' successfully");
            }
        }


        [Export(typeof(IActionExecutingContextConstraintHandlerProvider))]
        public class
            ManipulatePatientIdActionExecutingContextConstraintHandlerProvider :
                IActionExecutingContextConstraintHandlerProvider
        {
            private List<ResponsibleItem> resposibleFor = new()
                { new ResponsibleItem("manipulating", "changeId", new List<ResponsibleItem>()) };


            public ISignal.Signal GetSignal()
            {
                throw new NotImplementedException();
            }

            public bool IsResponsible(JToken constraint)
            {
                return resposibleFor.Any(r => r.IsMatch(constraint));
            }

            public Action<ActionExecutingContext> Accept()
            {
                return Manipulate;
            }

            private void Manipulate(ActionExecutingContext context)
            {
                IDictionary<string, object?> originalArguments = context.ActionArguments;
                IDictionary<string, object?> newArguments = new Dictionary<string, object?>(originalArguments);

                foreach (KeyValuePair<string, object?> argument in newArguments)
                {
                    if (argument.Value is int value)
                    {
                        if (value == 1)
                        {
                            newArguments[argument.Key] = 2;
                        }
                    }
                }

                context.ActionArguments.Clear();
                foreach (var (key, value) in newArguments)
                {
                    context.ActionArguments.Add(key, value);
                }
            }

            public IConsumerDelegate<ActionExecutingContext> GetHandler(JToken constraint)
            {
                return this;
            }
        }

        [Export(typeof(IConsumerDelegateConstraintHandlerProvider<string>))]
        public class LoggingConsumerDelegateConstraintHandler : IConsumerDelegateConstraintHandlerProvider<string>
        {
            public IConsumerDelegate<string> GetHandler(JToken constraint)
            {
                return this;
            }

            public ISignal.Signal GetSignal()
            {
                return ISignal.Signal.ON_DECISION;
            }

            public bool IsResponsible(JToken constraint)
            {
                return constraint.Value<string>()!.Equals("logging:inform_admin");
            }

            public Action<string> Accept()
            {
                return Log;
            }

            public void Log(string message)
            {
                Debug.WriteLine(message);
            }
        }
    }
}
