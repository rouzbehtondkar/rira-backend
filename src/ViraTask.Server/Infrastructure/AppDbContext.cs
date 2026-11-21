using Microsoft.EntityFrameworkCore;
using ViraTask.Server.Domain;

namespace ViraTask.Server.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var employee = modelBuilder.Entity<Employee>();

        employee.ToTable("Employees");
        employee.HasKey(x => x.Id);

        employee.Property(x => x.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        employee.Property(x => x.LastName)
            .HasMaxLength(100)
            .IsRequired();

        employee.Property(x => x.NationalCode)
            .HasMaxLength(20)
            .IsRequired();

        employee.HasIndex(x => x.NationalCode)
            .IsUnique();

        employee.Property(x => x.BirthDate)
            .HasColumnType("date")
            .IsRequired();
    }
}
