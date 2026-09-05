using Checkers.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Checkers.Persistence.Data.Configurations
{
	public class UserEntityConfiguration : IEntityTypeConfiguration<User>
	{
		public void Configure(EntityTypeBuilder<User> builder)
		{
			builder.ToTable("Users");
			
			builder.HasKey(k => k.Id);

			builder.Property(p => p.Id)
				.IsRequired();

			builder.Property(p => p.Username)
				.IsRequired()
				.HasMaxLength(50)
				.HasColumnName("User");

			builder.Property(p => p.Password)
				.IsRequired();

			builder.Property(p => p.Email)
				.IsRequired()
				.HasMaxLength(100);

			builder.Property(p => p.FirstName)
				.IsRequired()
				.HasMaxLength(50)
				.HasColumnName("First_Name");

			builder.Property(p => p.LastName)
				.IsRequired()
				.HasMaxLength(50)
				.HasColumnName("Last_Name");

			builder.Property(p => p.MiddleName)
				.HasMaxLength(50)
				.HasColumnName("Middle_Name");

			builder.Property(p => p.NickName)
				.HasMaxLength(50)
				.HasColumnName("Nick_Name");


		}
	}
}
