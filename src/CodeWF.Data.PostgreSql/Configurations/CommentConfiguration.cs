﻿namespace CodeWF.Data.PostgreSql.Configurations;

internal class CommentConfiguration : IEntityTypeConfiguration<CommentEntity>
{
    public void Configure(EntityTypeBuilder<CommentEntity> builder)
    {
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.CommentContent).IsRequired();
        builder.Property(e => e.CreateTimeUtc).HasColumnType("timestamp");
        builder.Property(e => e.Email).HasMaxLength(128);
        builder.Property(e => e.IPAddress).HasMaxLength(64);
        builder.Property(e => e.Username).HasMaxLength(64);
        builder.HasOne(d => d.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(d => d.PostId)
            .HasConstraintName("FK_Comment_Post");
    }
}