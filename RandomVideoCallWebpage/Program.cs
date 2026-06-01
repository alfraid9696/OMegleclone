using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RandomVideoCallWebpage.Data;
using RandomVideoCallWebpage.Hubs;
using RandomVideoCallWebpage.Models;
using RandomVideoCallWebpage.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSingleton<MatchmakingService>();
builder.Services.AddSingleton<OnlinePresenceService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var sqlitePath = connectionString
            .Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        var sqliteDir = Path.GetDirectoryName(sqlitePath);
        if (!string.IsNullOrEmpty(sqliteDir))
        {
            Directory.CreateDirectory(sqliteDir);
        }

        db.Database.EnsureCreated();
    }
    else
    {
        db.Database.Migrate();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapHub<ChatHub>("/chatHub");

app.Run();
