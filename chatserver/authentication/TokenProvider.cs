using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace chatserver.authentication
{
    public class TokenProvider
    {
        private static TokenProvider instance = new TokenProvider(
            GetSecretKey(), // should exists "JWT_SECRET_KEY" in environment variables
            60, // minutes
            7 // days
        );
        public static TokenProvider Instance { get { return instance; } }

        private readonly string _secretKey;
        private readonly int _accessTokenExpirationMinutes;
        private readonly int _refreshTokenExpirationDays;

        private readonly string issuer = "api.oxserver";
        private readonly string audience = "oxapp";

        private static string GetSecretKey()
        {
            try
            {            
                string? sk = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
                if (sk == null) throw new Exception("No secret key");
                return sk;
            }
            catch (Exception ex)
            {
                Logger.ConsoleLogger.Error(ex);
                return "";
            }
        }

        public TokenProvider(string secretKey, int accessTokenExpirationMinutes, int refreshTokenExpirationDays)
        {
            _secretKey = secretKey;
            _accessTokenExpirationMinutes = accessTokenExpirationMinutes;
            _refreshTokenExpirationDays = refreshTokenExpirationDays;
        }


        public string GenerateToken(string userId, string username)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        public string GenerateRefreshToken()
        {
            var randomBytes = new byte[32];
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes);
        }

        // Validate Token (Access or Refresh)
        public ClaimsPrincipal? ValidateToken(string token, bool isAccessToken = true)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = isAccessToken, // Only validate expiration for Access Tokens
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey)),
                ClockSkew = TimeSpan.Zero // Optional: No leeway for token expiration
            };

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                return tokenHandler.ValidateToken(token, tokenValidationParameters, out _);
            }
            catch
            {
                return null; // Token is invalid
            }
        }

        // Validate Refresh Token (custom implementation)
        public bool ValidateRefreshToken(string refreshToken, string storedRefreshToken)
        {
            return refreshToken == storedRefreshToken;
        }
    }
}
