using System.Threading.Tasks;
using WebAPI.Security.Tokens;
using WebAPI.Services;
using WebAPI.Communication;
using DataLayer.Welding;
using BusinessLayer.Accounts;

namespace WebAPI.Services
{
    public class AuthenticationService
    {
        private readonly WeldingContext _context;
        private readonly TokenHandler _tokenHandler;
        private AccountsManager _accountsManager;
        
        public AuthenticationService(WeldingContext context, TokenHandler tokenHandler)
        {
            _accountsManager = new AccountsManager(context);
            _context = context;
            _tokenHandler = tokenHandler;
        }

        public TokenResponse CreateAccessToken(string username, string password)
        {
            var user = _accountsManager.Authenticate(username, password);

            if (user == null)
            {
                return new TokenResponse(false, "Invalid credentials.", null);
            }

            var token = _tokenHandler.CreateAccessToken(user);

            return new TokenResponse(true, null, token);
        }

        public TokenResponse RefreshToken(string refreshToken, string username)
        {
            var token = _tokenHandler.TakeRefreshToken(refreshToken);

            if (token == null)
            {
                return new TokenResponse(false, "Invalid refresh token.", null);
            }

            if (token.IsExpired())
            {
                return new TokenResponse(false, "Expired refresh token.", null);
            }

            var user = _accountsManager.FindByUsername(username);
            if (user == null)
            {
                return new TokenResponse(false, "Invalid refresh token.", null);
            }

            var accessToken = _tokenHandler.CreateAccessToken(user);
            return new TokenResponse(true, null, accessToken);
        }

        public void RevokeRefreshToken(string refreshToken)
        {
            _tokenHandler.RevokeRefreshToken(refreshToken);
        }
    }
}