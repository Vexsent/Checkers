using AutoMapper;
using Checkers.Application.Core.Profiles;
using Checkers.Domain.Entities;
using Checkers.Shared.DTOs;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Application.Core.Profiles
{
	public class CheckerProfileTests
	{
		private readonly MapperConfiguration _config =
			new(cfg => cfg.AddProfile<CheckerProfile>(), NullLoggerFactory.Instance);

		[Fact]
		public void Configuration_IsValid()
		{
			// Fails if any map leaves a destination member unmapped without an explicit Ignore().
			_config.AssertConfigurationIsValid();
		}

		[Fact]
		public void Map_RegisterRequestToUser_CopiesFieldsAndLeavesPasswordHashUnset()
		{
			var request = new RegisterRequestDto
			{
				Username = "alice",
				Password = "Password1",
				Email = "alice@example.com",
				FirstName = "Alice",
				MiddleName = "B",
				LastName = "Smith",
				NickName = "Al",
			};

			var user = _config.CreateMapper().Map<User>(request);

			Assert.Equal(request.Username, user.Username);
			Assert.Equal(request.Email, user.Email);
			Assert.Equal(request.FirstName, user.FirstName);
			Assert.Equal(request.MiddleName, user.MiddleName);
			Assert.Equal(request.LastName, user.LastName);
			Assert.Equal(request.NickName, user.NickName);
			Assert.NotEqual(request.Password, user.PasswordHash);
		}

		[Fact]
		public void Map_UserToUserDto_CopiesFieldsWithoutSecrets()
		{
			var user = new User
			{
				Id = 7,
				Username = "alice",
				PasswordHash = "hash",
				Email = "alice@example.com",
				FirstName = "Alice",
				LastName = "Smith",
			};

			var dto = _config.CreateMapper().Map<UserDto>(user);

			Assert.Equal(user.Id, dto.Id);
			Assert.Equal(user.Username, dto.Username);
			Assert.Equal(user.FirstName, dto.FirstName);
			Assert.Equal(user.LastName, dto.LastName);
			Assert.Null(typeof(UserDto).GetProperty("PasswordHash"));
		}
	}
}
