using Assignment1.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Assignment1.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Assignment1.Services;
using Microsoft.AspNetCore.HttpOverrides;

namespace Assignment1.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Obituary> Obituaries => Set<Obituary>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Obituary>(e =>
        {
            e.ToTable("Obituaries");

            e.HasKey(x => x.Id);

            e.Property(x => x.FullName)
                .HasMaxLength(200)
                .IsRequired();

            e.Property(x => x.Biography)
                .IsRequired();

            // DateOnly maps to TEXT (ISO yyyy-MM-dd) for SQLite.
            e.Property(x => x.DateOfBirth)
                .HasConversion<DateOnlyConverter, DateOnlyComparer>()
                .IsRequired();

            e.Property(x => x.DateOfDeath)
                .HasConversion<DateOnlyConverter, DateOnlyComparer>()
                .IsRequired();

            // Base64 data URL string
            e.Property(x => x.PrimaryPhotoBase64)
                .HasColumnType("TEXT");

            // CreatedAtUtc as ISO-8601 string in SQLite
            e.Property(x => x.CreatedAtUtc)
                .HasConversion(
                    v => v,               // EF handles DateTime → TEXT for SQLite
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            // FK to Identity user
            e.Property(x => x.CreatedByUserId)
                .IsRequired();

            e.HasIndex(x => x.FullName);
            e.HasIndex(x => x.CreatedByUserId);
            e.HasIndex(x => x.CreatedAtUtc);

            e.HasOne<IdentityUser>()                // no navigation on Obituary needed
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed the database with sample data
        b.Seed();
    }
}


public sealed class DateOnlyConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateOnly, string>
{
    public DateOnlyConverter() : base(
        d => d.ToString("yyyy-MM-dd"),
        s => DateOnly.Parse(s))
    { }
}

public sealed class DateOnlyComparer : Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<DateOnly>
{
    public DateOnlyComparer() : base(
        (d1, d2) => d1.DayNumber == d2.DayNumber,
        d => d.GetHashCode())
    { }
}
