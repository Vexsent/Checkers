using Checkers.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Checkers.Persistence.Data.Configurations
{
	public class MatchRecordEntityConfiguration : IEntityTypeConfiguration<MatchRecord>
	{
		public void Configure(EntityTypeBuilder<MatchRecord> builder)
		{
			builder.ToTable("Match_Records");

			builder.HasKey(k => k.Id);

			// Restrict, not cascade: SQL Server rejects two cascade paths from Users into Match_Records,
			// and deleting a user shouldn't silently erase their opponent's history.
			builder.HasOne(m => m.BlackPlayer)
				.WithMany()
				.HasForeignKey(m => m.BlackPlayerId)
				.OnDelete(DeleteBehavior.Restrict);

			builder.HasOne(m => m.WhitePlayer)
				.WithMany()
				.HasForeignKey(m => m.WhitePlayerId)
				.OnDelete(DeleteBehavior.Restrict);

			builder.Property(p => p.BlackPlayerId)
				.HasColumnName("Black_Player_Id");

			builder.Property(p => p.WhitePlayerId)
				.HasColumnName("White_Player_Id");

			builder.Property(p => p.Result)
				.IsRequired()
				.HasConversion<string>()
				.HasMaxLength(20);

			builder.Property(p => p.StartedAtUtc)
				.IsRequired()
				.HasColumnName("Started_At_Utc");

			builder.Property(p => p.EndedAtUtc)
				.IsRequired()
				.HasColumnName("Ended_At_Utc");

			builder.HasMany(m => m.Moves)
				.WithOne()
				.HasForeignKey(mv => mv.MatchId)
				.OnDelete(DeleteBehavior.Cascade);

			builder.HasIndex(m => m.BlackPlayerId);
			builder.HasIndex(m => m.WhitePlayerId);
		}
	}
}
