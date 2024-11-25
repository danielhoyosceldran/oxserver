using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace chatserver.authentication
{
    public class TokenProvider
    {
        private readonly string _secretKey;
        private readonly int _expirationMinutes;

        public TokenProvider(string secretKey, int expirationMinutes)
        {
            _secretKey = secretKey;
            _expirationMinutes = expirationMinutes;
        }

        public string GenerateToken(string userId, string username)
        {
            // 1. Configura els "claims" (informació del token)
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // 2. Configura la clau secreta i l’algorisme de signaturawha
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 3. Crea el token amb configuració de temps d’expiració
            var token = new JwtSecurityToken(
                issuer: "your-issuer",
                audience: "your-audience",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),
                signingCredentials: creds
            );

            // 4. Retorna el token com una cadena
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
