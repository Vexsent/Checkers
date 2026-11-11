using Checkers.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Checkers.Persistence.Data.Configurations
{
	public class MatchRecordEntityConfiguration : IEntityTypeConfiguration<MatchRecord>
	{
		public void Configure(EntityTypeBuilder<MatchRecord> builder)
		{
			builder.ToTable("Match_Records", o => o.ExcludeFromMigrations());

			builder.HasKey(k => k.Id)
				.HasName("PK_Match_Records_Id");

			// Restrict, not cascade: SQL Server rejects two cascade paths from Users into Match_Records,
			// and deleting a user shouldn't silently erase their opponent's history.
			builder.HasOne(m => m.BlackPlayer)
				.WithMany()
				.HasForeignKey(m => m.BlackPlayerId)
				.OnDelete(DeleteBehavior.Restrict)
				.HasConstraintName("FK_Match_Records_Black_Player_Id");

			builder.HasOne(m => m.WhitePlayer)
				.WithMany()
				.HasForeignKey(m => m.WhitePlayerId)
				.OnDelete(DeleteBehavior.Restrict)
				.HasConstraintName("FK_Match_Records_White_Player_Id");

			// Match history looks matches up by player, so index both player columns (not unique:
			// a player has many matches as each colour).
			builder.HasIndex(m => m.BlackPlayerId, "IX_Match_Records_Black_Player_Id");

			builder.HasIndex(m => m.WhitePlayerId, "IX_Match_Records_White_Player_Id");

			// Nullable: a null player id means the computer opponent played that side.
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
				.HasConstraintName("FK_Match_Record_Id")
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}
