using System.Text;
using ChessBase.Api.Authentication;
using ChessBase.Api.Email;
using ChessBase.Application.Contracts;
using ChessBase.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace ChessBase.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    IJwtTokenService jwtTokenService,
    IEmailSender emailSender) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] AuthRegisterRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Login, email and password are required.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Login.Trim(),
            Email = request.Email.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                Errors = createResult.Errors.Select(e => e.Description).ToArray()
            });
        }

        var token = jwtTokenService.CreateToken(user);
        return Ok(token);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthLoginRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Login and password are required.");
        }

        var login = request.Login.Trim();
        var user = await userManager.FindByNameAsync(login) ?? await userManager.FindByEmailAsync(login);

        if (user is null)
        {
            return Unauthorized("Invalid credentials.");
        }

        var isPasswordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
        {
            return Unauthorized("Invalid credentials.");
        }

        var token = jwtTokenService.CreateToken(user);
        return Ok(token);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Email is required.");
        }

        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);

        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            await emailSender.SendAsync(
                email,
                "ChessBase password reset",
                $"Use this token to reset your password: {encodedToken}",
                cancellationToken);
        }

        return Ok("If the email exists, password reset instructions have been sent.");
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest("Email, token and new password are required.");
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return BadRequest("Invalid password reset request.");
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token.Trim()));
        }
        catch (FormatException)
        {
            return BadRequest("Invalid token format.");
        }

        var resetResult = await userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);
        if (!resetResult.Succeeded)
        {
            return BadRequest(new
            {
                Errors = resetResult.Errors.Select(e => e.Description).ToArray()
            });
        }

        return Ok("Password has been reset.");
    }
}
