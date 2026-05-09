using Microsoft.EntityFrameworkCore;
using SmartPlanner.Models.Enums;

namespace SmartPlanner.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();

        public DbSet<Activity> Activities => Set<Activity>();

        public DbSet<TaskItem> Tasks => Set<TaskItem>();

        public DbSet<Event> Events => Set<Event>();

        public DbSet<LifeArea> LifeAreas => Set<LifeArea>();

        public DbSet<Location> Locations => Set<Location>();

        public DbSet<ScheduleEntry> ScheduleEntries => Set<ScheduleEntry>();

        public DbSet<UserPreference> UserPreferences => Set<UserPreference>();

        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

        public DbSet<MLTrainingData> MLTrainingData => Set<MLTrainingData>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Activity>().UseTptMappingStrategy();

            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

            modelBuilder
                .Entity<Activity>()
                .HasOne(a => a.User)
                .WithMany(u => u.Activities)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Activity>()
                .HasOne(a => a.LifeArea)
                .WithMany(l => l.Activities)
                .HasForeignKey(a => a.LifeAreaId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder
                .Entity<Event>()
                .HasOne(e => e.Location)
                .WithMany(l => l.Events)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder
                .Entity<ScheduleEntry>()
                .HasOne(se => se.User)
                .WithMany(u => u.ScheduleEntries)
                .HasForeignKey(se => se.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<ScheduleEntry>()
                .HasOne(se => se.Activity)
                .WithMany(a => a.ScheduleEntries)
                .HasForeignKey(se => se.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<UserPreference>()
                .HasOne(up => up.User)
                .WithOne(u => u.UserPreference)
                .HasForeignKey<UserPreference>(up => up.UserId);

            modelBuilder
                .Entity<MLTrainingData>()
                .HasOne(m => m.TaskItem)
                .WithMany(t => t.MLTrainingData)
                .HasForeignKey(m => m.TaskItemId);

            modelBuilder
                .Entity<ScheduleEntry>()
                .Property(se => se.ConfidenceScore)
                .HasPrecision(5, 4);

            modelBuilder
                .Entity<MLTrainingData>()
                .Property(m => m.PredictionError)
                .HasPrecision(10, 2);

            modelBuilder.Entity<TaskItem>().HasIndex(t => t.Deadline);

            modelBuilder.Entity<ScheduleEntry>().HasIndex(se => se.StartAt);

            modelBuilder.Entity<Event>().HasIndex(e => e.StartTime);

            modelBuilder
                .Entity<ScheduleEntry>()
                .HasIndex(se => new
                {
                    se.UserId,
                    se.StartAt,
                    se.EndAt,
                });

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType
                    .GetProperties()
                    .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?));
                foreach (var property in properties)
                {
                    property.SetColumnType("timestamp with time zone");
                }
            }
        }
    }
}
