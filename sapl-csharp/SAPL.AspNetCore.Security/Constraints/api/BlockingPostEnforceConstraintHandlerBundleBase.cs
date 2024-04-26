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

namespace SAPL.AspNetCore.Security.Constraints.api
{
    /// <summary>
    /// Provides a base implementation for handling post-enforcement constraints, 
    /// with capabilities to manage error handling and transformation.
    /// </summary>
    public abstract class BlockingPostEnforceConstraintHandlerBundleBase : IBlockingPostEnforceConstraintHandlerBundle
    {
        protected IEnumerable<Action<Exception>>? ErrorHandlers;
        protected IEnumerable<Func<Exception, Exception>>? ErrorMappingHandlers;
        protected List<IRunnableDelegate>? RunnableHandlers;
        object? IBlockingPostEnforceConstraintHandlerBundle.HandleFilterPredicateAndTransformationHandlers()
        {
            return HandleFilterPredicateAndTransformationHandlers();
        }

        object? IBlockingPostEnforceConstraintHandlerBundle.HandleAllConstraints()
        {
            return HandleAllConstraints();
        }

        object? IBlockingPostEnforceConstraintHandlerBundle.HandleJsonFilterPredicateAndTransformationHandlers()
        {
            return HandleJsonFilterPredicateAndTransformationHandlers();
        }

        // Constructs and initializes handlers for various constraint types
        void IBlockingPostEnforceConstraintHandlerBundle.ConstructHandlers(
            List<ITypedPredicateConstraintHandlerProvider> lambdaFilterPredicateHandlers,
            List<IFilterPredicateConstraintHandlerProvider> filterPredicateHandlers,
            List<ITypedContentFilteringProvider> filterPredicateTransformHandlers,
            List<IJsonContentFilteringProvider> allJsonContentFilteringAndTransformationHandlers,
            IEnumerable<Action<Exception>>? errorHandlers,
            IEnumerable<Func<Exception, Exception>>? errorMappingHandlers,
            List<IRunnableDelegate>? runnableHandlers)
        {
            this.ErrorHandlers = errorHandlers;
            this.ErrorMappingHandlers = errorMappingHandlers;
            this.RunnableHandlers = runnableHandlers;
            ConstructHandlers(lambdaFilterPredicateHandlers, filterPredicateHandlers, filterPredicateTransformHandlers,
                allJsonContentFilteringAndTransformationHandlers);
        }

        public object? ResultValue
        {
            get => GetResultValue();
            set => SetResultValue(value);
        }

        // Abstract methods for handling constraints and setting result value
        public abstract object? GetResultValue();
        public abstract void SetResultValue(object? value);

        public abstract object? HandleFilterPredicateAndTransformationHandlers();
        public abstract object? HandleAllConstraints();
        public abstract object? HandleJsonFilterPredicateAndTransformationHandlers();
        public abstract void HandleAllConsumerDelegateConstraints();

        public abstract void ConstructHandlers(
            List<ITypedPredicateConstraintHandlerProvider> lambdaFilterPredicateHandlers,
            List<IFilterPredicateConstraintHandlerProvider> filterPredicateHandlers,
            List<ITypedContentFilteringProvider> filterPredicateTransformHandlers,
            List<IJsonContentFilteringProvider> allJsonContentFilteringAndTransformationHandlers);

        // Handles error mapping for constraints
        protected Exception? HandleOnErrorMapConstraints(Exception exception)
        {
            Exception current = null!;
            if (ErrorMappingHandlers != null)
                for (int i = 0; i < ErrorMappingHandlers.Count(); i++)
                {
                    Exception previous = null!;
                    if (i == 0)
                    {
                        previous = exception;
                    }
                    else
                    {
                        if (current != null)
                        {
                            previous = current;
                        }
                    }

                    if (previous != null)
                    {
                        current = ErrorMappingHandlers.ElementAt(i).Invoke(previous);
                    }
                }

            return current;
        }
        // Executes error handlers
        protected void HandleOnErrorConstraints(Exception exception)
        {
            if (ErrorHandlers != null)
            {
                foreach (Action<Exception> errorHandler in ErrorHandlers)
                {
                    errorHandler.Invoke(exception);
                }
            }
        }
        // Handles all error constraints and returns any transformed exceptions
        public Exception? HandleAllOnErrorConstraints(Exception exception)
        {
            HandleOnErrorConstraints(exception);
            return HandleOnErrorMapConstraints(exception);
        }


        // Executes all runnable handlers for on-decision constraints
        public void HandleOnDecisionConstraints()
        {
            RunnableHandlers?.ForEach(r => r.Run());
        }


    }
}
