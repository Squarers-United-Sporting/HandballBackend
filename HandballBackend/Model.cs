using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using HandballBackend.Database.Models;
using HandballBackend.EndpointHelpers;
using HandballBackend.FixtureGenerator;
using HandballBackend.Utils;
using static System.Enum;

namespace HandballBackend;

public class HandballContext : DbContext {
    public DbSet<Game> Games { get; set; }
    public DbSet<GameEvent> GameEvents { get; set; }
    public DbSet<Official> Officials { get; set; }
    public DbSet<Person> People { get; set; }
    public DbSet<PlayerGameStats> PlayerGameStats { get; set; }
    public DbSet<QuoteOfTheDay> QuotesOfTheDay { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<Tournament> Tournaments { get; set; }
    public DbSet<TournamentOfficial> TournamentOfficials { get; set; }
    public DbSet<TournamentTeam> TournamentTeams { get; set; }

    public static string ConnectionString => File.ReadAllText(Config.SECRETS_FOLDER + "/DatabaseConnection.txt");

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PlayerGameStats>()
            .HasOne(pgs => pgs.Team)
            .WithMany(t => t.PlayerGameStats)
            .HasForeignKey(pgs => pgs.TeamId);

        modelBuilder.Entity<Game>()
            .HasOne(g => g.Official)
            .WithMany(o => o.Games)
            .HasForeignKey(g => g.OfficialId);

        modelBuilder.Entity<Game>()
            .HasOne(g => g.Scorer)
            .WithMany(o => o.ScoredGames)
            .HasForeignKey(g => g.ScorerId);

        modelBuilder.Entity<PlayerGameStats>()
            .HasOne(pgs => pgs.Opponent)
            .WithMany()
            .HasForeignKey(pgs => pgs.OpponentId);
        modelBuilder
            .Entity<GameEvent>()
            .Property(e => e.EventType)
            .HasConversion(
                v => Utilities.SplitCamelCase(v.ToString()),
                v => Parse<GameEventType>(v.Replace(" ", "")));
        modelBuilder
            .Entity<TournamentOfficial>()
            .Property(e => e.Role)
            .HasConversion(
                v => Utilities.SplitCamelCase(v.ToString()),
                v => Parse<OfficialRole>(v.Replace(" ", "")));
        modelBuilder
            .Entity<Person>()
            .Property(e => e.PermissionLevel)
            .HasConversion(
                v => Utilities.SplitCamelCase(v.ToString()),
                v => Parse<PermissionType>(v.Replace(" ", "")));
        modelBuilder
            .Entity<Person>()
            .Property(e => e.PhoneNumber)
            .HasConversion(
                v => v == null ? null : EncryptionHelper.Encrypt(v),
                v => v == null ? null : EncryptionHelper.Decrypt(v));
        modelBuilder
            .Entity<GameEvent>()
            .HasOne(gE => gE.Player)
            .WithMany(p => p.Events
            );
        modelBuilder
            .Entity<GameEvent>()
            .HasOne(gE => gE.Game)
            .WithMany(g => g.Events
            );
        modelBuilder
            .Entity<GameEvent>()
            .HasOne(gE => gE.TeamOneLeft);

        modelBuilder
            .Entity<TournamentTeam>()
            .HasOne(gE => gE.Team)
            .WithMany(g => g.TournamentTeams)
            .HasForeignKey(g => g.TeamId);
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder options) {
        if (Config.USING_POSTGRES) {
            options.UseNpgsql(ConnectionString);
        } else {
            options.UseSqlite(ConnectionString);
        }
    }
}