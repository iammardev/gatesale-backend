using Microsoft.EntityFrameworkCore;
using GateSale.Core.Entities;

namespace GateSale.Infrastructure.Data
{
    public class GateSaleDbContext : DbContext
    {
        public GateSaleDbContext(DbContextOptions<GateSaleDbContext> options) : base(options) {}

        public DbSet<User> Users { get; set; }
        public DbSet<UserDevice> UserDevices { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Dispute> Disputes { get; set; }
        public DbSet<Locker> Lockers { get; set; }
        public DbSet<UserLocker> UserLockers { get; set; }
        public DbSet<OrderTrackingEvent> OrderTrackingEvents { get; set; }
        public DbSet<PudoWebhookLog> PudoWebhookLogs { get; set; }
        public DbSet<EmailVerification> EmailVerifications { get; set; }
        public DbSet<ParentalConsent> ParentalConsents { get; set; }
        public DbSet<WhitelistedDomain> WhitelistedDomains { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Configure entity relationships and constraints
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
                
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();
                
            modelBuilder.Entity<User>()
                .HasIndex(u => u.CognitoUserId)
                .IsUnique();
                
            modelBuilder.Entity<ParentalConsent>()
                .HasOne(pc => pc.User)
                .WithMany()
                .HasForeignKey(pc => pc.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Seller)
                .WithMany(u => u.Products)
                .HasForeignKey(p => p.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<ProductImage>()
                .HasOne(pi => pi.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Buyer)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.BuyerId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Order)
                .WithOne(o => o.Transaction)
                .HasForeignKey<Transaction>(t => t.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<Dispute>()
                .HasOne(d => d.Order)
                .WithOne(o => o.Dispute)
                .HasForeignKey<Dispute>(d => d.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<UserDevice>()
                .HasOne(ud => ud.User)
                .WithMany(u => u.Devices)
                .HasForeignKey(ud => ud.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<UserLocker>()
                .HasOne(ul => ul.User)
                .WithMany()
                .HasForeignKey(ul => ul.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<UserLocker>()
                .HasOne(ul => ul.Locker)
                .WithMany()
                .HasForeignKey(ul => ul.LockerId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<UserLocker>()
                .HasIndex(ul => new { ul.UserId, ul.LockerId })
                .IsUnique();
                
            modelBuilder.Entity<OrderTrackingEvent>()
                .HasOne(ote => ote.Order)
                .WithMany()
                .HasForeignKey(ote => ote.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
