using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RandomVideoCallWebpage.Models;

namespace RandomVideoCallWebpage.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();

    public DbSet<Friendship> Friendships => Set<Friendship>();

    public DbSet<FriendMessage> FriendMessages => Set<FriendMessage>();

    public DbSet<FriendCall> FriendCalls => Set<FriendCall>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<FriendRequest>(entity =>
        {
            entity.HasIndex(request => new { request.FromUserId, request.ToUserId });
            entity.HasIndex(request => request.ToUserId);
            entity.Property(request => request.FromUserId).HasMaxLength(450);
            entity.Property(request => request.ToUserId).HasMaxLength(450);
            entity.HasOne(request => request.FromUser)
                .WithMany()
                .HasForeignKey(request => request.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(request => request.ToUser)
                .WithMany()
                .HasForeignKey(request => request.ToUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Friendship>(entity =>
        {
            entity.HasIndex(friendship => new { friendship.UserAId, friendship.UserBId }).IsUnique();
            entity.Property(friendship => friendship.UserAId).HasMaxLength(450);
            entity.Property(friendship => friendship.UserBId).HasMaxLength(450);
        });

        builder.Entity<FriendMessage>(entity =>
        {
            entity.HasIndex(message => new { message.SenderId, message.ReceiverId });
            entity.HasIndex(message => message.ReceiverId);
            entity.Property(message => message.SenderId).HasMaxLength(450);
            entity.Property(message => message.ReceiverId).HasMaxLength(450);
            entity.Property(message => message.Body).HasMaxLength(2000);
        });

        builder.Entity<FriendCall>(entity =>
        {
            entity.HasIndex(call => call.ReceiverId);
            entity.Property(call => call.CallerId).HasMaxLength(450);
            entity.Property(call => call.ReceiverId).HasMaxLength(450);
        });
    }
}
