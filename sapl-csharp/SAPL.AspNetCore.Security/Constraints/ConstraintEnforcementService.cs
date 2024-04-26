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
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Constraints.Exceptions;
using SAPL.PDP.Api;

namespace SAPL.AspNetCore.Security.Constraints
{
    /// ///<summary>
    /// Manages the enforcement of security constraints based on decisions from the Policy Decision Point (PDP).
    /// It collects and executes handlers for constraints and advices in the context of an ASP.NET Core application.
    ///</summary>
    public class ConstraintEnforcementService : IConstraintEnforcementService
    {
        // Properties for various handler providers
        public IEnumerable<IErrorMappingConstraintHandlerProvider> GlobalErrorMappingHandlerProviders { get; private set; } = null!;
        public List<IErrorHandlerProvider> GlobalErrorHandlerProviders { get; private set; } = null!;

        [ImportMany]
        public IEnumerable<IRunnableDelegateConstraintHandlerProvider>? RunnableProviders { get; private set; }

        [ImportMany]
        public IEnumerable<IActionExecutingContextConstraintHandlerProvider>? ActionExecutingContextProviders { get; private set; }

        public List<IFilterPredicateConstraintHandlerProvider> FilterPredicateHandlers { get; private set; } = null!;

        public List<ITypedPredicateConstraintHandlerProvider> LambdaExpressionFilterPredicateHandlers { get; private set; } = null!;

        public List<ITypedContentFilteringProvider> FilterPredicateAndTransformationHandlers { get; private set; } = null!;

        public List<IJsonContentFilteringProvider> JsonContentFilteringAndTransformationHandlers { get; private set; } = null!;

        public IEnumerable<JToken>? UnhandledObligations
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes the service with a set of responsible handler providers.
        /// </summary>
        public ConstraintEnforcementService(IEnumerable<IResponsibleConstraintHandlerProvider?> responsibleHandlers)
        {
            ComposeConstraintHandlerProviders(responsibleHandlers!);
        }

        private void ComposeConstraintHandlerProviders(IEnumerable<IResponsibleConstraintHandlerProvider>? responsibleHandlers)
        {
            List<IRunnableDelegateConstraintHandlerProvider> runnableDelegates =
                new List<IRunnableDelegateConstraintHandlerProvider>();
            List<IConsumerDelegateConstraintHandlerProvider<string>> consumerDelegates =
                new List<IConsumerDelegateConstraintHandlerProvider<string>>();
            List<IActionExecutingContextConstraintHandlerProvider> actionExcecuters =
                new List<IActionExecutingContextConstraintHandlerProvider>();
            List<ITypedPredicateConstraintHandlerProvider> lambdaExpressionFilterPredicateHandlers =
                new List<ITypedPredicateConstraintHandlerProvider>();
            List<IFilterPredicateConstraintHandlerProvider> filterPredicateHandlers =
                new List<IFilterPredicateConstraintHandlerProvider>();
            List<ITypedContentFilteringProvider> filterPredicateTransformHandlers =
                new List<ITypedContentFilteringProvider>();
            List<IJsonContentFilteringProvider> jsonContentFilteringAndTransformationHandlers =
                new List<IJsonContentFilteringProvider>();
            List<IErrorMappingConstraintHandlerProvider> errorMappingHandlerProviders =
                new List<IErrorMappingConstraintHandlerProvider>();
            List<IErrorHandlerProvider> errorHandlerProviders =
                new List<IErrorHandlerProvider>();

            if (responsibleHandlers != null)
            {
                foreach (IResponsibleConstraintHandlerProvider provider in responsibleHandlers)
                {
                    if (provider is IRunnableDelegateConstraintHandlerProvider runnableDelegate)
                    {
                        runnableDelegates.Add(runnableDelegate);
                    }

                    if (provider is IConsumerDelegateConstraintHandlerProvider<string> consumerDelegate)
                    {
                        consumerDelegates.Add(consumerDelegate);
                    }

                    if (provider is IActionExecutingContextConstraintHandlerProvider actionExcecuter)
                    {
                        actionExcecuters.Add(actionExcecuter);
                    }

                    if (provider is ITypedPredicateConstraintHandlerProvider predicateConstraintHandler)
                    {
                        lambdaExpressionFilterPredicateHandlers.Add(predicateConstraintHandler);
                    }
                    if (provider is IFilterPredicateConstraintHandlerProvider filterPredicateConstraint)
                    {
                        filterPredicateHandlers.Add(filterPredicateConstraint);
                    }
                    if (provider is ITypedContentFilteringProvider filterPredicateTransformConstraint)
                    {
                        filterPredicateTransformHandlers.Add(filterPredicateTransformConstraint);
                    }
                    if (provider is IJsonContentFilteringProvider jsonContentFilteringProvider)
                    {
                        jsonContentFilteringAndTransformationHandlers.Add(jsonContentFilteringProvider);
                    }
                    if (provider is IErrorMappingConstraintHandlerProvider errorMappingConstraintHandlerProvider)
                    {
                        errorMappingHandlerProviders.Add(errorMappingConstraintHandlerProvider);
                    }
                    if (provider is IErrorHandlerProvider errorHandlerProvider)
                    {
                        errorHandlerProviders.Add(errorHandlerProvider);
                    }
                }
            }

            this.RunnableProviders = runnableDelegates;
            this.ActionExecutingContextProviders = actionExcecuters;
            this.FilterPredicateHandlers = filterPredicateHandlers;
            this.LambdaExpressionFilterPredicateHandlers = lambdaExpressionFilterPredicateHandlers;
            this.FilterPredicateAndTransformationHandlers = filterPredicateTransformHandlers;
            this.JsonContentFilteringAndTransformationHandlers = jsonContentFilteringAndTransformationHandlers;
            this.GlobalErrorMappingHandlerProviders = errorMappingHandlerProviders;
            this.GlobalErrorHandlerProviders = errorHandlerProviders;
            this.UnhandledObligations = new List<JToken>();
        }


