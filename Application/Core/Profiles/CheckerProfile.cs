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
		}
	}
}
