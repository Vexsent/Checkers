using System.Reflection;

namespace Checkers.Shared.Authorization
{
    public static class Roles
    {
        public const string User = nameof(User);
        public const string Admin = nameof(Admin);

        public static IEnumerable<string> GetAllRoles()
        {
            return typeof(Roles).GetFields(BindingFlags.Public | BindingFlags.Static)
              .Where(f => f.FieldType == typeof(string))
              .Select(x => (string)x.GetValue(null)!);
        }
    }
}