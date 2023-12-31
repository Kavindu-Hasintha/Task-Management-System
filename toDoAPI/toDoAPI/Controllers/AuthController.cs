﻿using toDoAPI.Models;
using toDoAPI.Services.RefreshTokenService;

namespace toDoAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly IEmailService _emailService;

        public AuthController(IUserRepository userRepository, IJwtTokenService jwtTokenService, IRefreshTokenService refreshTokenService, IEmailService emailService)
        {
            _userRepository = userRepository;
            _jwtTokenService = jwtTokenService;
            _refreshTokenService = refreshTokenService;
            _emailService = emailService;
        }

        [HttpPost]
        [Route("signup")]
        public async Task<IActionResult> Signup([FromBody] UserRegisterRequest request)
        {
            if (request == null)
            {
                return BadRequest();
            }

            bool isUserExists = await _userRepository.UserExists(request.Email);
            if (isUserExists)
            {
                return BadRequest("User already exists.");
            }

            CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                UserRole = request.UserRole
            };

            var isUserSaved = await _userRepository.RegisterUser(user);

            if (!isUserSaved)
            {
                ModelState.AddModelError("UserError", "Something went wrong while saving.");
                return StatusCode(500, ModelState);
            }

            return Ok("Successfully Registered!");
        }

        [HttpPost]
        [Route("signin")]
        public async Task<IActionResult> SignIn([FromBody] UserSignInDto request)
        {
            var user = await _userRepository.GetUserAsync(request.Email);

            if (user == null)
            {
                return BadRequest("Login failed!");
            }

            if (!VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
            {
                return BadRequest("Login failed!");
            }

            string token = await _jwtTokenService.CreateToken(user);

            var refreshToken = _refreshTokenService.GenerateRefreshToken();

            var isSetRefreshToken = await _refreshTokenService.SetRefreshTokenCookie(user.Id, refreshToken);

            if (!isSetRefreshToken)
            {
                return BadRequest("Server error!");
            }

            return Ok(token);

        }

        [HttpPost]
        [Route("refreshtoken")]
        public async Task<IActionResult> RefreshToken()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];

                if (string.IsNullOrEmpty(refreshToken))
                {
                    return Unauthorized("Refresh token is missing");
                }

                var user = await _userRepository.GetUserAsyncByRefreshToken(refreshToken);

                if (user == null || !user.RefreshToken.Equals(refreshToken))
                {
                    return Unauthorized("Invalid refresh token");
                }

                if (user.TokenExpired < DateTime.UtcNow)
                {
                    return Unauthorized("Token expired");
                }

                string newToken = await _jwtTokenService.CreateToken(user);

                return Ok(newToken);
            }
            catch (Exception ex)
            {
                // Log the exception or handle it appropriately
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPost]
        [Route("forgetpassword")]
        public async Task<IActionResult> ForgetPassword([FromBody] string email)
        {
            try
            {
                if (email == null || email.Length == 0)
                {
                    return BadRequest();
                }

                var IsUserExists = await _userRepository.UserExists(email);

                if (!IsUserExists)
                {
                    return BadRequest();
                }

                Random _random = new Random();
                int otp = 0;
                lock (_random) // Ensure thread safety
                {
                    otp = _random.Next(100000, 999999 + 1);
                }

                string subject = "OTP - Task Management System";
                string body = otp.ToString() + ", this is your OTP.";

                var isEmailSend = await _emailService.SendEmail("task@gmail.com", "taskPassword", email, subject, body);

                if (!isEmailSend)
                {
                    return StatusCode(500, "Internal Server Error");
                }

                return Ok("OTP has sent to your email");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using(var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            }
        }

        private bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512(passwordSalt))
            {
                var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return computedHash.SequenceEqual(passwordHash);
            }
        }
    }
}