        /// <summary>
        /// Constructs a bundle of constraint handlers based on the provided decision,
        /// to enforce security measures before accessing the endpoint.
        /// </summary>
        /// <param name="decision">The decision received from the PDP.</param>
        /// <returns>A BlockingPreEnforceConstraintHandlerBundle.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when a bundle cannot be constructed.</exception>
        public BlockingPreEnforceConstraintHandlerBundle? BlockingPreEnforceBundleFor(AuthorizationDecision decision)
        {
            if (decision.Obligations != null && decision.Obligations.Any())
            {
                UnhandledObligations = decision.Obligations;
            }
            else
            {
                UnhandledObligations = Enumerable.Empty<JToken>();
            }
            var bundle = new BlockingPreEnforceConstraintHandlerBundle(
                RunnableHandlersForSignal(ISignal.Signal.ON_DECISION, decision),
                ActionExecutingHandlers(decision));

            if (UnhandledObligations.Any())
            {
                throw new AccessDeniedException("No handler for obligation: " + UnhandledObligations);
            }
            return bundle;
        }

        /// <summary>
        /// Creates a post-enforcement constraint handler bundle for the given object type and instance,
        /// based on the provided authorization decision.
        /// </summary>
        /// <param name="objectType">The type of the object to enforce constraints on.</param>
        /// <param name="instance">The instance of the object.</param>
        /// <param name="decision">The decision from the PDP.</param>
        /// <returns>A blocking post-enforcement constraint handler bundle.</returns>
        public IBlockingPostEnforceConstraintHandlerBundle BlockingPostEnforceConstraintHandlerBundle(Type objectType, object? instance, AuthorizationDecision decision)
        {
            IBlockingPostEnforceConstraintHandlerBundle? bundle = null;
            IJsonContentFilteringProvider? jsonContentFilteringProvider = ConstraintApiUtil.CreaInstanceOfJsonContentFilteringProvider(objectType);
            if (objectType.IsArray || (objectType.IsGenericType && (objectType.GetGenericTypeDefinition() == typeof(List<>) || objectType.GetGenericTypeDefinition() == typeof(IEnumerable<>))))
            {
                var elementType = ConstraintApiUtil.GetAnyElementTypeOfGenericList(objectType);
                bundle = ConstraintApiUtil.CreaInstanceOfBlockingPostEnforcementbundle(objectType, elementType, instance);
            }
            else
            {
                bundle = ConstraintApiUtil.CreaInstanceOfBlockingPostEnforcementbundle(objectType, instance);
            }
            if (jsonContentFilteringProvider != null)
            {
                this.JsonContentFilteringAndTransformationHandlers.Add(jsonContentFilteringProvider);
            }
            if (decision.Obligations != null && decision.Obligations.Any())
            {
                UnhandledObligations = decision.Obligations;
            }
            else
            {
                UnhandledObligations = Enumerable.Empty<JToken>();
            }
            if (bundle == null)
            {
                throw new AccessDeniedException("No PostenforcementBundle for decision: " + decision);
            }

            bundle.ConstructHandlers(LambdaExpressionFilterHandlers(decision), AllFilterPredicateHandlers(decision),
                AllFilterPredicateAndTransformationHandlers(decision),
                AllJsonContentFilteringAndTransformationHandlers(decision), ConstructAllErrorHandlerProvider(decision),
                ConstructAllErrorMappingConstraintHandlerProvider(decision), RunnableHandlersForSignal(ISignal.Signal.ON_EXECUTION, decision));
            if (UnhandledObligations.Any())
            {
                throw new AccessDeniedException("No handler for obligation: " + UnhandledObligations);
            }
            return bundle;
        }

