using AutoMapper;
using Checkers.Application.Core;
using Checkers.Application.Core.Authorization;
using Checkers.Application.Core.Profiles;
using Checkers.Application.Core.Services;
using Checkers.Domain.Entities;
using Checkers.Persistence.Data.IRepositories;
using Checkers.Shared.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace UnitTests.Application.Core.Authorization
{
	public class AuthServiceTests
	{
		private readonly Mock<IUserRepository> _userRepository = new();
		private readonly Mock<IPasswordHasher> _passwordHasher = new();
		private readonly Mock<IJwtTokenService> _tokenService = new();
		private readonly IMapper _mapper;
		private readonly AuthService _sut;

		public AuthServiceTests()
		{
			_mapper = new MapperConfiguration(cfg => cfg.AddProfile<CheckerProfile>(), NullLoggerFactory.Instance).CreateMapper();
			_sut = new AuthService(_userRepository.Object, _passwordHasher.Object, _tokenService.Object, _mapper);

			_tokenService
				.Setup(t => t.GenerateToken(It.IsAny<User>()))
				.Returns(new AuthToken("fake-jwt", DateTime.UtcNow.AddHours(1)));
		}

		private static RegisterRequestDto ValidRegisterRequest() => new()
		{
			Username = "new_player",
			Password = "Password1",
			Email = "new_player@example.com",
			FirstName = "New",
			LastName = "Player",
		};

		[Fact]
		public async Task RegisterAsync_WithValidData_ReturnsSuccessWithToken()
		{
			_userRepository
				.Setup(r => r.ExistsByUsernameOrEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
				.ReturnsAsync(false);
			_passwordHasher
				.Setup(h => h.Hash(It.IsAny<User>(), "Password1"))
				.Returns("hashed-password");

			var result = await _sut.RegisterAsync(ValidRegisterRequest());

			Assert.True(result.Success);
			Assert.Equal("fake-jwt", result.Response!.Token);
			_userRepository.Verify(r => r.AddAsync(It.Is<User>(u => u.PasswordHash == "hashed-password")), Times.Once);
			_userRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
		}

		[Fact]
		public async Task RegisterAsync_WithWeakPassword_FailsWithoutTouchingRepository()
		{
			var request = ValidRegisterRequest();
			request.Password = "weak";

			var result = await _sut.RegisterAsync(request);

			Assert.False(result.Success);
			Assert.NotEmpty(result.Errors);
			_userRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
		}

		[Fact]
		public async Task RegisterAsync_WithDuplicateUsernameOrEmail_Fails()
		{
			_userRepository
				.Setup(r => r.ExistsByUsernameOrEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
				.ReturnsAsync(true);

			var result = await _sut.RegisterAsync(ValidRegisterRequest());

			Assert.False(result.Success);
			Assert.Contains(result.Errors, e => e.Contains("already taken"));
		}

		[Fact]
		public async Task LoginAsync_WithValidCredentials_ReturnsSuccessWithToken()
		{
			var user = new User { Id = 1, Username = "player1", PasswordHash = "hashed", Email = "p@example.com", FirstName = "P", LastName = "One" };
			_userRepository.Setup(r => r.GetByUsernameAsync("player1")).ReturnsAsync(user);
			_passwordHasher.Setup(h => h.Verify(user, "hashed", "Password1")).Returns(true);

			var result = await _sut.LoginAsync(new LoginRequestDto { Username = "player1", Password = "Password1" });

			Assert.True(result.Success);
			Assert.Equal("fake-jwt", result.Response!.Token);
		}

		[Fact]
		public async Task LoginAsync_WithWrongPassword_Fails()
		{
			var user = new User { Id = 1, Username = "player1", PasswordHash = "hashed", Email = "p@example.com", FirstName = "P", LastName = "One" };
			_userRepository.Setup(r => r.GetByUsernameAsync("player1")).ReturnsAsync(user);
			_passwordHasher.Setup(h => h.Verify(user, "hashed", It.IsAny<string>())).Returns(false);

			var result = await _sut.LoginAsync(new LoginRequestDto { Username = "player1", Password = "wrong" });

			Assert.False(result.Success);
		}

		[Fact]
		public async Task LoginAsync_WithUnknownUsername_Fails()
		{
			_userRepository.Setup(r => r.GetByUsernameAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

			var result = await _sut.LoginAsync(new LoginRequestDto { Username = "ghost", Password = "Password1" });

			Assert.False(result.Success);
		}
	}
}
