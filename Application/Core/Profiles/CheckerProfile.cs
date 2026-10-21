using AutoMapper;
using Checkers.Domain.Entities;
using Checkers.Shared.DTOs;

namespace Checkers.Application.Core.Profiles
{
	public class CheckerProfile : Profile
	{
		public CheckerProfile()
		{
			CreateMap<User, UserDto>()
				.ForMember(dest => dest.Name, opt => opt.Ignore())
				.ReverseMap();

			CreateMap<RegisterRequestDto, User>()
				.ForMember(dest => dest.Id, opt => opt.Ignore())
				.ForMember(dest => dest.PasswordHash, opt => opt.Ignore());

			// Login only carries Username/Password: everything else on User comes from the database, never the request.
			CreateMap<LoginRequestDto, User>()
				.ForMember(dest => dest.Id, opt => opt.Ignore())
				.ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
				.ForMember(dest => dest.Email, opt => opt.Ignore())
				.ForMember(dest => dest.FirstName, opt => opt.Ignore())
				.ForMember(dest => dest.LastName, opt => opt.Ignore())
				.ForMember(dest => dest.MiddleName, opt => opt.Ignore())
				.ForMember(dest => dest.NickName, opt => opt.Ignore());

		}
	}
}
