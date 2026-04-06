using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ACE.Database;
using ACE.Database.Models.Auth;
using ACE.Entity.Enum;
using System;
using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ACE.Common;

namespace ACE.Server.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : BaseController
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(AuthController));


        [HttpPost("login")]
        [AllowAnonymous]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                return Unauthorized(new { message = "Username and password are required." });

            try
            {
                var account = DatabaseManager.Authentication.GetAccountByName(request.Username);

                if (account == null)
                {
                    log.Info($"Web Portal login attempt for non-existent account: {request.Username}");
                    return Unauthorized(new { message = "Invalid username or password." });
                }

                // Check password parity with AuthenticationHandler.cs
                if (!account.PasswordMatches(request.Password))
                {
                    log.Info($"Web Portal login attempt with incorrect password for: {request.Username}");
                    return Unauthorized(new { message = "Invalid username or password." });
                }

                // Check for bans parity with AuthenticationHandler.cs
                if (account.BanExpireTime.HasValue)
                {
                    var now = DateTime.UtcNow;
                    if (now < account.BanExpireTime.Value)
                    {
                        var reason = account.BanReason ?? "No reason provided.";
                        log.Info($"Banned account {request.Username} attempted to log in to Web Portal. Reason: {reason}");
                        return StatusCode(403, new { message = $"Account Banned: {reason}" });
                    }
                }

                log.Info($"Web Portal login successful for: {request.Username}");

                // Update last login (matching game client behavior)
                account.UpdateLastLogin(HttpContext.Connection.RemoteIpAddress ?? IPAddress.Loopback);

                // Generate JWT Token
                var token = GenerateJwtToken(account);

                // Set HttpOnly Cookie
                var isDevelopment = HttpContext.Request.Host.Host == "localhost" || HttpContext.Request.Host.Host == "127.0.0.1";
                Response.Cookies.Append("ilt_auth_token", token, new Microsoft.AspNetCore.Http.CookieOptions
                {
                    HttpOnly = true,
                    Secure = !isDevelopment,
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
                    Expires = DateTime.UtcNow.AddHours(8)
                });

                return Ok(new
                {
                    username = account.AccountName,
                    accessLevel = (AccessLevel)account.AccessLevel,
                    lastLogin = account.LastLoginTime
                });
            }
            catch (Exception ex)
            {
                log.Error("Error during Web Portal authentication", ex);
                return StatusCode(500, new { message = "Internal server error during authentication." });
            }
        }

        private string GenerateJwtToken(Account account)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(WebPortalHost.Secret);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, account.AccountName),
                    new Claim(ClaimTypes.Role, ((AccessLevel)account.AccessLevel).ToString()),
                    new Claim("AccountId", account.AccountId.ToString())
                }),
                Expires = DateTime.UtcNow.AddHours(8),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("ilt_auth_token");
            return Ok(new { message = "Logged out successfully" });
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult GetIdentity()
        {
            // Restores in-memory state on page refresh
            var username = User.Identity?.Name;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var accountIdStr = User.FindFirst("AccountId")?.Value;

            if (string.IsNullOrEmpty(username) || !Enum.TryParse<AccessLevel>(role, out var accessLevel))
                return Unauthorized();

            return Ok(new
            {
                username,
                accessLevel,
                accountId = uint.Parse(accountIdStr ?? "0")
            });
        }

        public class LoginRequest
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
        }
    }
}
