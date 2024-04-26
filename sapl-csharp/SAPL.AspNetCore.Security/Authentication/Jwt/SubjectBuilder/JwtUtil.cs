// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace SAPL.AspNetCore.Security.Authentication.Jwt.SubjectBuilder;

/// <summary>
/// Utility class for handling JWT tokens, providing methods to encrypt and decrypt them.
/// </summary>
public class JwtUtil
{
    /// <summary>
    /// Decrypts a JWT token using provided configuration settings.
    /// </summary>
    /// <param name="jwtToken">The JWT token to be decrypted.</param>
    /// <param name="configuration">The configuration containing JWT settings.</param>
    /// <returns>The decrypted JwtSecurityToken or null if decryption fails.</returns>
    public JwtSecurityToken? GetDecryptedJwtToken(string? jwtToken, IConfiguration configuration)
    {
        if (jwtToken == null)
            return null;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(configuration?["JWT:Secret"] ?? string.Empty);
        try
        {
            // Validate the token using the token validation parameters
            tokenHandler.ValidateToken(jwtToken, new TokenValidationParameters
            {
                ValidAudience = configuration?["JWT:ValidAudience"],
                ValidIssuer = configuration?["JWT:ValidIssuer"],
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuerSigningKey = true,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var decryptedToken = (JwtSecurityToken)validatedToken;

            return decryptedToken;
        }
        catch
        {
            // Return null if token validation fails
            return null;
        }
    }

    /// <summary>
    /// Generates an encrypted JWT token with additional claims.
    /// </summary>
    /// <param name="additionalClaims">The additional claims to be included in the token.</param>
    /// <param name="configuration">The configuration containing JWT settings.</param>
    /// <returns>The encrypted JwtSecurityToken.</returns>
    public JwtSecurityToken? GetEncryptedJwtToken(List<Claim> additionalClaims, IConfiguration configuration)
    {
        var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(configuration["JWT:Secret"]?.ToString() ?? string.Empty));

        // Create a new JWT token with the specified claims and configuration
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