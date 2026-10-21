using System.Security.Claims;
using System.Security.Principal;

namespace Checkers.Client.Extensions
{
	public static class IIdentityExtension
	{
		extension(IIdentity identity)
		{
			/// <summary>
			/// The username (the token's ClaimTypes.Name claim). Identity.Name is the email,
			/// since JwtAuthStateProvider sets ClaimTypes.Email as the identity's name claim.
			/// </summary>
			public string? Account => (identity as ClaimsIdentity)?.FindFirst(ClaimTypes.Name)?.Value;
		}
	}
}
