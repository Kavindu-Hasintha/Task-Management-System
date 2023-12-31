﻿namespace toDoAPI.Services.RefreshTokenService
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserRepository _userRepository;
        private readonly int RefreshTokenExpirationTime = 25;

        public RefreshTokenService(IHttpContextAccessor httpContextAccessor, IUserRepository userRepository)
        {
            _httpContextAccessor = httpContextAccessor;
            _userRepository = userRepository;
        }

        public RefreshToken GenerateRefreshToken()
        {
            var refreshToken = new RefreshToken
            {
                Token = GenerateRandomToken(),
                Created = DateTime.UtcNow,
                Expires = DateTime.UtcNow.AddMinutes(RefreshTokenExpirationTime)
            };

            return refreshToken;
        }

        private string GenerateRandomToken()
        {
            byte[] randomNumber = new byte[64];

            using (var randomNumberGenerator = RandomNumberGenerator.Create())
            {
                randomNumberGenerator.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }

        public async Task<bool> SetRefreshTokenCookie(int userId, RefreshToken newRefreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = newRefreshToken.Expires,
                Secure = true, // Set to true if your application is served over HTTPS
                SameSite = SameSiteMode.Strict
            };

            _httpContextAccessor.HttpContext.Response.Cookies.Append("refreshToken", newRefreshToken.Token, cookieOptions);

            var isUpdated = await _userRepository.UpdateUserRefreshToken(userId, newRefreshToken.Token, newRefreshToken.Created, newRefreshToken.Expires);
        
            return isUpdated;
        }
    }
}
