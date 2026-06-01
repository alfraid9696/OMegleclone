using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
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

builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.AddSingleton<AdminAuthService>();

builder.Services.AddAuthentication()
    .AddCookie(AdminAuthConstants.Scheme, options =>
    {
        options.LoginPath = "/Admin/Login";
        options.AccessDeniedPath = "/Admin/Login";
        options.Cookie.Name = "StrangersCall.Admin";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSingleton<MatchmakingService>();
builder.Services.AddSingleton<OnlinePresenceService>();
builder.Services.AddSingleton<FriendCallSessionService>();
builder.Services.AddScoped<FriendService>();
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    var isSqlite = databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);

    if (isSqlite)
    {
        var sqlitePath = connectionString
            .Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        var sqliteDir = Path.GetDirectoryName(sqlitePath);
        if (!string.IsNullOrEmpty(sqliteDir))
        {
            Directory.CreateDirectory(sqliteDir);
        }
    }

    DatabaseInitializer.ApplyMigrations(db, isSqlite);
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
app.MapHub<FriendHub>("/friendHub");

app.Run();