        #region constructing RunnableHandlers

        private List<IRunnableDelegate>? RunnableHandlersForSignal(ISignal.Signal signal, AuthorizationDecision decision)
        {
            var onDecisionObligationHandlers = ConstructRunnableHandlersForObligations(signal);

            var onDecisionAdviceHandlers = ConstructRunnableHandlersForAdvices(signal, decision.Advice);

            return onDecisionObligationHandlers.Concat(onDecisionAdviceHandlers).ToList();
        }

        private List<IRunnableDelegate> ConstructRunnableHandlersForObligations(ISignal.Signal signal)
        {
            var handlers = new List<IRunnableDelegate>();
            if (UnhandledObligations == null || !UnhandledObligations.Any())
            {
                return handlers;
            }

            var unhandledObligationsCopy = UnhandledObligations.ToList();
            var constraints = UnhandledObligations.ToList();
            foreach (JToken obligation in constraints)
            {
                if (RunnableProviders != null)
                {
                    var runnableForSignal = RunnableProviders.Where(p => p.GetSignal().Equals(signal) && p.IsResponsible(obligation)).ToList();

                    if (runnableForSignal.Any())
                    {
                        runnableForSignal.ForEach(r => handlers.Add(r.GetHandler(obligation)));
                        unhandledObligationsCopy.RemoveAt(constraints.IndexOf(obligation));
                    }
                }
            }
            this.UnhandledObligations = unhandledObligationsCopy;
            return handlers;
        }

        private List<IRunnableDelegate> ConstructRunnableHandlersForAdvices(ISignal.Signal signal, JArray? advices)
        {
            var handlers = new List<IRunnableDelegate>();

            if (advices == null || !advices.Any())
            {
                return handlers;
            }
            foreach (JToken advice in advices)
            {
                if (RunnableProviders != null)
                {
                    var runnableForSignal = RunnableProviders.Where(p => p.GetSignal().Equals(signal) && p.IsResponsible(advice)).ToList();

                    if (runnableForSignal.Any())
                    {
                        runnableForSignal.ForEach(r => handlers.Add(r.GetHandler(advice)));
                    }
                }
            }
            return handlers;
        }

        #endregion

        #region constructing ActionDescriptorHandlers

        private List<IConsumerDelegate<ActionExecutingContext>> ActionExecutingHandlers(AuthorizationDecision decision)
        {
            var obligations = this.UnhandledObligations;
            if (obligations != null)
            {
                var unhandledObligations = obligations.ToList();
            }

            List<IConsumerDelegate<ActionExecutingContext>> onDecisionAdviceHandlers = new List<IConsumerDelegate<ActionExecutingContext>>();
            List<IConsumerDelegate<ActionExecutingContext>> onDecisionObligationHandlers = new List<IConsumerDelegate<ActionExecutingContext>>();

            if ((UnhandledObligations ?? Array.Empty<JToken>()).Any())
            {
                onDecisionObligationHandlers = ConstructActionExecutingHandlersForObligations();
                onDecisionAdviceHandlers = ConstructActionDescriptorHandlersForAdvices(decision.Advice);
            }
            return onDecisionObligationHandlers.Concat(onDecisionAdviceHandlers).ToList();
        }

