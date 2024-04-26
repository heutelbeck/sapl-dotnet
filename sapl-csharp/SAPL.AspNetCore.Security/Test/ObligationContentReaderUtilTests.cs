using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Subscription;
using Xunit;

namespace SAPL.AspNetCore.Security.Test
{
    public class ObligationContentReaderUtilTests
    {
        public readonly string obligationJson = @"{
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

        [Trait("Unit", "NoPDPRequired")]
        [Fact]
        public Task When_when_oblifation_and_obligatioType_excists_then_equals_directly_extractingMethods()
        {
            var obligation = JObject.Parse(obligationJson);
            var obligationFromJson = ObligationContentReaderUtil.GetProperty(obligation, "obligation");


            Assert.NotNull(obligationFromJson);

            var obligationTypeFromJson = ObligationContentReaderUtil.GetProperty(obligationFromJson, "type");
            Assert.NotNull(obligationTypeFromJson);
            var obligationTypeFromJsonvalue = ObligationContentReaderUtil.GetValue<string>(obligationTypeFromJson);
            Assert.True(!string.IsNullOrEmpty(obligationTypeFromJsonvalue) && obligationTypeFromJsonvalue.Equals("filterProducts"));
            var obligationTypeFromJsonvalueDirect = ObligationContentReaderUtil.GetObligationType(obligation);

            Assert.True(obligationTypeFromJsonvalue.Equals(obligationTypeFromJsonvalueDirect));
            return Task.CompletedTask;
        }

        [Trait("Unit", "NoPDPRequired")]
        [Fact]
        public Task When_conditions_excists_then_jarrray_and_properties()
        {
            var obligation = JObject.Parse(obligationJson);
            var obligationFromJson = ObligationContentReaderUtil.GetProperty(obligation, "obligation");
            var conditions = ObligationContentReaderUtil.GetProperty(obligationFromJson!, "conditions");
            Assert.NotNull(conditions);
            var conditionsArray = JArray.FromObject(conditions);
            Assert.NotNull(conditionsArray);
            Assert.True(conditionsArray.Any());
            foreach (JToken jToken in conditionsArray)
            {
                var path = ObligationContentReaderUtil.GetProperty(jToken, "path");
                var type = ObligationContentReaderUtil.GetProperty(jToken, "type");
                var value = ObligationContentReaderUtil.GetProperty(jToken, "value");
            }
            return Task.CompletedTask;
        }

    }
}
