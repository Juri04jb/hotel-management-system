using HotelManagementApp.Models;
using HotelManagementApp.Services;
using HotelManagementApp.Services.Authentication;
using HotelManagementApp.Services.CleaningTaskSpace;
using HotelManagementApp.Services.FeedbackServiceSpace;
using HotelManagementApp.Services.InvoiceSpace;
using HotelManagementApp.Services.MaintenanceRequest;
using HotelManagementApp.Services.Payment;
using HotelManagementApp.Services.ReservationServiceSpace;
using HotelManagementApp.Services.RoomServiceSpace;
using HotelManagementApp.Models.DTOs.RoomType;
using HotelManagementApp.Services.ServiceOrder;
using HotelManagementApp.Services.ServiceService;
using HotelManagementApp.Services.Statistics;
using HotelManagementApp.Services.UserManagement;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

namespace HotelManagementApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);

            Console.WriteLine("Starting application initialization...");

            // Configure DbContext
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    "Server=DESKTOP-VEC9Q0R;Database=HotelManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True",
                    sqlServerOptionsAction: sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    }
                ));

            Console.WriteLine("Using connection string for server: DESKTOP-VEC9Q0R");

            // Configure CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            // Configure Identity
            builder.Services.AddIdentity<ApplicationUser, ApplicationRole>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();

            // Configure JWT Authentication
            var jwtKey = builder.Configuration["Jwt:Key"] ?? "DefaultKeyForDevelopmentThatIsAtLeast32CharsLong";
            var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "http://localhost:7138";
            var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "http://localhost:5500";

            var key = Encoding.ASCII.GetBytes(jwtKey);

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    // This line ensures ASP.NET Core recognizes your role claim!
                    RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
                    NameClaimType = JwtRegisteredClaimNames.Sub
                };
            });

            // Register services
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IFeedbackService, FeedbackService>();
            builder.Services.AddScoped<ICleaningService, CleaningService>();
            builder.Services.AddScoped<IInvoiceService, InvoiceService>();
            builder.Services.AddScoped<IMaintenanceRequestService, MaintenanceRequestService>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            builder.Services.AddScoped<IReservationService, ReservationService>();
            builder.Services.AddScoped<RoomService>();
            builder.Services.AddScoped<IServiceService, ServiceService>();
            builder.Services.AddScoped<IServiceOrderService, ServiceOrderService>();
            builder.Services.AddScoped<IStatisticsService, StatisticsService>();
         
  

            // Add controllers
            builder.Services.AddControllers();

            // Add Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure middleware
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                Console.WriteLine("Configured Swagger for development environment");
            }

            app.UseHttpsRedirection();

            app.UseCors("CorsPolicy");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            // Initialize the database
            try
            {
                using (var scope = app.Services.CreateScope())
                {
                    var services = scope.ServiceProvider;

                    var dbContext = services.GetRequiredService<AppDbContext>();
                    Console.WriteLine("Testing database connection...");

                    var canConnect = dbContext.Database.CanConnectAsync().GetAwaiter().GetResult();
                    Console.WriteLine($"Database connection test result: {canConnect}");

                    if (!canConnect)
                    {
                        Console.WriteLine("WARNING: Cannot connect to database. Check your connection string.");

                        if (app.Environment.IsDevelopment())
                        {
                            Console.WriteLine("Attempting to create database...");
                            dbContext.Database.EnsureCreated();
                            Console.WriteLine("Database creation attempted");
                        }
                    }

                    Console.WriteLine("Initializing database...");
                    RunTestDataSeeder(app);
                    Console.WriteLine("Database initialized successfully");
                }

                Console.WriteLine("Application initialization completed, starting web server...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Startup error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);

                if (!app.Environment.IsDevelopment())
                {
                    return;
                }
            }

            app.Run();
        }

        private static void RunTestDataSeeder(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                var context = services.GetRequiredService<AppDbContext>();
                var logger = services.GetRequiredService<ILogger<Program>>();

                context.Database.EnsureCreated();

                TestDataSeeder.SeedTestData(services, logger).GetAwaiter().GetResult();

                logger.LogInformation("Test data seeding completed successfully.");
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred while seeding the test data.");
            }
        }
    }
}