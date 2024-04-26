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

using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Constraints.Exceptions;
using SAPL.AspNetCore.Security.Constraints.Providers.Utils;

namespace SAPL.AspNetCore.Security.Constraints
{
    /// <summary>
    /// this bundle aggregates all constraint handlers for a specific decision which
    /// are useful in a blocking PostEnforce scenario.
    /// for collection-types
    /// </summary>
    /// <typeparam name="TList"></typeparam>
    /// <typeparam name="TObject"></typeparam>
    public class BlockingPostEnforceConstraintHandlerBundleForCollection<TList, TObject> : BlockingPostEnforceConstraintHandlerBundleBase
                                                                            where TList : IEnumerable<TObject>
                                                                            where TObject : class?
    {
        public TList? ActionResultListValue { get; set; }

        public override object? GetResultValue()
        {
            return ActionResultListValue;
        }

        public override void SetResultValue(object? value)
        {
            this.ActionResultListValue = (TList)value!;
        }

        public TObject ActionResultObjectValue { get; set; } = null!;
        private List<IRunnableDelegate> OnDecisionHandlers { get; } = null!;
        private List<IConsumerDelegate<TObject>> ConsumerDelegatesHandlers { get; } = null!;
        private List<Predicate<TObject>>? FilterPredicateHandlers { get; set; }
        private List<List<(Predicate<TObject> predicate, List<(Action<TObject> action, string actionType)> actions)>>? FilterPredicateTransformHandlers
        {
            get;
            set;
        }

        private List<Func<TList, TList>>? JsonContentFilteringAndTransformationHandlers { get; set; }


        public BlockingPostEnforceConstraintHandlerBundleForCollection(ref TList? listValue, ref TObject objectValueValue, List<ITypedPredicateConstraintHandlerProvider> lambdaFilterPredicateHandlers,
            List<IFilterPredicateConstraintHandlerProvider> filterPredicateHandlers, List<ITypedContentFilteringProvider> filterPredicateTransformHandlers, List<IJsonContentFilteringProvider> jsonContentFilteringAndTransformationHandlers)
        {
            ConstructHandlers(lambdaFilterPredicateHandlers, filterPredicateHandlers, filterPredicateTransformHandlers, jsonContentFilteringAndTransformationHandlers);
            ActionResultListValue = listValue;
            ActionResultObjectValue = objectValueValue;
            ConsumerDelegatesHandlers = new List<IConsumerDelegate<TObject>>();
            OnDecisionHandlers = new List<IRunnableDelegate>();
        }

        public override void ConstructHandlers(List<ITypedPredicateConstraintHandlerProvider> lambdaFilterPredicateHandlers,
            List<IFilterPredicateConstraintHandlerProvider> filterPredicateHandlers,
            List<ITypedContentFilteringProvider> filterPredicateTransformHandlers,
            List<IJsonContentFilteringProvider> allJsonContentFilteringAndTransformationHandlers)
        {
            this.FilterPredicateHandlers = new List<Predicate<TObject>>();
            foreach (ITypedPredicateConstraintHandlerProvider nonGenericPredicateConstraintHandler in
                     lambdaFilterPredicateHandlers)
            {
                this.FilterPredicateHandlers.Add((Predicate<TObject>)nonGenericPredicateConstraintHandler.GetPredicate());
            }

            foreach (IFilterPredicateConstraintHandlerProvider nonGenericPredicateConstraintHandler in filterPredicateHandlers)
            {
                this.FilterPredicateHandlers.Add((Predicate<TObject>)nonGenericPredicateConstraintHandler.GetPredicate());
            }

            this.FilterPredicateTransformHandlers =
                new List<List<(Predicate<TObject> predicate, List<(Action<TObject> action, string actionType)> actions)>>();
            foreach (var handler in filterPredicateTransformHandlers)
            {
                this.FilterPredicateTransformHandlers.Add(
                    (List<(Predicate<TObject> predicate, List<(Action<TObject> action, string actionType)> actions)>)handler
                        .GetPredicatesAndActions());
            }

            this.JsonContentFilteringAndTransformationHandlers = new List<Func<TList, TList>>();
            foreach (var jsonContentFilteringAndTransformationHandler in allJsonContentFilteringAndTransformationHandlers)
            {
                if (jsonContentFilteringAndTransformationHandler.GetHandler() is Func<TList, TList> listHandler)
                {
                    this.JsonContentFilteringAndTransformationHandlers.Add(listHandler);
                }
            }
        }

        public BlockingPostEnforceConstraintHandlerBundleForCollection()
        {

        }

        public override void HandleAllConsumerDelegateConstraints()
        {
            try
            {
                if (ActionResultListValue != null)
                    foreach (TObject toConsume in ActionResultListValue)
                    {
                        ConsumerDelegatesHandlers.ForEach(a => a.Accept().Invoke(toConsume));
                    }
            }
            catch (Exception? e)
            {
                throw new AccessDeniedException(e);
            }
        }

        public override object? HandleAllConstraints()
        {
            TList? filteredCollection = ActionResultListValue;
            if (ActionResultListValue != null)
                filteredCollection = HandleFilterPredicateHandlers(filteredCollection ?? ActionResultListValue);
            if (filteredCollection != null)
            {
                filteredCollection = HandleFilterPredicateAndTransformationHandlersList(filteredCollection);
            }
            if (filteredCollection != null)
            {
                filteredCollection = HandleJsonListFilterPredicateAndTransformationHandlers(filteredCollection);
            }
            return filteredCollection;
        }


        /// <summary>
        /// Filter the List
        /// </summary>
        /// <returns></returns>
        private TList? HandleFilterPredicateHandlers(TList collection)
        {
            if (FilterPredicateHandlers != null)
                foreach (var predicateHandler in FilterPredicateHandlers)
                {
                    if (collection is List<TObject> list)
                    {
                        IEnumerable<TObject> result = list.Where(predicateHandler.Invoke).ToList();
                        return (TList?)result;
                    }

                    if (collection is IEnumerable<TObject> enumerable)
                    {
                        return (TList?)enumerable.Where(predicateHandler.Invoke);
                    }

                    if (collection is TObject[] array)
                    {
                        return (TList?)array.Where(predicateHandler.Invoke);
                    }
                }

            return collection;
        }

        public override object? HandleFilterPredicateAndTransformationHandlers()
        {
            return HandleFilterPredicateAndTransformationHandlersList(ActionResultListValue);
        }

        /// <summary>
        /// Handler are custom implemented
        /// Filter the list and excecute transformations
        /// </summary>
        /// <returns></returns>
        private TList? HandleFilterPredicateAndTransformationHandlersList(TList? collection)
        {
            if (FilterPredicateTransformHandlers != null)
                foreach (var handler in FilterPredicateTransformHandlers)
                {
                    foreach (var predicateHandlerHolder in handler)
                    {
                        if (collection is List<TObject> list)
                        {
                            if (predicateHandlerHolder.actions.Any())
                            {
                                foreach ((Action<TObject> action, string actionType) action in predicateHandlerHolder
                                             .actions)
                                {
                                    if (action.actionType.Equals(ContentFilterUtil.Delete))
                                    {
                                        list.RemoveAll(predicateHandlerHolder.predicate);
                                    }
                                    else
                                    {
                                        foreach (var o in list.Where(predicateHandlerHolder.predicate.Invoke))
                                        {
                                            action.action.Invoke(o);
                                        }
                                    }
                                }

                                return (TList?)collection;
                            }

                            IEnumerable<TObject> result = list.Where(predicateHandlerHolder.predicate.Invoke).ToList();
                            return (TList?)result;
                        }

                        if (collection is IEnumerable<TObject> enumerable)
                        {
                            return (TList?)enumerable.Where(predicateHandlerHolder.predicate.Invoke);
                        }

                        if (collection is TObject[] array)
                        {
                            return (TList?)array.Where(predicateHandlerHolder.predicate.Invoke);
                        }
                    }
                }


            return collection;
        }

        public override object? HandleJsonFilterPredicateAndTransformationHandlers()
        {
            return HandleJsonListFilterPredicateAndTransformationHandlers(ActionResultListValue);
        }

        /// <summary>
        /// Handler are automatically constructed from Obligation
        /// Filter the list and excecute transformations
        /// </summary>
        /// <returns></returns>
        private TList? HandleJsonListFilterPredicateAndTransformationHandlers(TList? collection)
        {
            if (collection != null)
            {
                TList result = collection;
                if (JsonContentFilteringAndTransformationHandlers != null)
                    foreach (var handler in JsonContentFilteringAndTransformationHandlers)
                    {
                        result = handler.Invoke(collection);
                    }

                return result;
            }
            return collection;
        }
    }
}
