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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Constraints;
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.PolicyMapping;
using SAPL.PDP.Api;
using Xunit;

namespace SAPL.AspNetCore.Security.Test.Constraints.Providers;

// This class contains unit tests for JSON content filtering within the SAPL framework.
// It tests various scenarios of filtering and transforming JSON content based on SAPL policies and obligations.
public class JsonContentFilteringProviderTests
{
    // Constant values used for predicate conditions in the tests.
    private const string predicateValue = "Test";
    private const string nonPredicateValue = "Test2";

    // A property to create and return a list of test products.
    public List<Product>? Products
    {
        get
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
            return products;
        }
    }

    public Product? TestProduct
    {
        get
        {
            var produkt = new Product();
            produkt.Name = predicateValue;
            produkt.PriceInt = 100;
            var produkt2 = new Product();
            produkt2.Name = nonPredicateValue;
            produkt2.PriceInt = 200;
            produkt.ReferencedProducts = new List<Product>() { produkt2 };
            return produkt;
        }
    }

    // This test verifies the replace transformation on a list of products based on a condition.
    [Trait("Integration", "NoPDPRequired")]
    [Fact]
    public Task When_ListValue_And_Filtercondition_And_Replace_Tranformation_in_constraint_then_manipulated_results()
    {
        string replacement = "Hallöle";

        var obl = new Obligation()
        {
            type = "filterJsonPathContent",
            conditions = new Condition[]{new Condition()
            {
                path = "$.[?(@.Name == 'Test')]", type = "",value = "",
                actions =  new Transformation[] { new Transformation() { path = "Name", replacement = replacement, type = "replace" } }
            }},
        };
        var oblJson = JsonConvert.SerializeObject(obl);
        JObject originalJson = JObject.Parse(oblJson);
        JArray obligations = new JArray { originalJson };
        IConstraintEnforcementService service =
            new ConstraintEnforcementService(new List<IResponsibleConstraintHandlerProvider>());
        var bundle =
            service.BlockingPostEnforceConstraintHandlerBundle(typeof(List<Product>),
                Products, new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
        var newProducts = bundle.HandleJsonFilterPredicateAndTransformationHandlers();
        Assert.NotNull(newProducts);
        Assert.True(((List<Product>)newProducts).Any(p => p.Name.Equals(replacement)));
        return Task.CompletedTask;
    }

    // This test verifies the replace transformation on a single product with nested products based on a condition.
    [Trait("Integration", "NoPDPRequired")]
    [Fact]
    public Task When_ElementValue_And_Filtercondition_And_Replace_Tranformation_in_constraint_then_manipulated_results()
    {
        string replacement = "Hallöle";

        var obl = new Obligation()
        {
            type = "filterJsonPathContent",
            conditions = new Condition[]{new Condition()
            {
                path = "$.ReferencedProducts[?(@.Name == 'Test2')]", type = "",value = "",
                actions =  new Transformation[] { new Transformation() { path = "Name", replacement = replacement, type = "replace" } }
            }},
        };
        var oblJson = JsonConvert.SerializeObject(obl);
        JObject originalJson = JObject.Parse(oblJson);
        JArray obligations = new JArray { originalJson };
        IConstraintEnforcementService service =
            new ConstraintEnforcementService(new List<IResponsibleConstraintHandlerProvider>());
        var bundle =
            service.BlockingPostEnforceConstraintHandlerBundle(typeof(Product),
                TestProduct, new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
        var newProduct = bundle.HandleJsonFilterPredicateAndTransformationHandlers();
        Assert.NotNull(newProduct);
        Assert.True(((Product)newProduct).ReferencedProducts.Any(p => p.Name.Equals(replacement)));
        return Task.CompletedTask;
    }

    // This test checks the blacken (partial masking) transformation on a list of products based on a condition.
    [Trait("Integration", "NoPDPRequired")]
    [Fact]
    public Task When_ListValue_And_Filtercondition_And_Blacken_Tranformation_in_constraint_then_manipulated_results()
    {
        string replacement = "XX";

        var obl = new Obligation()
        {
            type = "filterJsonPathContent",
            conditions = new Condition[]{new Condition()
            {
                path = "$.[?(@.Name == 'Test')]", type = "",value = "",
                actions =  new Transformation[] { new Transformation() { path = "Name", replacement = replacement, type = "blacken", discloseLeft = 1, discloseRight = 1} }
            }},
        };
        var oblJson = JsonConvert.SerializeObject(obl);
        JObject originalJson = JObject.Parse(oblJson);
        JArray obligations = new JArray { originalJson };
        IConstraintEnforcementService service =
            new ConstraintEnforcementService(new List<IResponsibleConstraintHandlerProvider>());
        var bundle =
            service.BlockingPostEnforceConstraintHandlerBundle(typeof(List<Product>),
                Products, new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
        var newProducts = bundle.HandleJsonFilterPredicateAndTransformationHandlers();
        Assert.NotNull(newProducts);
        Assert.True(((List<Product>)newProducts).Any(p => p.Name.Contains(replacement)));
        return Task.CompletedTask;
    }

    // This test verifies the delete operation on a list of products based on a filter condition.
    [Trait("Integration", "NoPDPRequired")]
    [Fact]
    public Task When_ListValue_And_Filtercondition_And_Delete_Tranformation_in_constraint_then_manipulated_results()
    {
        var obl = new Obligation()
        {
            type = "filterJsonPathContent",
            conditions = new Condition[]{new Condition()
            {
                path = "$.[?(@.Name == 'Test')]", type = "",value = "",
                actions =  new Transformation[]
                    { new Transformation()
                    {
                        type = "delete"
                    } }
            }},
        };
        var oblJson = JsonConvert.SerializeObject(obl);
        JObject originalJson = JObject.Parse(oblJson);
        JArray obligations = new JArray { originalJson };
        IConstraintEnforcementService service =
            new ConstraintEnforcementService(new List<IResponsibleConstraintHandlerProvider>());
        var bundle =
            service.BlockingPostEnforceConstraintHandlerBundle(typeof(List<Product>),
                Products, new AuthorizationDecision(Decision.PERMIT, obligations: obligations));
        var newProducts = bundle.HandleJsonFilterPredicateAndTransformationHandlers();
        Assert.NotNull(newProducts);
        Assert.True(!((List<Product>)newProducts).Any(p => p.Name.Equals(predicateValue)));
        return Task.CompletedTask;
    }


    public class Product
    {
        public string Name { get; set; } = null!;
        public int PriceInt { get; set; }
        public List<Product> ReferencedProducts { get; set; } = null!;
    }
}