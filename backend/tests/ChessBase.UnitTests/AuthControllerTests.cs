using ChessBase.Api.Authentication;
using ChessBase.Api.Controllers;
using ChessBase.Api.Email;
using ChessBase.Application.Contracts;
using ChessBase.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChessBase.UnitTests;

public class AuthControllerTests
{
    [Fact]
    public async Task Register_ReturnsOk_AndCallsCreateWithExpectedEmail_WhenValidRequest()
    {
        var userManager = new TestUserManager
        {
            CreateAsyncHandler = (_, _) => Task.FromResult(IdentityResult.Success)
        };

        var tokenService = new FakeJwtTokenService
        {
            AccessToken = "register-token"
        };
        var controller = new AuthController(userManager, tokenService, new FakeEmailSender());

        var request = new AuthRegisterRequest("john", "john@example.com", "Password123");
        var actionResult = await controller.Register(request);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<AuthTokenResponse>(ok.Value);
        Assert.Equal("register-token", response.AccessToken);
        Assert.Equal("john@example.com", userManager.LastCreatedUser?.Email);
        Assert.Equal("john", userManager.LastCreatedUser?.UserName);
        Assert.Equal("Password123", userManager.LastCreatedPassword);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenUserAlreadyExists()
    {
        var userManager = new TestUserManager
        {
            CreateAsyncHandler = (_, _) => Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateUserName",
                Description = "Username already exists"
            }))
        };

        var controller = new AuthController(userManager, new FakeJwtTokenService(), new FakeEmailSender());

        var request = new AuthRegisterRequest("john", "john@example.com", "Password123");
        var actionResult = await controller.Register(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(400, badRequest.StatusCode ?? 400);
        var errors = ExtractErrors(badRequest.Value);
        Assert.Contains("Username already exists", errors);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailIsInvalid()
    {
        var userManager = new TestUserManager
        {
            CreateAsyncHandler = (_, _) => Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidEmail",
                Description = "Email 'not-an-email' is invalid."
            }))
        };

        var controller = new AuthController(userManager, new FakeJwtTokenService(), new FakeEmailSender());

        var request = new AuthRegisterRequest("john", "not-an-email", "Password123");
        var actionResult = await controller.Register(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var errors = ExtractErrors(badRequest.Value);
        Assert.Contains("Email 'not-an-email' is invalid.", errors);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPasswordIsWeak()
    {
        var userManager = new TestUserManager
        {
            CreateAsyncHandler = (_, _) => Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordRequiresNonAlphanumeric",
                Description = "Passwords must have at least one non alphanumeric character."
            }))
        };

        var controller = new AuthController(userManager, new FakeJwtTokenService(), new FakeEmailSender());

        var request = new AuthRegisterRequest("john", "john@example.com", "Password123");
        var actionResult = await controller.Register(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var errors = ExtractErrors(badRequest.Value);
        Assert.Contains("Passwords must have at least one non alphanumeric character.", errors);
    }

    [Fact]
    public async Task Login_ReturnsOkWithAuthToken_WhenPasswordIsCorrect()
    {
        var user = new ApplicationUser
        {
            Id = "user-1",
            UserName = "john",
            Email = "john@example.com"
        };

        var userManager = new TestUserManager
        {
            FindByNameAsyncHandler = _ => Task.FromResult<ApplicationUser?>(user),
            CheckPasswordAsyncHandler = (_, _) => Task.FromResult(true)
        };

        var tokenService = new FakeJwtTokenService
        {
            AccessToken = "login-token"
        };

        var controller = new AuthController(userManager, tokenService, new FakeEmailSender());

        var request = new AuthLoginRequest("john", "Password123");
        var actionResult = await controller.Login(request);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<AuthTokenResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.Equal("login-token", response.AccessToken);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenUserNotFound()
    {
        var userManager = new TestUserManager
        {
            FindByNameAsyncHandler = _ => Task.FromResult<ApplicationUser?>(null),
            FindByEmailAsyncHandler = _ => Task.FromResult<ApplicationUser?>(null)
        };

        var controller = new AuthController(userManager, new FakeJwtTokenService(), new FakeEmailSender());

        var request = new AuthLoginRequest("missing-user", "Password123");
        var actionResult = await controller.Login(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal("Invalid credentials.", unauthorized.Value);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIsWrong()
    {
        var user = new ApplicationUser
        {
            Id = "user-1",
            UserName = "john",
            Email = "john@example.com"
        };

        var userManager = new TestUserManager
        {
            FindByNameAsyncHandler = _ => Task.FromResult<ApplicationUser?>(user),
            FindByEmailAsyncHandler = _ => Task.FromResult<ApplicationUser?>(null),
            CheckPasswordAsyncHandler = (_, _) => Task.FromResult(false)
        };

        var controller = new AuthController(userManager, new FakeJwtTokenService(), new FakeEmailSender());

        var request = new AuthLoginRequest("john", "wrong-password");
        var actionResult = await controller.Login(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal("Invalid credentials.", unauthorized.Value);
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public string AccessToken { get; init; } = "token";

        public AuthTokenResponse CreateToken(ApplicationUser user)
        {
            return new AuthTokenResponse(AccessToken, DateTime.UtcNow.AddMinutes(60));
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestUserManager : UserManager<ApplicationUser>
    {
        public Func<ApplicationUser, string, Task<IdentityResult>>? CreateAsyncHandler { get; init; }
        public Func<string, Task<ApplicationUser?>>? FindByNameAsyncHandler { get; init; }
        public Func<string, Task<ApplicationUser?>>? FindByEmailAsyncHandler { get; init; }
        public Func<ApplicationUser, string, Task<bool>>? CheckPasswordAsyncHandler { get; init; }
        public ApplicationUser? LastCreatedUser { get; private set; }
        public string? LastCreatedPassword { get; private set; }

        public TestUserManager()
            : base(
                new InMemoryUserStore(),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                new EmptyServiceProvider(),
                NullLogger<UserManager<ApplicationUser>>.Instance)
        {
        }

        public override Task<IdentityResult> CreateAsync(ApplicationUser user, string password)
        {
            LastCreatedUser = user;
            LastCreatedPassword = password;
            return CreateAsyncHandler?.Invoke(user, password)
                ?? Task.FromResult(IdentityResult.Success);
        }

        public override Task<ApplicationUser?> FindByNameAsync(string userName)
        {
            return FindByNameAsyncHandler?.Invoke(userName)
                ?? Task.FromResult<ApplicationUser?>(null);
        }

        public override Task<ApplicationUser?> FindByEmailAsync(string email)
        {
            return FindByEmailAsyncHandler?.Invoke(email)
                ?? Task.FromResult<ApplicationUser?>(null);
        }

        public override Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
        {
            return CheckPasswordAsyncHandler?.Invoke(user, password)
                ?? Task.FromResult(false);
        }
    }

    private sealed class InMemoryUserStore : IUserStore<ApplicationUser>
    {
        public void Dispose()
        {
        }

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.Id);
        }

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.UserName);
        }

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.NormalizedUserName);
        }

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<ApplicationUser?>(null);
        }

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            return Task.FromResult<ApplicationUser?>(null);
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }

    private static string[] ExtractErrors(object? badRequestValue)
    {
        Assert.NotNull(badRequestValue);
        var errorsProperty = badRequestValue!.GetType().GetProperty("Errors");
        Assert.NotNull(errorsProperty);

        var errors = errorsProperty!.GetValue(badRequestValue) as string[];
        Assert.NotNull(errors);
        return errors!;
    }
}
