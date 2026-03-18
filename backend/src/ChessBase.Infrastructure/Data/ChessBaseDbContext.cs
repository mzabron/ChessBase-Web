using Microsoft.EntityFrameworkCore;
using ChessBase.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace ChessBase.Infrastructure.Data;

public class ChessBaseDbContext : IdentityDbContext<ApplicationUser>
{
    public ChessBaseDbContext(DbContextOptions<ChessBaseDbContext> options)
        : base(options)
    {
    }

    public DbSet<Game> Games { get; set; }
    public DbSet<Player> Players { get; set; }
    public DbSet<Move> Moves { get; set; }
    public DbSet<Position> Positions { get; set; }
    public DbSet<UserDatabase> UserDatabases { get; set; }
    public DbSet<UserDatabaseGame> UserDatabaseGames { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasIndex(p => p.NormalizedFullName).IsUnique();
            entity.HasIndex(p => p.NormalizedFirstName);
            entity.HasIndex(p => p.NormalizedLastName);
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasIndex(g => new { g.Year, g.Id });
            entity.HasIndex(g => g.MoveCount);

            entity
                .HasOne(g => g.WhitePlayer)
                .WithMany(p => p.GamesAsWhite)
                .HasForeignKey(g => g.WhitePlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity
                .HasOne(g => g.BlackPlayer)
                .WithMany(p => p.GamesAsBlack)
                .HasForeignKey(g => g.BlackPlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Move>(entity =>
        {
            entity
                .HasOne(m => m.Game)
                .WithMany(g => g.Moves)
                .HasForeignKey(m => m.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasIndex(p => p.FenHash);
            entity.HasIndex(p => new {p.GameId, p.PlyCount });
            entity
                .HasOne(p => p.Game)
                .WithMany(g => g.Positions)
                .HasForeignKey(p => p.GameId);

        });

        modelBuilder.Entity<UserDatabase>(entity =>
        {
            entity.Property(d => d.Name).HasMaxLength(200).IsRequired();
            entity.Property(d => d.OwnerUserId).IsRequired();
            entity.Property(d => d.CreatedAtUtc).IsRequired();

            entity.HasIndex(d => new { d.OwnerUserId, d.Name }).IsUnique();
            entity.HasIndex(d => d.IsPublic);

            entity
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserDatabaseGame>(entity =>
        {
            entity.HasKey(x => new { x.UserDatabaseId, x.GameId });
            entity.Property(x => x.AddedAtUtc).IsRequired();

            entity
                .HasOne(x => x.UserDatabase)
                .WithMany(d => d.UserDatabaseGames)
                .HasForeignKey(x => x.UserDatabaseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(x => x.Game)
                .WithMany(g => g.UserDatabaseGames)
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}