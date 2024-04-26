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

using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Constraints;
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Constraints.Providers;
using SAPL.AspNetCore.Security.Subscription;
using SAPL.PDP.Api;
using Xunit;

namespace SAPL.AspNetCore.Security.Test
{
    public class BlockingPostEnforceConstraintHandlerBundleTests
    {
        private const string predicateValue = "Test";
        private const string nonPredicateValue = "Test2";

        private const string jsonObligationGeneric = @"{
             'obligation': {
                'type': 'filterTypedContent',
                'conditions': [
                {
                 'path': 'Name',
                 'type': '==',
                 'value': 'Test'      
                }
                              ]
                           }
                             }";

        private const string jsonObligationFilterProducts = @"{
             'obligation': {
                'type': 'filterProducts',
                'conditions': [
                {
                 'path': 'Name',
                 'type': '==',
                 'value': 'Test'      
                }
                              ]
                           }
                             }";
        private const string jsonObligationGenericRegex = @"{
             'obligation': {
                'type': 'filterTypedContentRegex',
                'conditions': [
                {
                 'path': 'Name',
                 'type': '=~',
                 'value': '([0-9])'      
                }
                              ]
                           }
                             }";
        [Trait("Integration", "NoPDPRequired")]
        [Fact]
        public Task When_No_Obligation_And_FilterPredicate_Then_unfiltered_Result()
        {
            var produkt = new Product();
            produkt.Name = predicateValue;
            var produkt2 = new Product();
            produkt2.Name = nonPredicateValue;

            List<Product>? products = new List<Product>
            {
                produkt,
                produkt2
            };
            var handlers = new List<IResponsibleConstraintHandlerProvider> { new ProductsTypedFilterPredicateConstraintHandlerProvider() };

            IConstraintEnforcementService constraintService = new ConstraintEnforcementService(handlers);
            var bundle =
                constraintService.BlockingPostEnforceConstraintHandlerBundle(typeof(List<Product>), products, new AuthorizationDecision(Decision.PERMIT));
            var newProducts = (List<Product>)bundle.HandleAllConstraints()!;
            Assert.NotNull(newProducts);
            Assert.True(newProducts.Any(p => p.Name.Equals(nonPredicateValue)));
            return Task.CompletedTask;
        }

        [Trait("Integration", "NoPDPRequired")]
        [Fact]
        public Task When_Obligation_And_FilterPredicate_Then_filtered_Result()
        {
            var predicate = new Predicate<Product>(p => p.Name.Equals("Test"));

            var produkt = new Product();
            produkt.Name = predicateValue;
            var produkt2 = new Product();
            produkt2.Name = nonPredicateValue;

            List<Predicate<Product>> list = new List<Predicate<Product>> { predicate };
            List<Product>? products = new List<Product>
            {
                produkt,
                produkt2
            };
            var obligation = JObject.Parse(jsonObligationFilterProducts);

            var obligationJson = ObligationContentReaderUtil.GetProperty(obligation, "obligation");
            JArray obligations = new JArray();
            obligations.Add(obligationJson!);

            var handlers = new List<IResponsibleConstraintHandlerProvider> { new ProductsTypedFilterPredicateConstraintHandlerProvider() };

            IConstraintEnforcementService constraintService = new ConstraintEnforcementService(handlers);

            //var bundle =
            //    constraintService.BlockingPostEnforceConstraintHandlerBundle(ref products, ref p,
            //        new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
            var bundle =
                constraintService.BlockingPostEnforceConstraintHandlerBundle(typeof(List<Product>), products, new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
            var newProducts = (List<Product>)bundle.HandleAllConstraints()!;
            Assert.NotNull(newProducts);
            Assert.True(!newProducts.Any(p => p.Name.Equals(nonPredicateValue)));
            return Task.CompletedTask;
        }

        [Trait("Integration", "NoPDPRequired")]
        [Fact]
        public Task CreatePredicate()
        {
            var predicate = new Predicate<Product>(p => p.Name.Equals("Test"));

            var produkt = new Product();
            produkt.Name = predicateValue;
            var produkt2 = new Product();
            produkt2.Name = nonPredicateValue;

            List<Predicate<Product>> list = new List<Predicate<Product>> { predicate };
            List<Product>? products = new List<Product>
            {
                produkt,
                produkt2
            };
            var handlers = new List<IResponsibleConstraintHandlerProvider> { new ProductsTypedFilterPredicateConstraintHandlerProvider() };

            var obligation = JObject.Parse(jsonObligationFilterProducts);

            var obligationJson = ObligationContentReaderUtil.GetProperty(obligation, "obligation");
            JArray obligations = new JArray();
            obligations.Add(obligationJson!);
            IConstraintEnforcementService constraintService = new ConstraintEnforcementService(handlers);
            var bundle =
                constraintService.BlockingPostEnforceConstraintHandlerBundle(typeof(List<Product>),
                    products, new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
            var newProducts = (List<Product>)bundle.HandleAllConstraints()!;
            Assert.NotNull(products);
            Assert.True(!newProducts.Any(p => p.Name.Equals(nonPredicateValue)));
            return Task.CompletedTask;
        }

        [Trait("Integration", "NoPDPRequired")]
        [Fact]
        public Task When_Paramametrized_bundle_and_lambda_in_constraint_then_filter_results()
        {
            var predicate = new Predicate<Product>(p => p.Name.Equals("Test"));

            var produkt = new Product();
            produkt.Name = predicateValue;
            var produkt2 = new Product();
            produkt2.Name = nonPredicateValue;

            List<Predicate<Product>> list = new List<Predicate<Product>> { predicate };
            List<Product>? products = new List<Product>
            {
                produkt,
                produkt2
            };

            var handler = new ProductsTypedFilterPredicateConstraintHandlerProvider();

            var handlers = new List<ITypedPredicateConstraintHandlerProvider> { handler };
            var handlers2 = new List<IFilterPredicateConstraintHandlerProvider>();
            var handlers3 = new List<ITypedContentFilteringProvider>();
            var handlers4 = new List<IJsonContentFilteringProvider>();

            Product? p = null;

            var bundle =
                new BlockingPostEnforceConstraintHandlerBundleForCollection<List<Product>, Product>(ref products, ref p!, handlers, handlers2, handlers3, handlers4);
            List<Product> newProducts = (List<Product>)bundle.HandleAllConstraints()!;
            Assert.NotNull(products);
            Assert.True(!newProducts.Any(p => p.Name.Equals(nonPredicateValue)));
            return Task.CompletedTask;
        }


        [Trait("Integration", "NoPDPRequired")]
        [Fact]
        public Task When_Paramametrized_bundle_and_lambda_in_constraint_then_filter_results2()
        {
            var predicate0 = new Predicate<Product>(p => p.Name.Equals("Test"));

            var produkt = new Product();
            produkt.Name = predicateValue;
            var produkt2 = new Product();
            produkt2.Name = nonPredicateValue;
            List<Product>? products = new List<Product>
            {
                produkt,
                produkt2
            };

            var obligation = JObject.Parse(jsonObligationFilterProducts);

            var obligationJson = ObligationContentReaderUtil.GetProperty(obligation, "obligation");
            JArray obligations = new JArray();
            obligations.Add(obligationJson!);
            var handler = new ProductsTypedFilterPredicateConstraintHandlerProvider();

            var constraintHandlers = new ITypedPredicateConstraintHandlerProvider[] { new ProductsTypedFilterPredicateConstraintHandlerProvider() };

            var predicate = constraintHandlers[0].GetPredicate();

            IConstraintEnforcementService service =
                new ConstraintEnforcementService(constraintHandlers);
            var bundle =
                service.BlockingPostEnforceConstraintHandlerBundle(typeof(List<Product>),
                    products, new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
            var newProducts = (List<Product>)bundle.HandleAllConstraints()!;
            Assert.NotNull(products);
            Assert.True(!newProducts!.Any(p => p.Name.Equals(nonPredicateValue)));
            return Task.CompletedTask;
        }

        [Trait("Integration", "NoPDPRequired")]
        [Fact]
        public Task When_Paramametrized_bundle_and_filterconditions_string_in_constraint_then_filter_results()
        {
            var produkt = new Product();
            produkt.Name = predicateValue;
            var produkt2 = new Product();
            produkt2.Name = nonPredicateValue;

            List<Product>? products = new List<Product>
            {
                produkt,
                produkt2
            };

            var constraintHandlers = new ITypedContentFilteringProvider[] { new ProductWithNameTestFilterPredicateConstraintHandler() };
            var obligation = JObject.Parse(jsonObligationGeneric);
            var obligationJson = ObligationContentReaderUtil.GetProperty(obligation, "obligation");
            JArray obligations = new JArray();
            obligations.Add(obligationJson!);
            IConstraintEnforcementService service =
                new ConstraintEnforcementService(constraintHandlers);
            var bundle =
                service.BlockingPostEnforceConstraintHandlerBundle(typeof(List<Product>),
                    products, new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
            var newProducts = (List<Product>)bundle.HandleFilterPredicateAndTransformationHandlers()!;
            Assert.NotNull(products);
            Assert.True(!newProducts.Any(p => p.Name.Equals(nonPredicateValue)));
            return Task.CompletedTask;
        }

        [Trait("Integration", "NoPDPRequired")]
        [Fact]
        public Task When_Paramametrized_bundle_and_filterconditions_regex_in_constraint_then_filter_results()
        {
            var produkt = new Product();
            produkt.Name = predicateValue;
            var produkt2 = new Product();
            produkt2.Name = nonPredicateValue;

            List<Product>? products = new List<Product>
            {
                produkt,
                produkt2
            };

            var constraintHandlers = new ITypedContentFilteringProvider[] { new ProductWithNameRegexFilterPredicateConstraintHandler() };
            var obligation = JObject.Parse(jsonObligationGenericRegex);
            var obligationJson = ObligationContentReaderUtil.GetProperty(obligation, "obligation");
            JArray obligations = new JArray();
            obligations.Add(obligationJson!);
            IConstraintEnforcementService service =
                 new ConstraintEnforcementService(constraintHandlers);
            var bundle =
                service.BlockingPostEnforceConstraintHandlerBundle(typeof(List<Product>), products, new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
            var newProducts = (List<Product>)bundle.HandleFilterPredicateAndTransformationHandlers()!;
            Assert.NotNull(products);
            Assert.True(!newProducts.Any(p => p.Name.Equals(predicateValue)));
            return Task.CompletedTask;
        }

        [Trait("Integration", "NoPDPRequired")]
        [Fact]
        public Task When_Paramametrized_bundle_and_filterconditions_int_in_constraint_then_filter_results()
        {
            var produkt = new Product();
            produkt.Name = predicateValue;
            produkt.PriceInt = 100;
            var produkt2 = new Product();
            produkt2.Name = nonPredicateValue;
            produkt2.PriceInt = 200;

            List<Product>? products = new List<Product>
            {
                produkt,
                produkt2
            };
            Predicate<Product> predicate = p => p.Name.Equals(predicateValue);
            //Func<List<Product>, Product> selector = str => str.Where(predicate.Invoke).Select();
            //var newProductos = products.Select(selector);

            var constraintHandlers = new ITypedContentFilteringProvider[] { new ProductWithPrice100FilterPredicateConstraintHandler() };

            string json = @"{
             'obligation': {
                'type': 'filterTypedContentPrice',
                'conditions': [
                {
                 'path': 'PriceInt',
                 'type': '>',
                 'value': '100'      
                }
                              ]
                           }
                             }";

            var obligation = JObject.Parse(json);
            var obligationJson = ObligationContentReaderUtil.GetProperty(obligation, "obligation");
            JArray obligations = new JArray();
            if (obligationJson != null) obligations.Add(obligationJson);
            IConstraintEnforcementService service =
                new ConstraintEnforcementService(constraintHandlers);
            var bundle =
                service.BlockingPostEnforceConstraintHandlerBundle(typeof(List<Product>),
                    products, new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
            var newProducts = (List<Product>)bundle.HandleFilterPredicateAndTransformationHandlers()!;
            Assert.NotNull(products);
            Assert.True(!newProducts.Any(p => p.PriceInt <= 100));
            return Task.CompletedTask;
        }

        [Trait("Integration", "NoPDPRequired")]
        [Fact]
        public Task When_filterconditions_And_Replacement_Tranformation_in_constraint_then_manipulated_results()
        {
            var produkt = new Product();
            produkt.Name = predicateValue;
            produkt.PriceInt = 100;
            var produkt2 = new Product();
            produkt2.Name = nonPredicateValue;
            produkt2.PriceInt = 200;

            List<Product>? products = new List<Product>
            {
                produkt,
                produkt2
            };
            var constraintHandlers = new ITypedContentFilteringProvider[] { new ProductWithNameTestFilterPredicateConstraintHandler() };

            string json = @"{
             'obligation': {
                'type': 'filterTypedContent',
                'conditions': [
                {
                 'path': 'PriceInt',
                 'type': '>',
                 'value': '100',
                'actions': [
                {
                 'path': 'Name',
                 'type': 'replace',
                 'replacement': 'Hallöle',

                } 
                           ]

                }
                              ]
                           }
                             }";

            var obligation = JObject.Parse(json);
            var obligationJson = ObligationContentReaderUtil.GetProperty(obligation, "obligation");
            if (obligationJson != null)
            {
                JArray obligations = new JArray { obligationJson };
                IConstraintEnforcementService service =
                    new ConstraintEnforcementService(constraintHandlers);
                var bundle =
                    service.BlockingPostEnforceConstraintHandlerBundle(typeof(List<Product>),
                        products, new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
                var newProducts = (List<Product>)bundle.HandleFilterPredicateAndTransformationHandlers()!;
                Assert.NotNull(products);
                Assert.True(!newProducts!.Any(p => p.Name.Equals(nonPredicateValue)));
            }

            return Task.CompletedTask;
        }

        [Trait("Integration", "NoPDPRequired")]
        [Fact]
        public Task When_filterconditions_And_Delete_Tranformation_in_constraint_then_manipulated_results()
        {
            var produkt = new Product();
            produkt.Name = predicateValue;
            produkt.PriceInt = 100;
            var produkt2 = new Product();
            produkt2.Name = nonPredicateValue;
            produkt2.PriceInt = 200;

            List<Product>? products = new List<Product>
            {
                produkt,
                produkt2
            };
            var constraintHandlers = new ITypedContentFilteringProvider[] { new ProductWithNameTestFilterPredicateConstraintHandler() };

            string json = @"{
             'obligation': {
                'type': 'filterTypedContent',
                'conditions': [
                {
                 'path': 'PriceInt',
                 'type': '>',
                 'value': '100',
                'actions': [
                {
                 'path': 'Name',
                 'type': 'delete',
                 'replacement': 'Hallöle',

                } 
                           ]

                }
                              ]
                           }
                             }";

            var obligation = JObject.Parse(json);
            var obligationJson = ObligationContentReaderUtil.GetProperty(obligation, "obligation");
            JArray obligations = new JArray { obligationJson! };
            IConstraintEnforcementService service =
                new ConstraintEnforcementService(constraintHandlers);
            var bundle =
                service.BlockingPostEnforceConstraintHandlerBundle(typeof(List<Product>),
                    products, new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
            var newProducts = (List<Product>)bundle.HandleFilterPredicateAndTransformationHandlers()!;
            Assert.NotNull(products);
            Assert.True(!newProducts.Any(p => p.Name.Equals(nonPredicateValue)));
            return Task.CompletedTask;
        }


        //[Fact]
        //public Task When_filterconditions_And_Delete_Tranformation_in_constraint_then_manipulated_results2()
        //{
        //    var produkt = new Product();
        //    produkt.Name = predicateValue;
        //    produkt.PriceInt = 100;
        //    var produkt2 = new Product();
        //    produkt2.Name = nonPredicateValue;
        //    produkt2.PriceInt = 200;

        //    List<Product> products = new List<Product>
        //    {
        //        produkt,
        //        produkt2
        //    };
        //    Product? p = null;
        //    var instance = ConstraintApiUtil.CreaInstanceOfFilterPredicateProvider(typeof(Product));

        //    var constraintHandlers = new[] { instance };

        //    string json = @"{
        //     'obligation': {
        //        'type': 'filterTypedContent',
        //        'conditions': [
        //        {
        //         'path': 'PriceInt',
        //         'type': '>',
        //         'value': '100',
        //        'actions': [
        //        {
        //         'path': 'Name',
        //         'type': 'delete',
        //         'replacement': 'Hallöle',

        //        } 
        //                   ]

        //        }
        //                      ]
        //                   }
        //                     }";

        //    var obligation = JObject.Parse(json);
        //    var obligationJson = ObligationContentReaderUtil.GetProperty(obligation, "obligation");
        //    JArray obligations = new JArray { obligationJson };
        //    IConstraintEnforcementService service =
        //        new ConstraintEnforcementService(constraintHandlers);
        //    var bundle =
        //        service.BlockingPostEnforceConstraintHandlerBundle(typeof(List<Product>),
        //            products, new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
        //    var newProducts = (List<Product>)bundle.HandleFilterPredicateAndTransformationHandlers();
        //    Assert.NotNull(products);
        //    Assert.True(!newProducts.Any(p => p.Name.Equals(nonPredicateValue)));
        //    return Task.CompletedTask;
        //}


        public class Product
        {
            public string Name { get; set; } = null!;
            public int PriceInt { get; set; }
        }




        public class ProductsTypedFilterPredicateConstraintHandlerProvider : TypedFilterPredicateConstraintHandlerProviderBase<Product>
        {
            private const string CONSTRAINT_TYPE = "filterProducts";
            protected override Predicate<Product> GetHandler()
            {
                return p => p.Name.Equals("Test");
            }

            public override ISignal.Signal GetSignal()
            {
                return ISignal.Signal.ON_EXECUTION;
            }

            public override bool IsResponsible(JToken constraint)
            {
                var obligationType = ObligationContentReaderUtil.GetObligationType(constraint);
                if (!string.IsNullOrEmpty(obligationType))
                {
                    return obligationType.Equals(CONSTRAINT_TYPE);
                }

                return false;
            }
        }

        public class ProductWithNameTestFilterPredicateConstraintHandler : TypedContentFilteringProviderBase<Product>
        {
            private const string CONSTRAINT_TYPE = "filterTypedContent";
            protected override bool IsResponsible(JToken constraint)
            {
                var obligationType = ObligationContentReaderUtil.GetObligationType(constraint);
                if (obligationType != null)
                {
                    if (obligationType.Equals(CONSTRAINT_TYPE))
                    {
                        this.constraint = constraint;
                        return true;
                    }
                }
                return false;
            }

            protected override List<(Predicate<Product> predicate, List<(Action<Product> action, string actionType)> actions)> GetHandlerWithTransformation(JToken constraint)
            {
                var predicates = new List<(Predicate<Product> predicate, List<(Action<Product> action, string actionType)> actions)>();
                List<(Action<Product> action, string actionType)> actions = new List<(Action<Product> action, string actionType)>();
                predicates.Add((GetPredicate(), actions));
                return predicates;
            }
            protected Predicate<Product> GetPredicate()
            {
                return p => p.Name.Equals("Test");
            }
        }

        public class ProductWithPrice100FilterPredicateConstraintHandler : TypedContentFilteringProviderBase<Product>
        {
            private const string CONSTRAINT_TYPE = "filterTypedContentPrice";
            protected override bool IsResponsible(JToken constraint)
            {
                var obligationType = ObligationContentReaderUtil.GetObligationType(constraint);
                if (obligationType != null)
                {
                    if (obligationType.Equals(CONSTRAINT_TYPE))
                    {
                        this.constraint = constraint;
                        return true;
                    }
                }
                return false;
            }

            protected override List<(Predicate<Product> predicate, List<(Action<Product> action, string actionType)> actions)> GetHandlerWithTransformation(JToken constraint)
            {
                var predicates = new List<(Predicate<Product> predicate, List<(Action<Product> action, string actionType)> actions)>();
                List<(Action<Product> action, string actionType)> actions = new List<(Action<Product> action, string actionType)>();
                predicates.Add((GetPredicate(), actions));
                return predicates;
            }
            protected Predicate<Product> GetPredicate()
            {
                return p => p.PriceInt > 100;
            }
        }

        public class ProductWithNameRegexFilterPredicateConstraintHandler : TypedContentFilteringProviderBase<Product>
        {
            private const string CONSTRAINT_TYPE = "filterTypedContentRegex";
            protected override bool IsResponsible(JToken constraint)
            {
                var obligationType = ObligationContentReaderUtil.GetObligationType(constraint);
                if (obligationType != null)
                {
                    if (obligationType.Equals(CONSTRAINT_TYPE))
                    {
                        this.constraint = constraint;
                        return true;
                    }
                }
                return false;
            }

            protected override List<(Predicate<Product> predicate, List<(Action<Product> action, string actionType)> actions)> GetHandlerWithTransformation(JToken constraint)
            {
                var predicates = new List<(Predicate<Product> predicate, List<(Action<Product> action, string actionType)> actions)>();

                predicates.Add((GetRegExPredicate(), GetEmptyActions<Product>()));
                return predicates;
            }

            protected Predicate<Product> GetRegExPredicate()
            {
                Regex regex = new Regex("([0-9])");
                return p => regex.Match(p.Name).Success;
            }
        }




        //filterTypedContentRegex
    }
}
