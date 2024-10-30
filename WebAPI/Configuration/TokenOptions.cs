namespace WebAPI.Configuration
{
    public class TokenOptions
    {
        public string Audience { get; set; }
        public string Issuer { get; set; }
        public long AccessTokenExpirationSeconds { get; set; }
        public long RefreshTokenExpirationSeconds { get; set; }
        public string SecretKey { get; set; }
    }
}