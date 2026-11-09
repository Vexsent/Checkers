using Checkers.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Checkers.Persistence.Data.Configurations
{
	public class MatchRecordMoveEntityConfiguration : IEntityTypeConfiguration<MatchRecordMove>
	{
		public void Configure(EntityTypeBuilder<MatchRecordMove> builder)
		{
			builder.ToTable("Match_Record_Moves");

			builder.HasKey(k => k.Id);

			builder.Property(p => p.MatchId)
				.IsRequired()
				.HasColumnName("Match_Record_Id");

			builder.Property(p => p.MoveNumber)
				.IsRequired()
				.HasColumnName("Move_Number");

			builder.Property(p => p.FromRow)
				.IsRequired()
				.HasColumnName("From_Row");

			builder.Property(p => p.FromCol)
				.IsRequired()
				.HasColumnName("From_Col");

			builder.Property(p => p.ToRow)
				.IsRequired()
				.HasColumnName("To_Row");

			builder.Property(p => p.ToCol)
				.IsRequired()
				.HasColumnName("To_Col");

			builder.HasIndex(mv => new { mv.MatchId, mv.MoveNumber })
				.IsUnique();
		}
	}
}