        private List<IConsumerDelegate<ActionExecutingContext>> ConstructActionDescriptorHandlersForAdvices(JArray? advices)
        {
            var handlers = new List<IConsumerDelegate<ActionExecutingContext>>();

            if (advices == null || !advices.Any())
            {
                return handlers;
            }

            advices.ToList().ForEach(advice =>
            {
                if (ActionExecutingContextProviders != null)
                    handlers.AddRange((ActionExecutingContextProviders
                        .Where(m => m.IsResponsible(advice))
                        .Select(a => a.GetHandler(advice))));
            });

            return handlers;
        }

        private List<IConsumerDelegate<ActionExecutingContext>> ConstructActionExecutingHandlersForObligations()
        {
            var handlers = new List<IConsumerDelegate<ActionExecutingContext>>();

            if (UnhandledObligations == null || !UnhandledObligations.Any() || ActionExecutingContextProviders == null)
            {
                return handlers;
            }
            var unhandledObligations = UnhandledObligations.ToList();
            var unhandledObligationsCopy = UnhandledObligations.ToList();
            unhandledObligations.ForEach(obligation =>
            {
                var found =
                ActionExecutingContextProviders
                    .Where(m => m.IsResponsible(obligation))
                    .Select(a => a.GetHandler(obligation)).ToList();
                if (found.Any())
                {
                    handlers.AddRange(found);
                    unhandledObligationsCopy.RemoveAt(unhandledObligations.IndexOf(obligation));
                }


            });
            this.UnhandledObligations = unhandledObligationsCopy;
            return handlers;
        }
        #endregion

        #region constructing FilterConstraintHandlers

        private List<ITypedPredicateConstraintHandlerProvider> ConstructLambdaExpressionFilterHandlersForConstraint()
        {
            var handlers = new List<ITypedPredicateConstraintHandlerProvider>();
            if (UnhandledObligations == null || !UnhandledObligations.Any())
            {
                return handlers;
            }

            var unhandledObligationsCopy = UnhandledObligations.ToList();
            var constraints = UnhandledObligations.ToList();
            foreach (JToken obligation in constraints)
            {
                if (LambdaExpressionFilterPredicateHandlers != null)
                {
                    var filterPredicates = LambdaExpressionFilterPredicateHandlers.Where(p => p.IsResponsible(obligation)).ToList();
                    //var runnableForSignal = RunnableProviders.Where(p => p.GetSignal().Equals(signal) && p.IsResponsible(obligation)).ToList();

                    if (filterPredicates.Any())
                    {
                        filterPredicates.ForEach(r => handlers.Add(r));
                        unhandledObligationsCopy.RemoveAt(constraints.IndexOf(obligation));
                    }
                }
            }
            this.UnhandledObligations = unhandledObligationsCopy;
            return handlers;
        }

        private List<ITypedPredicateConstraintHandlerProvider> ConstructLambdaExpressionFilterHandlersForAdvices(JArray? advices)
        {
            var handlers = new List<ITypedPredicateConstraintHandlerProvider>();

            if (advices == null || !advices.Any())
            {
                return handlers;
            }
            foreach (JToken advice in advices)
            {
                if (RunnableProviders != null)
                {
                    if (LambdaExpressionFilterPredicateHandlers != null)
                    {
                        var filterPredicates = LambdaExpressionFilterPredicateHandlers.Where(p => p.IsResponsible(advice)).ToList();
                        //var runnableForSignal = RunnableProviders.Where(p => p.GetSignal().Equals(signal) && p.IsResponsible(obligation)).ToList();

                        if (filterPredicates.Any())
                        {
                            filterPredicates.ForEach(r => handlers.Add(r));
                        }
                    }
                }
            }
            return handlers;
        }

        private List<ITypedPredicateConstraintHandlerProvider> LambdaExpressionFilterHandlers(AuthorizationDecision decision)
        {
            var onDecisionObligationHandlers = ConstructLambdaExpressionFilterHandlersForConstraint();

            var onDecisionAdviceHandlers = ConstructLambdaExpressionFilterHandlersForAdvices(decision.Advice);

            return onDecisionObligationHandlers.Concat(onDecisionAdviceHandlers).ToList();
        }


