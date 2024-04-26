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

using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SAPL.WebAPIDemo.ExampleData.Data;
using SAPL.WebAPIDemo.ExampleData.Models;

namespace Web_API_Demo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticateController : ControllerBase
    {
        private IConfiguration configuration;
        public AuthenticateController(IConfiguration configurationSettings)
        {
            configuration = configurationSettings;
        }

        [HttpPost]
        [Route("[action]")]
        //https://localhost:44345/api/Authenticate/Login
        public IActionResult Login([FromBody] ApplicationUser user)
        {
            List<Claim>? claims = null;

            if (EmployeeCollection.IsEmployee(user.Username))
            {
                Employee? employee = EmployeeCollection.GetEmployee(user.Username);
                claims = new List<Claim>
                {
                    new Claim(nameof(employee.FirstName),employee?.FirstName!),
                    new Claim(nameof(employee.LastName),employee!.LastName),
                    new Claim(nameof(employee.Email),employee.Email),
                    new Claim(nameof(employee.Address),employee.Address),
                    new Claim(nameof(employee.City),employee.City),
                    new Claim(nameof(employee.Country),employee.Country),
                    new Claim(nameof(employee.Status),employee.Status.ToString()),
                    new Claim(nameof(employee.Role),employee.Role.ToString()),
                    new Claim(nameof(employee.Department),employee.Department.ToString()),
                    new Claim(nameof(employee.Id),employee.Id.ToString())

                };
            }
            else if (PatientCollection.IsPatient(user.Username))
            {
                Patient? patient = PatientCollection.GetPatient(user.Username);
                claims = new List<Claim>
                {
                    new Claim(nameof(patient.FirstName), patient?.FirstName!),
                    new Claim(nameof(patient.LastName), patient!.LastName),
                    new Claim(nameof(patient.Email), patient.Email),
                    new Claim(nameof(patient.Address), patient.Address),
                    new Claim(nameof(patient.City), patient.City),
                    new Claim(nameof(patient.State), patient.State),
                    new Claim(nameof(patient.Zip), patient.Zip),
                    new Claim(nameof(patient.Country), patient.Country),
                    new Claim(nameof(patient.Id), patient.Id.ToString())

                };
            }
            else
            {
                return Forbid();
            }
            var token = GetEncryptedJwtToken(claims);
            return new OkObjectResult(new JwtSecurityTokenHandler().WriteToken(token));
        }

        private JwtSecurityToken? GetEncryptedJwtToken(List<Claim> additionalClaims)
        {
            {
                var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(configuration["JWT:Secret"]?.ToString() ?? string.Empty));

                var Token = new JwtSecurityToken(
                    configuration["JWT:ValidIssuer"]?.ToString(),
                    configuration["JWT:ValidAudience"]?.ToString(),
                    additionalClaims,
                    expires: DateTime.Now.AddDays(30.0),
                    signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
                );
                return Token;
            }
        }
    }
}