using AutoMapper;
using Checkers.Application.Core.Services;
using Checkers.Domain.Entities;
using Checkers.Persistence.Data.IRepositories;
using Checkers.Shared.Authentication.Policies;
using Checkers.Shared.DTOs;

namespace Checkers.Application.Core.Authorization
{
	internal class AuthService(
		IUserRepository userRepository,
		IPasswordHasher passwordHasher,
		IJwtTokenService tokenService,
		IMapper mapper) : IAuthService
	{
		private readonly IMapper _mapper = mapper;

		public async Task<AuthResult> RegisterAsync(RegisterRequestDto request)
		{
			var errors = new List<string>();
			errors.AddRange(UsernamePolicy.Validate(request.Username));
			errors.AddRange(PasswordPolicy.Validate(request.Password));

			if (await userRepository.ExistsByUsernameOrEmailAsync(request.Username, request.Email))
				errors.Add("Username or email is already taken.");

			if (errors.Count > 0)
				return AuthResult.Failed(errors);

			var user = _mapper.Map<User>(request);

			user.PasswordHash = passwordHasher.Hash(user, request.Password);

			await userRepository.AddAsync(user);
			await userRepository.SaveChangesAsync();

			return AuthResult.Succeeded(BuildAuthResponse(user));
		}

		public async Task<AuthResult> LoginAsync(LoginRequestDto request)
		{
			var user = await userRepository.GetByUsernameAsync(request.Username);
			if (user is null || !passwordHasher.Verify(user, user.PasswordHash, request.Password))
				return AuthResult.Failed(["Invalid username or password."]);

			return AuthResult.Succeeded(BuildAuthResponse(user));
		}

		private AuthResponseDto BuildAuthResponse(User user)
		{
			var token = tokenService.GenerateToken(user);
			return new AuthResponseDto
			{
				Token = token.Token,
				ExpiresAtUtc = token.ExpiresAtUtc,
				User = mapper.Map<UserDto>(user),
			};
		}
	}
}
