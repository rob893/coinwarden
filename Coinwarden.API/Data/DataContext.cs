using System;
using Coinwarden.API.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Coinwarden.API.Data;

/// <summary>
/// The main Entity Framework database context for Coinwarden.
/// </summary>
public sealed class DataContext : IdentityDbContext<User, Role, int,
    IdentityUserClaim<int>, UserRole, IdentityUserLogin<int>,
    IdentityRoleClaim<int>, IdentityUserToken<int>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataContext"/> class.
    /// </summary>
    /// <param name="options">The database context options.</param>
    public DataContext(DbContextOptions<DataContext> options) : base(options) { }

    /// <summary>Gets the refresh tokens DbSet.</summary>
    public DbSet<RefreshToken> RefreshTokens => this.Set<RefreshToken>();

    /// <summary>Gets the linked accounts DbSet.</summary>
    public DbSet<LinkedAccount> LinkedAccounts => this.Set<LinkedAccount>();

    /// <summary>Gets the notes DbSet.</summary>
    public DbSet<Note> Notes => this.Set<Note>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.Entity<UserRole>(userRole =>
        {
            userRole.HasKey(ur => new { ur.UserId, ur.RoleId });

            userRole.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .IsRequired();

            userRole.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .IsRequired();
        });

        builder.Entity<RefreshToken>(rToken =>
        {
            rToken.HasKey(k => new { k.UserId, k.DeviceId });
            rToken.HasIndex(rt => rt.DeviceId);
        });

        builder.Entity<LinkedAccount>(linkedAccount =>
        {
            linkedAccount.HasKey(account => new { account.Id, account.LinkedAccountType });
            linkedAccount.Property(account => account.LinkedAccountType).HasConversion<string>();
        });

        builder.Entity<Note>(note =>
        {
            note.Property(n => n.CreatedAt).HasDefaultValueSql("now()");
            note.Property(n => n.UpdatedAt).HasDefaultValueSql("now()");

            note.HasOne(n => n.User)
                .WithMany(u => u.Notes)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