        private List<IFilterPredicateConstraintHandlerProvider> ConstructFilterHandlersForConstraint()
        {
            var handlers = new List<IFilterPredicateConstraintHandlerProvider>();
            if (UnhandledObligations == null || !UnhandledObligations.Any())
            {
                return handlers;
            }

            var unhandledObligationsCopy = UnhandledObligations.ToList();
            var constraints = UnhandledObligations.ToList();
            foreach (JToken obligation in constraints)
            {
                if (FilterPredicateHandlers != null)
                {
                    var filterPredicates = FilterPredicateHandlers.Where(p => p.IsResponsible(obligation)).ToList();
                    //var runnableForSignal = RunnableProviders.Where(p => p.GetSignal().Equals(signal) && p.IsResponsible(obligation)).ToList();

                    if (filterPredicates.Any())
                    {
                        filterPredicates.ForEach(r => handlers.Add(r));
                        unhandledObligationsCopy.RemoveAt(constraints.IndexOf(obligation));
                    }
                }
            }
            this.UnhandledObligations = unhandledObligationsCopy;
            return handlers;
        }

        private List<IFilterPredicateConstraintHandlerProvider> ConstructFilterHandlersForAdvices(JArray? advices)
        {
            var handlers = new List<IFilterPredicateConstraintHandlerProvider>();

            if (advices == null || !advices.Any())
            {
                return handlers;
            }
            foreach (JToken advice in advices)
            {
                if (RunnableProviders != null)
                {
                    if (LambdaExpressionFilterPredicateHandlers != null)
                    {
                        var filterPredicates = FilterPredicateHandlers.Where(p => p.IsResponsible(advice)).ToList();
                        //var runnableForSignal = RunnableProviders.Where(p => p.GetSignal().Equals(signal) && p.IsResponsible(obligation)).ToList();

                        if (filterPredicates.Any())
                        {
                            filterPredicates.ForEach(r => handlers.Add(r));
                        }
                    }
                }
            }
            return handlers;
        }

        private List<IFilterPredicateConstraintHandlerProvider> AllFilterPredicateHandlers(AuthorizationDecision decision)
        {
            var onDecisionObligationHandlers = ConstructFilterHandlersForConstraint();

            var onDecisionAdviceHandlers = ConstructFilterHandlersForAdvices(decision.Advice);

            return onDecisionObligationHandlers.Concat(onDecisionAdviceHandlers).ToList();
        }
        private List<ITypedContentFilteringProvider> ConstructFilterPredicateAndTransformationHandlersForConstraint()
        {
            var handlers = new List<ITypedContentFilteringProvider>();
            if (UnhandledObligations == null || !UnhandledObligations.Any())
            {
                return handlers;
            }

            var unhandledObligationsCopy = UnhandledObligations.ToList();
            var constraints = UnhandledObligations.ToList();
            foreach (JToken obligation in constraints)
            {
                if (FilterPredicateAndTransformationHandlers != null)
                {
                    var filterPredicates = FilterPredicateAndTransformationHandlers.Where(p => p.IsResponsible(obligation)).ToList();
                    if (filterPredicates.Any())
                    {
                        filterPredicates.ForEach(r => handlers.Add(r));
                        unhandledObligationsCopy.RemoveAt(constraints.IndexOf(obligation));
                    }
                }
            }
            this.UnhandledObligations = unhandledObligationsCopy;
            return handlers;
        }

        private List<ITypedContentFilteringProvider> ConstructFilterPredicateAndTransformationHandlersForAdvices(JArray? advices)
        {
            var handlers = new List<ITypedContentFilteringProvider>();

            if (advices == null || !advices.Any())
            {
                return handlers;
            }
            foreach (JToken advice in advices)
            {
                if (FilterPredicateAndTransformationHandlers != null)
                {
                    var filterPredicates = FilterPredicateAndTransformationHandlers.Where(p => p.IsResponsible(advice)).ToList();

                    if (filterPredicates.Any())
                    {
                        filterPredicates.ForEach(r => handlers.Add(r));
                    }
                }
            }
            return handlers;
        }

