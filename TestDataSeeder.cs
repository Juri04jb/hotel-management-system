using HotelManagementApp.Models.Enums;
using HotelManagementApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using HotelManagementApp.Models.Enums;
using HotelManagementApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public static class TestDataSeeder
{
    public static async Task SeedTestData(IServiceProvider serviceProvider, ILogger logger, bool forceReseed = true)
    {
        logger.LogInformation("Starting test data seeding...");

        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;

        try
        {
            var context = scopedServices.GetRequiredService<AppDbContext>();
            var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scopedServices.GetRequiredService<RoleManager<ApplicationRole>>();

            // Check if we need to clear existing data
            if (forceReseed)
            {
                await ClearExistingData(context, logger);
            }

            // Check if roles exist, if not create them
            await EnsureRolesExist(roleManager, logger);

            // Create test users if needed
            var users = await CreateTestUsers(userManager, logger);

            if (users.Count == 0)
            {
                logger.LogError("Failed to create any users. Aborting seeding process.");
                return;
            }

            // Create/ensure room types exist
            var roomTypes = await EnsureRoomTypesExist(context, logger);

            // Create rooms
            var rooms = await CreateTestRooms(context, roomTypes, logger);

            // Create services
            var services = await CreateTestServices(context, logger);

            // Create reservations with various statuses
            var reservations = await CreateTestReservations(context, users, rooms, roomTypes, logger);


            // Create service orders for reservations
            await CreateTestServiceOrders(context, reservations, services, users, logger);
            // Create invoices and payments
            await CreateTestInvoicesAndPayments(context, reservations, users, logger);

            

            // Create cleaning tasks
            await CreateTestCleaningTasks(context, rooms, users, logger);

            // Create maintenance requests
            await CreateTestMaintenanceRequests(context, rooms, users, logger);

            // Create feedback
            await CreateTestFeedback(context, reservations, users, logger);

            // Create reports
            await CreateTestReports(context, users, logger);

            logger.LogInformation("Test data seeding completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding test data.");
            throw;
        }
    }

    private static async Task ClearExistingData(AppDbContext context, ILogger logger)
    {
        logger.LogInformation("Force reseed requested. Clearing existing data...");

        try
        {
            // Clear data in reverse order of dependencies
            if (context.Reports.Any())
            {
                context.Reports.RemoveRange(context.Reports);
                await context.SaveChangesAsync();
                logger.LogInformation("Cleared existing reports");
            }

            if (context.Feedback.Any())
            {
                context.Feedback.RemoveRange(context.Feedback);
                await context.SaveChangesAsync();
                logger.LogInformation("Cleared existing feedback");
            }

            if (context.MaintenanceRequests.Any())
            {
                context.MaintenanceRequests.RemoveRange(context.MaintenanceRequests);
                await context.SaveChangesAsync();
                logger.LogInformation("Cleared existing maintenance requests");
            }

            if (context.CleaningTasks.Any())
            {
                context.CleaningTasks.RemoveRange(context.CleaningTasks);
                await context.SaveChangesAsync();
                logger.LogInformation("Cleared existing cleaning tasks");
            }

            if (context.ServiceOrders.Any())
            {
                context.ServiceOrders.RemoveRange(context.ServiceOrders);
                await context.SaveChangesAsync();
                logger.LogInformation("Cleared existing service orders");
            }

            if (context.Payments.Any())
            {
                context.Payments.RemoveRange(context.Payments);
                await context.SaveChangesAsync();
                logger.LogInformation("Cleared existing payments");
            }

            if (context.Invoices.Any())
            {
                context.Invoices.RemoveRange(context.Invoices);
                await context.SaveChangesAsync();
                logger.LogInformation("Cleared existing invoices");
            }

            if (context.Reservations.Any())
            {
                context.Reservations.RemoveRange(context.Reservations);
                await context.SaveChangesAsync();
                logger.LogInformation("Cleared existing reservations");
            }

            if (context.Services.Any())
            {
                context.Services.RemoveRange(context.Services);
                await context.SaveChangesAsync();
                logger.LogInformation("Cleared existing services");
            }

            if (context.Rooms.Any())
            {
                // Reset room status before deletion to avoid FK constraints
                foreach (var room in context.Rooms)
                {
                    room.Status = RoomStatus.Available;
                    room.CleanedById = null;
                }
                await context.SaveChangesAsync();

                context.Rooms.RemoveRange(context.Rooms);
                await context.SaveChangesAsync();
                logger.LogInformation("Cleared existing rooms");
            }

            if (context.RoomTypes.Any())
            {
                context.RoomTypes.RemoveRange(context.RoomTypes);
                await context.SaveChangesAsync();
                logger.LogInformation("Cleared existing room types");
            }

            // Note: We don't clear users and roles to preserve existing accounts
            logger.LogInformation("Data clearing completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while clearing existing data");
            throw;
        }
    

    
    }


private static async Task EnsureRolesExist(RoleManager<ApplicationRole> roleManager, ILogger logger)
    {
        logger.LogInformation("Ensuring roles exist...");

        string[] roleNames = { "Admin", "Manager", "Receptionist", "Housekeeper", "Guest" };
        string[] descriptions = {
            "System administrator with full access",
            "Hotel management and administrative access",
            "Front desk operations and guest services",
            "Room cleaning and maintenance",
            "Hotel guest with booking capabilities"
        };

        for (int i = 0; i < roleNames.Length; i++)
        {
            var roleName = roleNames[i];
            var roleDescription = descriptions[i];

            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new ApplicationRole
                {
                    Name = roleName,
                    NormalizedName = roleName.ToUpper(),
                    Description = roleDescription
                });

                if (roleResult.Succeeded)
                {
                    logger.LogInformation("Created role: {RoleName}", roleName);
                }
                else
                {
                    var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                    logger.LogError("Failed to create role {RoleName}: {Errors}", roleName, errors);
                }
            }
        }
    }

    private static async Task<List<ApplicationUser>> CreateTestUsers(UserManager<ApplicationUser> userManager, ILogger logger)
    {
        logger.LogInformation("Creating test users...");

        var users = new List<ApplicationUser>();
        var testUsers = new List<(ApplicationUser User, string Password, string Role)>
        {
            (new ApplicationUser
            {
                UserName = "admin@hotel.com",
                Email = "admin@hotel.com",
                FirstName = "Admin",
                LastName = "User",
                EmailConfirmed = true,
                PhoneNumber = "1000000000",
                IsActive = true,
                AccountStatus = AccountStatus.Active,
                Created = DateTime.UtcNow,
                RegistrationDate = DateTime.UtcNow,
                HireDate = DateTime.UtcNow.AddYears(-3),
                IdType = "Employee ID",
                IdNumber = "EMP-456789"
            }, "Admin123!", "Admin"),

            (new ApplicationUser
            {
                UserName = "manager@hotel.com",
                Email = "manager@hotel.com",
                FirstName = "John",
                LastName = "Manager",
                EmailConfirmed = true,
                PhoneNumber = "2000000000",
                IsActive = true,
                AccountStatus = AccountStatus.Active,
                Created = DateTime.UtcNow,
                RegistrationDate = DateTime.UtcNow,
                Address = "123 Main St",
                City = "New York",
                State = "NY",
                Country = "USA",
                PostalCode = "10001",
                HireDate = DateTime.UtcNow.AddYears(-2),
                IdType = "Employee ID",
                IdNumber = "EMP-256789"
            }, "Manager123!", "Manager"),

            (new ApplicationUser
            {
                UserName = "receptionist@hotel.com",
                Email = "receptionist@hotel.com",
                FirstName = "Alice",
                LastName = "Receptionist",
                EmailConfirmed = true,
                PhoneNumber = "3000000000",
                IsActive = true,
                AccountStatus = AccountStatus.Active,
                Created = DateTime.UtcNow,
                RegistrationDate = DateTime.UtcNow,
                HireDate = DateTime.UtcNow.AddMonths(-9),
                IdType = "Employee ID",
                IdNumber = "EMP-356789"
            }, "Receptionist123!", "Receptionist"),

            (new ApplicationUser
            {
                UserName = "housekeeper@hotel.com",
                Email = "housekeeper@hotel.com",
                FirstName = "Bob",
                LastName = "Housekeeper",
                EmailConfirmed = true,
                PhoneNumber = "4000000000",
                IsActive = true,
                AccountStatus = AccountStatus.Active,
                Created = DateTime.UtcNow,
                RegistrationDate = DateTime.UtcNow,
                HireDate = DateTime.UtcNow.AddMonths(-6),
                IdType = "Employee ID",
                IdNumber = "EMP-456789"
            }, "Housekeeper123!", "Housekeeper"),
        };

        // Create 5 guest users
        for (int i = 1; i <= 5; i++)
        {
            testUsers.Add((new ApplicationUser
            {
                UserName = $"guest{i}@example.com",
                Email = $"guest{i}@example.com",
                FirstName = $"Guest{i}",
                LastName = $"User{i}",
                EmailConfirmed = true,
                PhoneNumber = $"5{i}00000000",
                IsActive = true,
                AccountStatus = AccountStatus.Active,
                Created = DateTime.UtcNow.AddDays(-i * 10),
                RegistrationDate = DateTime.UtcNow.AddDays(-i * 10),
                Address = $"{i}01 Guest St",
                City = "Boston",
                State = "MA",
                Country = "USA",
                PostalCode = "02101",
                IdType = i % 2 == 0 ? "Passport" : "Driver License",
                IdNumber = $"ID{i}00000"
            }, "Guest123!", "Guest"));
        }

        foreach (var (user, password, role) in testUsers)
        {
            var existingUser = await userManager.FindByEmailAsync(user.Email);
            if (existingUser == null)
            {
                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    var roleResult = await userManager.AddToRoleAsync(user, role);
                    if (roleResult.Succeeded)
                    {
                        logger.LogInformation("Created user {UserEmail} with role {Role}", user.Email, role);
                        users.Add(user);
                    }
                    else
                    {
                        logger.LogError("Failed to assign role to user {UserEmail}", user.Email);
                    }
                }
                else
                {
                    logger.LogError("Failed to create user {UserEmail}", user.Email);
                }
            }
            else
            {
                users.Add(existingUser);
                logger.LogInformation("User {UserEmail} already exists", user.Email);
            }
        }

        return users;
    }

    private static async Task<List<RoomType>> EnsureRoomTypesExist(AppDbContext context, ILogger logger)
    {
        logger.LogInformation("Ensuring room types exist...");

        if (!await context.RoomTypes.AnyAsync())
        {
            var roomTypes = new List<RoomType>
            {
                new RoomType
                {
                    Name = "Standard",
                    BasePrice = 100m,
                    Capacity = 2,
                    Description = "Comfortable standard room with basic amenities",
                    Amenities = "WiFi,TV,Air conditioning,Coffee maker",
                    ImageUrl = "https://example.com/images/standard.jpg"
                },
                new RoomType
                {
                    Name = "Deluxe",
                    BasePrice = 150m,
                    Capacity = 2,
                    Description = "Spacious room with premium amenities and city view",
                    Amenities = "WiFi,TV,Air conditioning,Mini-bar,Safe,Premium toiletries",
                    ImageUrl = "https://example.com/images/standard.jpg"
                },
                new RoomType
                {
                    Name = "Suite",
                    BasePrice = 250m,
                    Capacity = 4,
                    Description = "Luxury suite with separate living area and kitchen",
                    Amenities = "WiFi,TV,Air conditioning,Full kitchen,Living room,Balcony,Jacuzzi",
                    ImageUrl = "https://example.com/images/standard.jpg"
                },
                new RoomType
                {
                    Name = "Family",
                    BasePrice = 200m,
                    Capacity = 6,
                    Description = "Spacious room ideal for families with children",
                    Amenities = "WiFi,TV,Air conditioning,Mini-fridge,Extra beds,Child-friendly",
                    ImageUrl = "https://example.com/images/standard.jpg"

                },
                new RoomType
                {
                    Name = "Single",
                    BasePrice = 75m,
                    Capacity = 1,
                    Description = "Cozy room for single travelers",
                    Amenities = "WiFi,TV,Air conditioning,Desk,Single bed",
                    ImageUrl = "https://example.com/images/standard.jpg"
                }
            };

            await context.RoomTypes.AddRangeAsync(roomTypes);
            await context.SaveChangesAsync();
            logger.LogInformation("Added {Count} room types", roomTypes.Count);
            return roomTypes;
        }

        return await context.RoomTypes.ToListAsync();
    }

    private static async Task<List<Room>> CreateTestRooms(AppDbContext context, List<RoomType> roomTypes, ILogger logger)
    {
        logger.LogInformation("Creating test rooms...");

        // Check if rooms already exist
        if (await context.Rooms.AnyAsync())
        {
            logger.LogInformation("Rooms already exist, returning existing rooms");
            return await context.Rooms.ToListAsync();
        }

        // Create rooms with different statuses
        var rooms = new List<Room>();

        // Create rooms for each floor
        for (int floor = 1; floor <= 4; floor++)
        {
            // For each room type
            foreach (var roomType in roomTypes)
            {
                // Create 3 rooms per floor per type
                for (int i = 1; i <= 3; i++)
                {
                    var roomStatus = RoomStatus.Available;

                    // Set some rooms to different statuses for testing
                    if (i == 1 && floor == 1)
                        roomStatus = RoomStatus.Occupied;
                    else if (i == 2 && floor == 2)
                        roomStatus = RoomStatus.Maintenance;
                    else if (i == 3 && floor == 3)
                        roomStatus = RoomStatus.Reserved;

                    var room = new Room
                    {
                        RoomNumber = $"{floor}{roomType.Id}{i:D2}",
                        Floor = floor,
                        RoomTypeId = roomType.Id,
                        Status = roomStatus,
                        BasePrice = roomType.BasePrice,
                        NeedsCleaning = roomStatus == RoomStatus.Maintenance,
                        LastCleaned = DateTime.UtcNow.AddDays(-i),
                        Notes = $"{roomType.Name} room on floor {floor}"
                    };

                    rooms.Add(room);
                }
            }
        }

        await context.Rooms.AddRangeAsync(rooms);
        await context.SaveChangesAsync();
        logger.LogInformation("Added {Count} rooms", rooms.Count);
        return rooms;
    }

    private static async Task<List<Service>> CreateTestServices(AppDbContext context, ILogger logger)
    {
        logger.LogInformation("Creating test services...");

        if (await context.Services.AnyAsync())
        {
            logger.LogInformation("Services already exist, returning existing services");
            return await context.Services.ToListAsync();
        }

        var services = new List<Service>
        {
            new Service
            {
                ServiceName = "Room Cleaning",
                Description = "Standard room cleaning service",
                ServiceType = "Housekeeping",
                Price = 25.00m,
                IsActive = true
            },
            new Service
            {
                ServiceName = "Breakfast",
                Description = "Continental breakfast served in room",
                ServiceType = "Dining",
                Price = 15.00m,
                IsActive = true
            },
            new Service
            {
                ServiceName = "Laundry",
                Description = "Clothing laundry and ironing",
                ServiceType = "Housekeeping",
                Price = 35.00m,
                IsActive = true
            },
            new Service
            {
                ServiceName = "Massage",
                Description = "In-room massage service",
                ServiceType = "Wellness",
                Price = 80.00m,
                IsActive = true
            },
            new Service
            {
                ServiceName = "Airport Shuttle",
                Description = "Transportation to/from airport",
                ServiceType = "Transportation",
                Price = 50.00m,
                IsActive = true
            },
            new Service
            {
                ServiceName = "Mini Bar Restock",
                Description = "Restock mini bar with beverages and snacks",
                ServiceType = "Food & Beverage",
                Price = 40.00m,
                IsActive = true
            },
            new Service
            {
                ServiceName = "Late Check-out",
                Description = "Extended check-out time (until 3 PM)",
                ServiceType = "Accommodation",
                Price = 30.00m,
                IsActive = true
            }
        };

        await context.Services.AddRangeAsync(services);
        await context.SaveChangesAsync();
        logger.LogInformation("Added {Count} services", services.Count);
        return services;
    }

    private static async Task<List<Reservation>> CreateTestReservations(
        AppDbContext context,
        List<ApplicationUser> users,
        List<Room> rooms,
        List<RoomType> roomTypes,
        ILogger logger)
    {
        logger.LogInformation("Creating test reservations...");

        if (await context.Reservations.AnyAsync())
        {
            logger.LogInformation("Reservations already exist, returning existing reservations");
            return await context.Reservations.ToListAsync();
        }

        var reservations = new List<Reservation>();
        var random = new Random();

        // Get guest users
        var guests = users.Where(u => u.UserName.Contains("guest")).ToList();
        var staff = users.Where(u => !u.UserName.Contains("guest")).ToList();
        var manager = staff.FirstOrDefault(u => u.UserName.Contains("manager"));
        var receptionist = staff.FirstOrDefault(u => u.UserName.Contains("receptionist"));

        if (guests.Count == 0 || staff.Count == 0 || manager == null || receptionist == null)
        {
            logger.LogError("Missing required users for reservation creation");
            return reservations;
        }

        // 1. Create future confirmed reservations (5)
        for (int i = 0; i < 5; i++)
        {
            var checkInDate = DateTime.UtcNow.AddDays(random.Next(1, 30));
            var checkOutDate = checkInDate.AddDays(random.Next(1, 7));
            var guest = guests[random.Next(guests.Count)];
            var roomType = roomTypes[random.Next(roomTypes.Count)];

            // Find available rooms of the selected type
            var availableRooms = rooms.Where(r =>
                r.RoomTypeId == roomType.Id &&
                r.Status == RoomStatus.Available).ToList();

            if (availableRooms.Count == 0)
            {
                logger.LogWarning("No available rooms for room type {RoomTypeId}, skipping", roomType.Id);
                continue;
            }

            var room = availableRooms[random.Next(availableRooms.Count)];

            var reservation = new Reservation
            {
                ReservationNumber = $"RES{DateTime.UtcNow:yyyyMMdd}{i + 1:D4}",
                UserId = guest.Id,
                RoomId = i % 2 == 0 ? room.Id : null, // Some with assigned room, some without
                RoomTypeId = roomType.Id,
                CheckInDate = checkInDate,
                CheckOutDate = checkOutDate,
                Status = ReservationStatus.Confirmed,
                ReservationDate = DateTime.UtcNow.AddDays(-random.Next(1, 7)),
                NumberOfGuests = random.Next(1, roomType.Capacity + 1),
                TotalPrice = roomType.BasePrice * (decimal)(checkOutDate - checkInDate).TotalDays,
                PaymentMethod = (PaymentMethod)random.Next(0, Enum.GetValues(typeof(PaymentMethod)).Length),
                PaymentStatus = PaymentStatus.Pending,
                SpecialRequests = random.Next(3) > 0 ? "No special requests" : "Late check-in, please keep reservation",
                CreatedBy = manager.Id,
                ServiceOrders = new List<ServiceOrder>(),
                Payments = new List<Payment>(),
                Invoices = new List<Invoice>()
            };

            // Mark room as reserved if a room is assigned
            if (room != null && reservation.RoomId.HasValue)
            {
                room.Status = RoomStatus.Reserved;
            }

            reservations.Add(reservation);
        }

        // 2. Create checked-in reservations (3)
        for (int i = 0; i < 3; i++)
        {
            var checkInDate = DateTime.UtcNow.AddDays(-random.Next(1, 3));
            var checkOutDate = DateTime.UtcNow.AddDays(random.Next(1, 5));
            var guest = guests[random.Next(guests.Count)];
            var roomType = roomTypes[random.Next(roomTypes.Count)];

            // For checked-in, we need a room
            var availableRoom = rooms.FirstOrDefault(r =>
                r.RoomTypeId == roomType.Id &&
                r.Status == RoomStatus.Available);

            if (availableRoom == null)
            {
                logger.LogWarning("No available room for checked-in reservation, trying another room type");
                availableRoom = rooms.FirstOrDefault(r => r.Status == RoomStatus.Available);

                if (availableRoom == null)
                {
                    logger.LogWarning("No available rooms at all, skipping checked-in reservation");
                    continue;
                }

                roomType = roomTypes.First(rt => rt.Id == availableRoom.RoomTypeId);
            }

            // Mark room as occupied
            availableRoom.Status = RoomStatus.Occupied;

            var reservation = new Reservation
            {
                ReservationNumber = $"RES{DateTime.UtcNow:yyyyMMdd}{5 + i + 1:D4}",
                UserId = guest.Id,
                RoomId = availableRoom.Id,
                RoomTypeId = roomType.Id,
                CheckInDate = checkInDate,
                CheckOutDate = checkOutDate,
                Status = ReservationStatus.CheckedIn,
                ReservationDate = DateTime.UtcNow.AddDays(-random.Next(5, 15)),
                NumberOfGuests = random.Next(1, roomType.Capacity + 1),
                TotalPrice = roomType.BasePrice * (decimal)(checkOutDate - checkInDate).TotalDays,
                PaymentMethod = PaymentMethod.CreditCard,
                PaymentStatus = PaymentStatus.Paid,
                SpecialRequests = "Extra pillows and late checkout if possible",
                CreatedBy = manager.Id,
                CheckedInBy = receptionist.Id,
                CheckedInTime = checkInDate,
                ServiceOrders = new List<ServiceOrder>(),
                Payments = new List<Payment>(),
                Invoices = new List<Invoice>()
            };

            reservations.Add(reservation);
        }

        // 3. Create checked-out reservations (4)
        for (int i = 0; i < 4; i++)
        {
            var checkOutDate = DateTime.UtcNow.AddDays(-random.Next(1, 10));
            var checkInDate = checkOutDate.AddDays(-random.Next(1, 7));
            var guest = guests[random.Next(guests.Count)];
            var roomType = roomTypes[random.Next(roomTypes.Count)];

            // Find any room of the selected type (doesn't matter for past reservation)
            var room = rooms.FirstOrDefault(r => r.RoomTypeId == roomType.Id);

            if (room == null)
            {
                logger.LogWarning("No room found for room type {RoomTypeId}, skipping", roomType.Id);
                continue;
            }

            var reservation = new Reservation
            {
                ReservationNumber = $"RES{DateTime.UtcNow:yyyyMMdd}{8 + i + 1:D4}",
                UserId = guest.Id,
                RoomId = room.Id,
                RoomTypeId = roomType.Id,
                CheckInDate = checkInDate,
                CheckOutDate = checkOutDate,
                Status = ReservationStatus.CheckedOut,
                ReservationDate = checkInDate.AddDays(-random.Next(1, 14)),
                NumberOfGuests = random.Next(1, roomType.Capacity + 1),
                TotalPrice = roomType.BasePrice * (decimal)(checkOutDate - checkInDate).TotalDays,
                PaymentMethod = PaymentMethod.CreditCard,
                PaymentStatus = PaymentStatus.Paid,
                SpecialRequests = i % 2 == 0 ? null : "Early check-in requested",
                CreatedBy = manager.Id,
                CheckedInBy = receptionist.Id,
                CheckedInTime = checkInDate,
                CheckedOutBy = receptionist.Id,
                CheckedOutTime = checkOutDate,
                ServiceOrders = new List<ServiceOrder>(),
                Payments = new List<Payment>(),
                Invoices = new List<Invoice>()
            };

            reservations.Add(reservation);
        }

        // 4. Create cancelled reservations (2)
        for (int i = 0; i < 2; i++)
        {
            var checkInDate = DateTime.UtcNow.AddDays(random.Next(15, 45));
            var checkOutDate = checkInDate.AddDays(random.Next(1, 5));
            var cancellationDate = DateTime.UtcNow.AddDays(-random.Next(1, 5));
            var guest = guests[random.Next(guests.Count)];
            var roomType = roomTypes[random.Next(roomTypes.Count)];

            var reservation = new Reservation
            {
                ReservationNumber = $"RES{DateTime.UtcNow:yyyyMMdd}{12 + i + 1:D4}",
                UserId = guest.Id,
                RoomTypeId = roomType.Id,
                CheckInDate = checkInDate,
                CheckOutDate = checkOutDate,
                Status = ReservationStatus.Cancelled,
                ReservationDate = cancellationDate.AddDays(-random.Next(1, 10)),
                NumberOfGuests = random.Next(1, roomType.Capacity + 1),
                TotalPrice = roomType.BasePrice * (decimal)(checkOutDate - checkInDate).TotalDays,
                PaymentMethod = (PaymentMethod)random.Next(0, Enum.GetValues(typeof(PaymentMethod)).Length),
                PaymentStatus = PaymentStatus.Refunded,
                CancellationReason = "Change of plans",
                CancelledAt = cancellationDate,
                CreatedBy = manager.Id,
                ServiceOrders = new List<ServiceOrder>(),
                Payments = new List<Payment>(),
                Invoices = new List<Invoice>()
            };

            reservations.Add(reservation);
        }

        await context.Reservations.AddRangeAsync(reservations);
        await context.SaveChangesAsync();
        logger.LogInformation("Added {Count} reservations", reservations.Count);
        return reservations;
    }

    private static async Task CreateTestInvoicesAndPayments(
        AppDbContext context,
        List<Reservation> reservations,
        List<ApplicationUser> users,
        ILogger logger)
    {
        logger.LogInformation("Creating test invoices and payments...");

        if (await context.Invoices.AnyAsync())
        {
            logger.LogInformation("Invoices already exist, skipping");
            return;
        }

        var random = new Random();
        var invoices = new List<Invoice>();

        // Find receptionist user - use exact email match and ensure it exists
        var receptionist = users.FirstOrDefault(u => u.UserName == "receptionist@hotel.com");

        if (receptionist == null)
        {
            logger.LogError("Receptionist user not found! Skipping payment creation.");
            // Create invoices but skip payments since we need a receptionist
            foreach (var reservation in reservations)
            {
                // Create the primary invoice for the room
                var creationDate = reservation.ReservationDate.AddDays(1);
                bool isPaid = reservation.Status == ReservationStatus.CheckedIn ||
                              reservation.Status == ReservationStatus.CheckedOut;
                DateTime? paidDate = isPaid ? reservation.CheckedInTime ?? DateTime.UtcNow.AddDays(-1) : null;

                var invoice = new Invoice
                {
                    InvoiceNumber = $"INV-{creationDate:yyyyMMdd}-{reservation.Id}",
                    ReservationId = reservation.Id,
                    Amount = reservation.TotalPrice,
                    Tax = reservation.TotalPrice * 0.10m, // 10% tax
                    Total = reservation.TotalPrice * 1.10m,
                    CreatedAt = creationDate,
                    DueDate = reservation.CheckInDate,
                    Notes = "Hotel stay invoice",
                    IsPaid = isPaid,
                    PaidAt = paidDate
                };

                invoices.Add(invoice);
            }

            await context.Invoices.AddRangeAsync(invoices);
            await context.SaveChangesAsync();
            logger.LogInformation("Added {Count} invoices (without payments)", invoices.Count);
            return;
        }

        // Verify the receptionist ID exists in the database
        var receptionistExists = await context.Users.AnyAsync(u => u.Id == receptionist.Id);
        if (!receptionistExists)
        {
            logger.LogError("Receptionist user found in memory but not in database! ID: {Id}", receptionist.Id);
            return;
        }

        logger.LogInformation("Using receptionist with ID {Id} for payment processing", receptionist.Id);

        foreach (var reservation in reservations)
        {
            // Create the primary invoice for the room
            var creationDate = reservation.ReservationDate.AddDays(1);
            bool isPaid = reservation.Status == ReservationStatus.CheckedIn ||
                          reservation.Status == ReservationStatus.CheckedOut;
            DateTime? paidDate = isPaid ? reservation.CheckedInTime ?? DateTime.UtcNow.AddDays(-1) : null;

            var invoice = new Invoice
            {
                InvoiceNumber = $"INV-{creationDate:yyyyMMdd}-{reservation.Id}",
                ReservationId = reservation.Id,
                Amount = reservation.TotalPrice,
                Tax = reservation.TotalPrice * 0.10m, // 10% tax
                Total = reservation.TotalPrice * 1.10m,
                CreatedAt = creationDate,
                DueDate = reservation.CheckInDate,
                Notes = "Hotel stay invoice",
                IsPaid = isPaid,
                PaidAt = paidDate
            };

            invoices.Add(invoice);
        }

        await context.Invoices.AddRangeAsync(invoices);
        await context.SaveChangesAsync();
        logger.LogInformation("Added {Count} invoices", invoices.Count);

        // Now create payments for paid invoices
        var payments = new List<Payment>();
        foreach (var invoice in invoices.Where(i => i.IsPaid))
        {
            var reservation = reservations.FirstOrDefault(r => r.Id == invoice.ReservationId);
            if (reservation == null) continue;

            var paymentDate = invoice.PaidAt ?? DateTime.UtcNow;

            var payment = new Payment
            {
                InvoiceId = invoice.Id,
                AmountPaid = invoice.Total,
                PaymentDate = paymentDate,
                Method = reservation.PaymentMethod,
                ProcessedBy = receptionist.Id,
                TransactionId = $"TXN{paymentDate:yyyyMMdd}{random.Next(1000, 9999)}",
                Notes = "Payment for hotel stay"
            };

            payments.Add(payment);
        }

        try
        {
            if (payments.Any())
            {
                await context.Payments.AddRangeAsync(payments);
                await context.SaveChangesAsync();
                logger.LogInformation("Added {Count} payments", payments.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save payments. Skipping payment creation.");
            // Continue execution to associate invoices with reservations
        }

        // Associate invoices with reservations
        foreach (var reservation in reservations)
        {
            var reservationInvoices = invoices.Where(i => i.ReservationId == reservation.Id).ToList();
            if (reservationInvoices.Any())
            {
                reservation.Invoices = reservationInvoices;
            }
        }

        await context.SaveChangesAsync();
    }


    private static async Task CreateTestServiceOrders(
        AppDbContext context,
        List<Reservation> reservations,
        List<Service> services,
        List<ApplicationUser> users,
        ILogger logger)
    {
        logger.LogInformation("Creating test service orders...");

        if (await context.ServiceOrders.AnyAsync())
        {
            logger.LogInformation("Service orders already exist, skipping");
            return;
        }

        var serviceOrders = new List<ServiceOrder>();
        var random = new Random();

        // Get housekeeper for service completion
        var housekeeper = users.FirstOrDefault(u => u.UserName.Contains("housekeeper"));
        if (housekeeper == null)
        {
            logger.LogWarning("No housekeeper found for service order completion");
            return;
        }

        // Create service orders for active reservations (checked-in or checked-out)
        var activeReservations = reservations
            .Where(r => r.Status == ReservationStatus.CheckedIn || r.Status == ReservationStatus.CheckedOut)
            .ToList();

        foreach (var reservation in activeReservations)
        {
            // Create 1-3 service orders per active reservation
            var orderCount = random.Next(1, 4);

            for (int i = 0; i < orderCount; i++)
            {
                var service = services[random.Next(services.Count)];
                var quantity = random.Next(1, 4);

                // For checked-in reservations, orders could be in various states
                // For checked-out, all orders should be completed
                var status = reservation.Status == ReservationStatus.CheckedOut ?
                    ServiceOrderStatus.Completed :
                    (ServiceOrderStatus)random.Next(4); // Random status for checked-in

                var orderDate = reservation.Status == ReservationStatus.CheckedIn ?
                    DateTime.UtcNow.AddHours(-random.Next(1, 24)) :
                    reservation.CheckInDate.AddDays(random.Next((int)(reservation.CheckOutDate - reservation.CheckInDate).TotalDays));

                var serviceOrder = new ServiceOrder
                {
                    ReservationId = reservation.Id,
                    ServiceId = service.Id,
                    OrderDateTime = orderDate,
                    Quantity = quantity,
                    Status = status,
                    TotalPrice= 100,
                    PriceCharged=100,
                    SpecialInstructions = random.Next(3) == 0 ? "Please deliver to room" : null,
                    DeliveryLocation = $"Room {reservation.Room?.RoomNumber ?? "unknown"}"
                };

                // Add completion details for completed orders
                if (status == ServiceOrderStatus.Completed)
                {
                    serviceOrder.CompletedById = housekeeper.Id;
                    serviceOrder.CompletedAt = orderDate.AddHours(random.Next(1, 3));
                    serviceOrder.CompletionNotes = "Service completed as requested";
                }

                serviceOrders.Add(serviceOrder);
            }
        }

        await context.ServiceOrders.AddRangeAsync(serviceOrders);
        await context.SaveChangesAsync();
        logger.LogInformation("Added {Count} service orders", serviceOrders.Count);
    }

    private static async Task CreateTestCleaningTasks(
        AppDbContext context,
        List<Room> rooms,
        List<ApplicationUser> users,
        ILogger logger)
    {
        logger.LogInformation("Creating test cleaning tasks...");

        if (await context.CleaningTasks.AnyAsync())
        {
            logger.LogInformation("Cleaning tasks already exist, skipping");
            return;
        }

        var cleaningTasks = new List<CleaningTask>();
        var random = new Random();

        // Get housekeeper
        var housekeeper = users.FirstOrDefault(u => u.UserName.Contains("housekeeper"));
        if (housekeeper == null)
        {
            logger.LogError("No housekeeper found for cleaning tasks");
            return;
        }

        // Get status values
        var statuses = Enum.GetValues(typeof(CleaningRequestStatus)).Cast<CleaningRequestStatus>().ToArray();
        var priorities = Enum.GetValues(typeof(Priority)).Cast<Priority>().ToArray();

        // Create tasks for various rooms
        foreach (var room in rooms.Take(15)) // Limit to first 15 rooms
        {
            // Skip if room is occupied and not due for cleaning
            if (room.Status == RoomStatus.Occupied && random.Next(3) > 0)
                continue;

            var status = statuses[random.Next(statuses.Length)];
            var priority = priorities[random.Next(priorities.Length)];

            // Use higher priority for occupied rooms that need cleaning
            if (room.Status == RoomStatus.Occupied && room.NeedsCleaning)
                priority = Priority.High;

            var createdAt = DateTime.UtcNow.AddDays(-random.Next(0, 5));

            var task = new CleaningTask
            {
                TaskId = $"CLN{createdAt:yyyyMMdd}{random.Next(1000, 9999)}",
                RoomId = room.Id,
                AssignedToId = housekeeper.Id,
                Description = GetRandomCleaningDescription(random),
                Status = status,
                Priority = priority,
                CreatedAt = createdAt,
                Notes= "Cleaning task created for room",
            };

            // For completed tasks, add completion info
            if (status == CleaningRequestStatus.Cleaned)
            {
                task.CompletedAt = createdAt.AddHours(random.Next(1, 8));
                task.CompletionNotes = "Room cleaned and ready for guests";

                // Update room status
                room.NeedsCleaning = false;
                room.LastCleaned = task.CompletedAt;
                room.CleanedById = housekeeper.Id;
            }

            cleaningTasks.Add(task);
        }

        await context.CleaningTasks.AddRangeAsync(cleaningTasks);
        await context.SaveChangesAsync();
        logger.LogInformation("Added {Count} cleaning tasks", cleaningTasks.Count);
    }

    private static string GetRandomCleaningDescription(Random random)
    {
        var descriptions = new[]
        {
            "Standard daily cleaning",
            "Deep cleaning required",
            "Post-checkout cleaning",
            "Linen change and cleaning",
            "Urgent cleaning request",
            "Scheduled maintenance cleaning",
            "Bathroom deep cleaning",
            "Full room sanitization",
            "Carpet cleaning required",
            "Weekly cleaning schedule"
        };

        return descriptions[random.Next(descriptions.Length)];
    }

    private static async Task CreateTestMaintenanceRequests(
        AppDbContext context,
        List<Room> rooms,
        List<ApplicationUser> users,
        ILogger logger)
    {
        logger.LogInformation("Creating test maintenance requests...");

        if (await context.MaintenanceRequests.AnyAsync())
        {
            logger.LogInformation("Maintenance requests already exist, skipping");
            return;
        }

        var maintenanceRequests = new List<MaintenanceRequest>();
        var random = new Random();

        // Get staff members
        var manager = users.FirstOrDefault(u => u.UserName.Contains("manager"));
        var housekeeper = users.FirstOrDefault(u => u.UserName.Contains("housekeeper"));

        if (manager == null || housekeeper == null)
        {
            logger.LogError("Manager or housekeeper not found for maintenance requests");
            return;
        }

        // Get status values
        var statuses = Enum.GetValues(typeof(MaintenanceRequestStatus)).Cast<MaintenanceRequestStatus>().ToArray();
        var priorities = Enum.GetValues(typeof(MaintenanceRequestPriority)).Cast<MaintenanceRequestPriority>().ToArray();

        // Create maintenance requests for some rooms
        foreach (var room in rooms.Where(r => random.Next(5) > 3).Take(8)) // About 20% of rooms, max 8
        {
            var reportDate = DateTime.UtcNow.AddDays(-random.Next(1, 14));
            var status = statuses[random.Next(statuses.Length)];
            var priority = priorities[random.Next(priorities.Length)];

            // Higher chance of maintenance for rooms already under maintenance
            if (room.Status == RoomStatus.Maintenance && random.Next(3) > 0)
            {
                status = MaintenanceRequestStatus.InProgress;
                priority = MaintenanceRequestPriority.High;
            }

            var request = new MaintenanceRequest
            {
                ReportDate = reportDate,
                IssueDescription = GetRandomMaintenanceIssue(random),
                Status = status,
                Priority = priority,
                ReportedBy = random.Next(2) == 0 ? manager.Id : housekeeper.Id,
                AssignedTo = housekeeper.Id,
                RoomId = room.Id
            };

            // Add completion info for resolved requests
            if (status == MaintenanceRequestStatus.Resolved)
            {
                request.CompletedAt = reportDate.AddDays(random.Next(1, 3));
                request.ResolutionNotes = "Issue has been repaired and tested.";

                // If room was under maintenance, mark it as available again
                if (room.Status == RoomStatus.Maintenance)
                    room.Status = RoomStatus.Available;
            }

            maintenanceRequests.Add(request);
        }

        await context.MaintenanceRequests.AddRangeAsync(maintenanceRequests);
        await context.SaveChangesAsync();
        logger.LogInformation("Added {Count} maintenance requests", maintenanceRequests.Count);
    }

    private static string GetRandomMaintenanceIssue(Random random)
    {
        var issues = new[]
        {
            "Leaking faucet in bathroom",
            "Air conditioning not cooling properly",
            "Television remote needs battery replacement",
            "Light fixture flickering",
            "Toilet running continuously",
            "Shower drain clogged",
            "Door lock not working properly",
            "Window stuck and won't open",
            "Ceiling fan making noise",
            "Refrigerator not cooling",
            "Electrical outlet not working",
            "Heating system not functioning"
        };

        return issues[random.Next(issues.Length)];
    }

    private static async Task CreateTestFeedback(
        AppDbContext context,
        List<Reservation> reservations,
        List<ApplicationUser> users,
        ILogger logger)
    {
        logger.LogInformation("Creating test feedback...");

        if (await context.Feedback.AnyAsync())
        {
            logger.LogInformation("Feedback already exists, skipping");
            return;
        }

        var feedbacks = new List<Feedback>();
        var random = new Random();

        // Get manager for resolution
        var manager = users.FirstOrDefault(u => u.UserName.Contains("manager"));
        if (manager == null)
        {
            logger.LogError("Manager not found for feedback resolution");
            return;
        }

        // Categories for feedback
        var categories = new[] { "Room", "Service", "Cleanliness", "Food", "General", "Staff" };

        // Create feedback for checked-out reservations
        var completedReservations = reservations.Where(r => r.Status == ReservationStatus.CheckedOut).ToList();

        foreach (var reservation in completedReservations)
        {
            // 80% chance of feedback for each completed reservation
            if (random.Next(5) == 0)
                continue;

            var user = users.FirstOrDefault(u => u.Id == reservation.UserId);
            if (user == null) continue;

            // Generate a rating - mostly positive but some mixed/negative
            var rating = GetWeightedRating(random);
            var createdAt = reservation.CheckedOutTime?.AddDays(random.Next(1, 5)) ?? DateTime.UtcNow;
            var isResolved = rating <= 3 || random.Next(2) == 0; // Always resolve negative, 50% resolve positive

            var feedback = new Feedback
            {
                UserId = user.Id,
                ReservationId = reservation.Id,
                GuestName = $"{user.FirstName} {user.LastName}",
                GuestEmail = user.Email,
                Rating = rating,
                Subject = GetFeedbackSubject(rating, random),
                Comments = GetFeedbackComment(rating, random),
                Category = categories[random.Next(categories.Length)],
                IsResolved = isResolved,
                CreatedAt = createdAt
            };

            if (isResolved)
            {
                feedback.ResolutionNotes = GetResolutionNotes(rating, random);
                feedback.ResolvedById = manager.Id;
                feedback.ResolvedAt = createdAt.AddDays(random.Next(1, 3));
            }

            feedbacks.Add(feedback);
        }

        // Add some standalone feedback not tied to reservations
        for (int i = 0; i < 5; i++)
        {
            var rating = GetWeightedRating(random);
            var isResolved = rating <= 3 || random.Next(3) > 0; // Always resolve negative, 2/3 chance for positive
            var createdAt = DateTime.UtcNow.AddDays(-random.Next(1, 60)); // Up to 2 months old

            var feedback = new Feedback
            {
                GuestName = GetRandomName(random),
                GuestEmail = $"feedback{random.Next(100, 999)}@example.com",
                Rating = rating,
                Subject = GetFeedbackSubject(rating, random),
                Comments = GetFeedbackComment(rating, random),
                Category = categories[random.Next(categories.Length)],
                IsResolved = isResolved,
                CreatedAt = createdAt
            };

            if (isResolved)
            {
                feedback.ResolutionNotes = GetResolutionNotes(rating, random);
                feedback.ResolvedById = manager.Id;
                feedback.ResolvedAt = createdAt.AddDays(random.Next(1, 5));
            }

            feedbacks.Add(feedback);
        }

        await context.Feedback.AddRangeAsync(feedbacks);
        await context.SaveChangesAsync();
        logger.LogInformation("Added {Count} feedback items", feedbacks.Count);
    }

    private static int GetWeightedRating(Random random)
    {
        // Distribution: 60% positive (4-5), 25% neutral (3), 15% negative (1-2)
        var value = random.Next(100);

        if (value < 60)
            return random.Next(4, 6); // 4 or 5
        else if (value < 85)
            return 3;
        else
            return random.Next(1, 3); // 1 or 2
    }

    private static string GetFeedbackSubject(int rating, Random random)
    {
        if (rating >= 4)
        {
            var subjects = new[] {
                "Excellent stay, highly recommended!",
                "Outstanding service and facilities",
                "Wonderful experience from start to finish",
                "Exceeded my expectations",
                "Perfect getaway, will return"
            };
            return subjects[random.Next(subjects.Length)];
        }
        else if (rating == 3)
        {
            var subjects = new[] {
                "Decent stay with some minor issues",
                "Average experience overall",
                "Good but not great",
                "Mixed experience during my stay",
                "Room for improvement"
            };
            return subjects[random.Next(subjects.Length)];
        }
        else
        {
            var subjects = new[] {
                "Disappointed with our stay",
                "Several issues need addressing",
                "Not what I expected for the price",
                "Service fell short of expectations",
                "Will not be returning"
            };
            return subjects[random.Next(subjects.Length)];
        }
    }

    private static string GetFeedbackComment(int rating, Random random)
    {
        if (rating >= 4)
        {
            var comments = new[] {
                "The staff was incredibly helpful and attentive throughout our stay. The room was pristine and the bed was so comfortable. Will definitely stay here again!",
                "We had a wonderful time at your hotel. The amenities were top-notch and the location was perfect for our needs. Thank you for a memorable experience.",
                "From check-in to check-out, everything was handled professionally. The room service was prompt and the food was delicious. Highly recommend!",
                "The room exceeded our expectations - spacious, clean, and well-appointed. The staff went above and beyond to make our anniversary special.",
                "Such a pleasant surprise! The hotel was beautiful, the staff was friendly, and the room was perfect. Looking forward to our next visit."
            };
            return comments[random.Next(comments.Length)];
        }
        else if (rating == 3)
        {
            var comments = new[] {
                "The stay was okay overall. The room was clean but dated. Staff was friendly but sometimes slow to respond to requests.",
                "Decent hotel for the price, but nothing exceptional. The bed was comfortable but the room was smaller than expected.",
                "Mixed experience - good location and friendly staff, but the noise from the street was bothersome and the bathroom needs updating.",
                "The hotel was acceptable but didn't quite match what was advertised online. Breakfast was good but limited in options.",
                "Average stay. Some staff were helpful, others seemed disinterested. The room was clean but the air conditioning was noisy."
            };
            return comments[random.Next(comments.Length)];
        }
        else
        {
            var comments = new[] {
                "Disappointing stay. The room was not properly cleaned, with stains on the carpets and dust on surfaces. The staff was unresponsive when we reported issues.",
                "Several problems with our room - the shower didn't drain properly, the TV remote was missing batteries, and we could hear every conversation in the hallway.",
                "Not worth the price we paid. The photos online were misleading as the room was much smaller and more dated than shown. The bed was uncomfortable and wifi was unreliable.",
                "Poor service throughout our stay. Had to wait over an hour for check-in, room was not ready despite arriving after check-in time, and requests for extra towels were ignored.",
                "The air conditioning in our room didn't work properly, making for an uncomfortable night's sleep. Despite multiple calls to the front desk, no one came to fix it."
            };
            return comments[random.Next(comments.Length)];
        }
    }

    private static string GetResolutionNotes(int rating, Random random)
    {
        if (rating >= 4)
        {
            var notes = new[] {
                "Thank you for your positive feedback! We're delighted you enjoyed your stay and look forward to welcoming you back soon.",
                "We appreciate your kind words about our hotel and staff. Your feedback has been shared with the team!",
                "Thank you for taking the time to share your experience. We're pleased you enjoyed your stay with us.",
                "We're thrilled to hear about your wonderful experience and thank you for choosing our hotel.",
                "Your feedback means a lot to us. Thank you for your kind review and we hope to see you again."
            };
            return notes[random.Next(notes.Length)];
        }
        else if (rating == 3)
        {
            var notes = new[] {
                "Thank you for your feedback. We've noted your comments about [specific issue] and are working to improve this aspect of our service.",
                "We appreciate your honest review and have shared your comments with our management team to address the areas where we can improve.",
                "Thank you for bringing these points to our attention. We're constantly working to enhance our guest experience and your feedback is valuable.",
                "We've addressed the issues you mentioned and have implemented changes to prevent similar occurrences. Thank you for helping us improve.",
                "Thank you for your balanced review. We apologize for not exceeding your expectations and have made note of your suggestions."
            };
            return notes[random.Next(notes.Length)];
        }
        else
        {
            var notes = new[] {
                "We sincerely apologize for the issues you experienced. As immediate action, we've [specific action taken] and have provided additional training to our staff.",
                "Thank you for bringing these concerns to our attention. We've addressed them directly with the relevant departments and have implemented new procedures to prevent recurrence.",
                "We deeply regret that your stay didn't meet our usual standards. We've personally inspected the room issues you mentioned and have made necessary repairs.",
                "Please accept our sincere apologies for your disappointing experience. We would welcome the opportunity to make it up to you on a future stay.",
                "We take your feedback very seriously and have already implemented changes based on your comments. We've also credited your account as a goodwill gesture."
            };
            return notes[random.Next(notes.Length)];
        }
    }

    private static string GetRandomName(Random random)
    {
        var firstNames = new[] { "James", "Mary", "Robert", "Patricia", "John", "Jennifer", "Michael", "Linda", "William", "Elizabeth",
            "David", "Susan", "Richard", "Jessica", "Thomas", "Sarah", "Charles", "Karen", "Daniel", "Nancy" };

        var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Miller", "Davis", "Garcia", "Rodriguez", "Wilson",
            "Martinez", "Anderson", "Taylor", "Thomas", "Hernandez", "Moore", "Martin", "Jackson", "Thompson", "White" };

        return $"{firstNames[random.Next(firstNames.Length)]} {lastNames[random.Next(lastNames.Length)]}";
    }

    private static async Task CreateTestReports(
        AppDbContext context,
        List<ApplicationUser> users,
        ILogger logger)
    {
        logger.LogInformation("Creating test reports...");

        if (await context.Reports.AnyAsync())
        {
            logger.LogInformation("Reports already exist, skipping");
            return;
        }

        var reports = new List<Report>();
        var random = new Random();

        // Get manager for report creation
        var manager = users.FirstOrDefault(u => u.UserName.Contains("manager"));
        if (manager == null)
        {
            logger.LogError("Manager not found for report creation");
            return;
        }

        // Get all report types
        var reportTypes = Enum.GetValues(typeof(ReportType)).Cast<ReportType>().ToList();

        // Create reports for the past several months
        for (int monthsAgo = 0; monthsAgo < 6; monthsAgo++)
        {
            var reportDate = DateTime.UtcNow.AddMonths(-monthsAgo);

            // Create one of each type of report for each month
            foreach (var reportType in reportTypes)
            {
                var report = new Report
                {
                    ReportName = $"{reportType} Report - {reportDate:MMMM yyyy}",
                    ReportType = reportType,
                    CreationDate = new DateTime(reportDate.Year, reportDate.Month, random.Next(1, 28)),
                    CreatedBy = manager.Id,
                    ReportData = GetMockReportData(reportType, reportDate, random)
                };

                reports.Add(report);
            }
        }

        await context.Reports.AddRangeAsync(reports);
        await context.SaveChangesAsync();
        logger.LogInformation("Added {Count} reports", reports.Count);
    }

    private static string GetMockReportData(ReportType reportType, DateTime reportDate, Random random)
    {
        switch (reportType)
        {
            case ReportType.Occupancy:
                var occupancyRate = random.Next(50, 96);
                return $@"{{
                    ""totalRooms"": 100,
                    ""occupiedRooms"": {occupancyRate},
                    ""occupancyRate"": {occupancyRate}.{random.Next(10, 100)},
                    ""averageDailyRate"": {random.Next(100, 251)}.{random.Next(10, 100)},
                    ""revPAR"": {random.Next(50, 201)}.{random.Next(10, 100)},
                    ""reportPeriod"": ""{reportDate:MMMM yyyy}""
                }}";

            case ReportType.Revenue:
                var baseRevenue = random.Next(80000, 150001);
                return $@"{{
                    ""totalRevenue"": {baseRevenue}.00,
                    ""roomRevenue"": {baseRevenue * 0.75}.00,
                    ""foodBeverageRevenue"": {baseRevenue * 0.15}.00,
                    ""otherRevenue"": {baseRevenue * 0.10}.00,
                    ""expenses"": {baseRevenue * 0.6}.00,
                    ""profit"": {baseRevenue * 0.4}.00,
                    ""reportPeriod"": ""{reportDate:MMMM yyyy}""
                }}";

            case ReportType.Maintenance:
                return $@"{{
                    ""totalRequests"": {random.Next(10, 51)},
                    ""resolvedRequests"": {random.Next(5, 46)},
                    ""pendingRequests"": {random.Next(1, 11)},
                    ""averageResolutionTime"": ""{random.Next(1, 25)} hours"",
                    ""mostCommonIssue"": ""Plumbing problems"",
                    ""reportPeriod"": ""{reportDate:MMMM yyyy}""
                }}";

            case ReportType.Staff:
                return $@"{{
                    ""totalStaff"": {random.Next(20, 41)},
                    ""housekeeping"": {random.Next(5, 11)},
                    ""reception"": {random.Next(3, 9)},
                    ""management"": {random.Next(2, 6)},
                    ""maintenance"": {random.Next(2, 6)},
                    ""other"": {random.Next(3, 11)},
                    ""averageWorkingHours"": {random.Next(160, 185)},
                    ""absenceRate"": {random.Next(1, 6)}.{random.Next(10, 100)},
                    ""reportPeriod"": ""{reportDate:MMMM yyyy}""
                }}";

            case ReportType.Guest:
                return $@"{{
                    ""totalGuests"": {random.Next(200, 601)},
                    ""newGuests"": {random.Next(50, 151)},
                    ""returningGuests"": {random.Next(150, 451)},
                    ""averageStayDuration"": {random.Next(2, 6)}.{random.Next(1, 10)},
                    ""mostCommonPurpose"": ""Business"",
                    ""topCountryOfOrigin"": ""United States"",
                    ""reportPeriod"": ""{reportDate:MMMM yyyy}""
                }}";

            default:
                return $@"{{
                    ""reportDate"": ""{reportDate:yyyy-MM-dd}"",
                    ""generatedBy"": ""System"",
                    ""dataPoints"": {random.Next(10, 101)},
                    ""reportPeriod"": ""{reportDate:MMMM yyyy}""
                }}";
        }
    }
}

