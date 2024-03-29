﻿namespace CodeWF.Data.PostgreSql.Configurations;

internal class CommentReplyConfiguration : IEntityTypeConfiguration<CommentReplyEntity>
{
    public void Configure(EntityTypeBuilder<CommentReplyEntity> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.CreateTimeUtc).HasColumnType("timestamp");
        builder.HasOne(d => d.Comment)
            .WithMany(p => p.Replies)
            .HasForeignKey(d => d.CommentId);
    }
}