        private List<ITypedContentFilteringProvider> AllFilterPredicateAndTransformationHandlers(AuthorizationDecision decision)
        {
            var onDecisionObligationHandlers = ConstructFilterPredicateAndTransformationHandlersForConstraint();

            var onDecisionAdviceHandlers = ConstructFilterPredicateAndTransformationHandlersForAdvices(decision.Advice);

            return onDecisionObligationHandlers.Concat(onDecisionAdviceHandlers).ToList();
        }

        private List<IJsonContentFilteringProvider> ConstructJsonContentFilteringAndTransformationHandlersForConstraint()
        {
            var handlers = new List<IJsonContentFilteringProvider>();
            if (UnhandledObligations == null || !UnhandledObligations.Any())
            {
                return handlers;
            }

            var unhandledObligationsCopy = UnhandledObligations.ToList();
            var constraints = UnhandledObligations.ToList();
            foreach (JToken obligation in constraints)
            {
                if (JsonContentFilteringAndTransformationHandlers != null)
                {
                    var filterPredicates = JsonContentFilteringAndTransformationHandlers.Where(p => p.IsResponsible(obligation)).ToList();
                    if (filterPredicates.Any())
                    {
                        filterPredicates.ForEach(r => handlers.Add(r));
                        unhandledObligationsCopy.RemoveAt(constraints.IndexOf(obligation));
                    }
                }
            }
            this.UnhandledObligations = unhandledObligationsCopy;
            return handlers;
        }

        private List<IJsonContentFilteringProvider> ConstructJsonContentFilteringAndTransformationHandlersForAdvices(JArray? advices)
        {
            var handlers = new List<IJsonContentFilteringProvider>();

            if (advices == null || !advices.Any())
            {
                return handlers;
            }
            foreach (JToken advice in advices)
            {
                if (JsonContentFilteringAndTransformationHandlers != null)
                {
                    var filterPredicates = JsonContentFilteringAndTransformationHandlers.Where(p => p.IsResponsible(advice)).ToList();

                    if (filterPredicates.Any())
                    {
                        filterPredicates.ForEach(r => handlers.Add(r));
                    }
                }
            }
            return handlers;
        }

        private List<IJsonContentFilteringProvider> AllJsonContentFilteringAndTransformationHandlers(AuthorizationDecision decision)
        {
            var onDecisionObligationHandlers = ConstructJsonContentFilteringAndTransformationHandlersForConstraint();

            var onDecisionAdviceHandlers = ConstructJsonContentFilteringAndTransformationHandlersForAdvices(decision.Advice);

            return onDecisionObligationHandlers.Concat(onDecisionAdviceHandlers).ToList();
        }


        #endregion

        #region constructing IErrorMappingConstraintHandlerProvider

        private IEnumerable<Func<Exception, Exception>>? ConstructAllErrorMappingConstraintHandlerProvider(AuthorizationDecision decision)
        {
            var handler = ConstructErrorMappingConstraintHandlerProvider(decision);
            handler.Sort();
            List<Func<Exception, Exception>>? funcs = new List<Func<Exception, Exception>>();
            if (decision == null || decision.Obligations == null)
            {
                return funcs;
            }
            foreach (JToken obligation in decision.Obligations)
            {
                funcs.AddRange(handler.Select(h => h.GetHandler(obligation)));
            }
            return funcs;
        }

        private List<IErrorMappingConstraintHandlerProvider> ConstructErrorMappingConstraintHandlerProvider(AuthorizationDecision decision)
        {
            var onDecisionObligationHandlers = ConstructErrorMappingHandlersForObligations();

            var onDecisionAdviceHandlers = ConstructErrorMappingHandlersForAdvices(decision.Advice);

            return onDecisionObligationHandlers.Concat(onDecisionAdviceHandlers).ToList();
        }

