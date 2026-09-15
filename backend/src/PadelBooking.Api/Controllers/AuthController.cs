using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using PadelBooking.Api.DTOs;
using PadelBooking.Application.Authentication.GetCurrentUser;
using PadelBooking.Application.Authentication.Login;
using PadelBooking.Application.Authentication.Register;

namespace PadelBooking.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly RegisterUser _registerUser;
        private readonly LoginUser _loginUser;
        private readonly GetCurrentUser _getCurrentUser;

        public AuthController(
            RegisterUser registerUser,
            LoginUser loginUser,
            GetCurrentUser getCurrentUser)
        {
            _registerUser = registerUser;
            _loginUser = loginUser;
            _getCurrentUser = getCurrentUser;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var result = await _registerUser.ExecuteAsync(
                new RegisterCommand(
                    request.FirstName,
                    request.LastName,
                    request.Email,
                    request.Password),
                HttpContext.RequestAborted);

            if (result.IsDuplicateEmail)
            {
                return BadRequest("Korisnik sa ovim email-om već postoji.");
            }

            var user = result.User!;
            return Ok(new
            {
                message = "Registracija uspešna.",
                user = new
                {
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.Role
                }
            });
        }

        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var result = await _loginUser.ExecuteAsync(
                new LoginCommand(request.Email, request.Password),
                HttpContext.RequestAborted);

            if (!result.Succeeded)
            {
                return Unauthorized("Pogrešan email ili lozinka.");
            }

            var user = result.User!;
            return Ok(new
            {
                message = "Prijava uspešna.",
                token = result.Token,
                user = new
                {
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Email,
                    user.Role
                }
            });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var user = await _getCurrentUser.ExecuteAsync(
                userId,
                HttpContext.RequestAborted);

            if (user == null)
            {
                return NotFound("Korisnik nije pronađen.");
            }

            return Ok(new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Role
            });
        }
    }
}
