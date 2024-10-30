using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using DataLayer.Welding;
using WebAPI.Security.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebAPI.Configuration;

namespace WebAPI.Security.Tokens
{
    public class TokenHandler
    {
        private readonly ISet<RefreshToken> _refreshTokens = new HashSet<RefreshToken>();

        private readonly TokenOptions _tokenOptions;

        public TokenHandler(IOptions<TokenOptions> tokenOptionsSnapshot)
        {
            _tokenOptions = tokenOptionsSnapshot.Value;
        }

        public AccessToken CreateAccessToken(UserAccount user)
        {
            var refreshToken = BuildRefreshToken(user);
            var accessToken = BuildAccessToken(user, refreshToken);
            _refreshTokens.Add(refreshToken);

            return accessToken;
        }

        public RefreshToken TakeRefreshToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var refreshToken = _refreshTokens.SingleOrDefault(t => t.Token == token);
            if (refreshToken != null)
                _refreshTokens.Remove(refreshToken);

            return refreshToken;
        }

        public void RevokeRefreshToken(string token)
        {
            TakeRefreshToken(token);
        }

        private RefreshToken BuildRefreshToken(UserAccount user)
        {
            var refreshToken = new RefreshToken
            (
                token : Guid.NewGuid().ToString(),
                expiration : DateTime.UtcNow.AddSeconds(_tokenOptions.RefreshTokenExpirationSeconds).Ticks
            );

            return refreshToken;
        }

        private AccessToken BuildAccessToken(UserAccount user, RefreshToken refreshToken)
        {
            var accessTokenExpiration = DateTime.UtcNow.AddSeconds(_tokenOptions.AccessTokenExpirationSeconds);

            var Key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_tokenOptions.SecretKey));
            var SigningCredentials = new SigningCredentials(Key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature);

            var securityToken = new JwtSecurityToken
            (
                issuer : _tokenOptions.Issuer,
                audience : _tokenOptions.Audience,
                claims : GetClaims(user),
                expires : accessTokenExpiration,
                notBefore : DateTime.UtcNow,
                signingCredentials : SigningCredentials
            );

            var handler = new JwtSecurityTokenHandler();
            var accessToken = handler.WriteToken(securityToken);

            return new AccessToken(accessToken, accessTokenExpiration.Ticks, refreshToken);
        }

        private IEnumerable<Claim> GetClaims(UserAccount user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName)
            };

            claims.Add(new Claim(ClaimTypes.Name, user.UserName));

            // claims.Add(new Claim(ClaimTypes.Role, user.UserRole));

            return claims;
        }
    }
}