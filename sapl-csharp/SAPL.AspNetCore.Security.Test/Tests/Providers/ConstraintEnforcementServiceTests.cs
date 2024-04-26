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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;
using Newtonsoft.Json.Linq;
using NSubstitute;
using SAPL.AspNetCore.Security.Constraints;
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Constraints.Exceptions;
using SAPL.PDP.Api;
using Xunit;

namespace SAPL.AspNetCore.Security.Test.Tests.Providers
{
    /// <summary>
    /// Tests for constrainthandling with  mocked Providers
    /// </summary>
    public class ConstraintEnforcementServiceTests
    {
        private static JArray ONE_CONSTRAINT = new() { "one_constraint" };

        [Fact]
        public Task When_noConstraints_then_AccessIsGranted()
        {
            var service = new ConstraintEnforcementService(null!);
            var decision = AuthorizationDecision.PERMIT;
            var bundle = service.BlockingPreEnforceBundleFor(decision);

            //Act
            var exception = Record.Exception(() => bundle?.HandleOnDecisionConstraints());
            //Assert
            Assert.Null(exception);
            return Task.CompletedTask;
        }

        [Fact]
        public Task When_obligation_and_noHandler_then_Access_Denied_and_UnhandledObligations()
        {
            var service = new ConstraintEnforcementService(null!);
            var decision = AuthorizationDecision.PERMIT.WithObligations(ONE_CONSTRAINT);

            Assert.Throws<AccessDeniedException>(() =>
            {

                BlockingPreEnforceConstraintHandlerBundle? bundle = service.BlockingPreEnforceBundleFor(decision);
                bundle?.HandleOnDecisionConstraints();
            });
            Assert.True(service.UnhandledObligations != null && service.UnhandledObligations.Any());
            Assert.True(service.RunnableProviders == null || !service.RunnableProviders.Any());
            return Task.CompletedTask;
        }

        [Fact]
        public Task When_obligation_and_onDecisionHandlerIsResponsible_andSucceeds_then_No_UnhandledObligations()
        {
            var counter = 0;
            //var mockProvider = new Mock<IRunnableConstraintHandlerProvider>();

            //mockProvider.Setup(p => p.GetSignal()).Returns(ISignal.Signal.ON_DECISION);
            //mockProvider.Setup(p => p.IsResponsible(ONE_CONSTRAINT.ToList().First())).Returns(true);
            //var runnable = mockProvider.As<IRunnable>();
            //runnable.Setup(p => p.Run()).Returns(() => counter++);
            //mockProvider.Setup(p => p.GetHandler(ONE_CONSTRAINT.ToList().First())).Returns(runnable.Object);
            //IRunnableConstraintHandlerProvider provider = mockProvider.Object;

            IRunnableDelegateConstraintHandlerProvider provider = Substitute.For<IRunnableDelegateConstraintHandlerProvider>();
            provider.GetSignal().Returns(ISignal.Signal.ON_DECISION);
            provider.IsResponsible(ONE_CONSTRAINT.ToList().First()).Returns(true);
            provider.GetHandler(ONE_CONSTRAINT.ToList().First()).Returns(provider);
            provider.Run().Returns(() => counter++);

            Assert.True(provider.IsResponsible(ONE_CONSTRAINT.ToList().First()));

            var decision = AuthorizationDecision.PERMIT.WithObligations(ONE_CONSTRAINT);
            var service = new ConstraintEnforcementService(
                new List<IRunnableDelegateConstraintHandlerProvider>() { provider });

            Assert.True(service.RunnableProviders != null && service.RunnableProviders.Any());

            var exception = Record.Exception(() =>
            {
                BlockingPreEnforceConstraintHandlerBundle? bundle = service.BlockingPreEnforceBundleFor(decision);
                bundle?.HandleOnDecisionConstraints();
            });
            Assert.True(counter == 1);
            Assert.Null(exception);
            Assert.True(service.UnhandledObligations == null || !service.UnhandledObligations.Any());
            Assert.True(service.RunnableProviders != null && service.RunnableProviders.Count().Equals(1));
            return Task.CompletedTask;
        }


        [Fact]
        public Task when_parameter_and_ActionExcecutingConstraint_then_modified_response()
        {
            string parameterName = "id";
            int unmanipulatedValue = 1;
            int manipulatedValue = 2;
            IDictionary<string, object?> arguments = new Dictionary<string, object?>() { { parameterName, unmanipulatedValue } };

            IAsyncActionFilter actionFilter = Substitute.For<IAsyncActionFilter>();
            var actionContext = new ActionContext(
                Mock.Of<HttpContext>(),
                Mock.Of<RouteData>(),
                Mock.Of<ActionDescriptor>(),
                Mock.Of<ModelStateDictionary>()
            );

            var actionExecutingContext = new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                arguments,
                Mock.Of<Controller>()
            );


            IActionExecutingContextConstraintHandlerProvider provider = Substitute.For<IActionExecutingContextConstraintHandlerProvider>();
            provider.GetSignal().Returns(ISignal.Signal.ON_DECISION);
            provider.IsResponsible(ONE_CONSTRAINT.ToList().First()).Returns(true);
            provider.GetHandler(ONE_CONSTRAINT.ToList().First()).Returns(provider);
            provider.Accept().Returns(context =>
            {
                IDictionary<string, object?> originalArguments = context.ActionArguments;
                IDictionary<string, object?> newArguments = new Dictionary<string, object?>(originalArguments);

                foreach (KeyValuePair<string, object?> argument in newArguments)
                {
                    if (argument.Value is int value && argument.Key.Equals(parameterName))
                    {
                        if (value == unmanipulatedValue)
                        {
                            newArguments[argument.Key] = manipulatedValue;
                        }
                    }
                }
                context.ActionArguments.Clear();
                foreach (var (key, value) in newArguments)
                {
                    context.ActionArguments.Add(key, value);
                }

            });

            var decision = AuthorizationDecision.PERMIT.WithObligations(ONE_CONSTRAINT);
            var service = new ConstraintEnforcementService(new List<IActionExecutingContextConstraintHandlerProvider>() { provider });
            var exception = Record.Exception(() =>
            {
                BlockingPreEnforceConstraintHandlerBundle? bundle = service.BlockingPreEnforceBundleFor(decision);
                bundle?.HandleMethodInvocationHandlers(actionExecutingContext);
            });
            Assert.Null(exception);
            Assert.True(actionExecutingContext.ActionArguments[parameterName] is int value && value == manipulatedValue);
            return Task.CompletedTask;
        }

    }
}