        private List<IErrorMappingConstraintHandlerProvider> ConstructErrorMappingHandlersForObligations()
        {
            var handlers = new List<IErrorMappingConstraintHandlerProvider>();
            if (UnhandledObligations == null || !UnhandledObligations.Any())
            {
                return handlers;
            }

            var unhandledObligationsCopy = UnhandledObligations.ToList();
            var constraints = UnhandledObligations.ToList();
            foreach (JToken obligation in constraints)
            {
                if (GlobalErrorMappingHandlerProviders != null)
                {
                    var errorMappingConstraintHandlerProviders = GlobalErrorMappingHandlerProviders.Where(p => p.IsResponsible(obligation)).ToList();

                    if (errorMappingConstraintHandlerProviders.Any())
                    {
                        errorMappingConstraintHandlerProviders.ForEach(r => handlers.Add(r));
                        unhandledObligationsCopy.RemoveAt(constraints.IndexOf(obligation));
                    }
                }
            }
            this.UnhandledObligations = unhandledObligationsCopy;
            return handlers;
        }

        private List<IErrorMappingConstraintHandlerProvider> ConstructErrorMappingHandlersForAdvices(JArray? advices)
        {
            var handlers = new List<IErrorMappingConstraintHandlerProvider>();

            if (advices == null || !advices.Any())
            {
                return handlers;
            }
            foreach (JToken advice in advices)
            {
                if (GlobalErrorMappingHandlerProviders != null)
                {
                    var errorMappingConstraintHandlerProviders = GlobalErrorMappingHandlerProviders.Where(p => p.IsResponsible(advice)).ToList();

                    if (errorMappingConstraintHandlerProviders.Any())
                    {
                        errorMappingConstraintHandlerProviders.ForEach(r => handlers.Add(r));
                    }
                }
            }
            return handlers;
        }

        #endregion

        #region constructing IErrorHandlerProvider

        private IEnumerable<Action<Exception>>? ConstructAllErrorHandlerProvider(AuthorizationDecision decision)
        {
            var handler = ConstructErrorHandlerProviderProvider(decision);
            List<Action<Exception>>? actions = new List<Action<Exception>>();

            if (decision.Obligations == null)
            {
                return actions;
            }
            foreach (JToken obligation in decision.Obligations)
            {
                actions.AddRange(handler.Select(h => h.GetHandler(obligation)));
            }

            return actions;
        }

        private List<IErrorHandlerProvider> ConstructErrorHandlerProviderProvider(AuthorizationDecision decision)
        {
            var onDecisionObligationHandlers = ConstructErrorHandlerProviderForObligations();

            var onDecisionAdviceHandlers = ConstructErrorHandlerProviderForAdvices(decision.Advice);

            return onDecisionObligationHandlers.Concat(onDecisionAdviceHandlers).ToList();
        }

        private List<IErrorHandlerProvider> ConstructErrorHandlerProviderForObligations()
        {
            var handlers = new List<IErrorHandlerProvider>();
            if (UnhandledObligations == null || !UnhandledObligations.Any())
            {
                return handlers;
            }

            var unhandledObligationsCopy = UnhandledObligations.ToList();
            var constraints = UnhandledObligations.ToList();
            foreach (JToken obligation in constraints)
            {
                if (GlobalErrorHandlerProviders != null)
                {
                    var errorMappingConstraintHandlerProviders = GlobalErrorHandlerProviders.Where(p => p.IsResponsible(obligation)).ToList();

                    if (errorMappingConstraintHandlerProviders.Any())
                    {
                        errorMappingConstraintHandlerProviders.ForEach(r => handlers.Add(r));
                        unhandledObligationsCopy.RemoveAt(constraints.IndexOf(obligation));
                    }
                }
            }
            this.UnhandledObligations = unhandledObligationsCopy;
            return handlers;
        }

        private List<IErrorHandlerProvider> ConstructErrorHandlerProviderForAdvices(JArray? advices)
        {
            var handlers = new List<IErrorHandlerProvider>();

            if (advices == null || !advices.Any())
            {
                return handlers;
            }
            foreach (JToken advice in advices)
            {
                if (GlobalErrorHandlerProviders != null)
                {
                    var errorMappingConstraintHandlerProviders = GlobalErrorHandlerProviders.Where(p => p.IsResponsible(advice)).ToList();

                    if (errorMappingConstraintHandlerProviders.Any())
                    {
                        errorMappingConstraintHandlerProviders.ForEach(r => handlers.Add(r));
                    }
                }
            }
            return handlers;
        }

        #endregion

    }
}
