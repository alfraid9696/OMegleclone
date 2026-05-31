using RandomVideoCallWebpage.Hubs;
using RandomVideoCallWebpage.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSingleton<MatchmakingService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();
app.UseAuthorization();

app.UseStaticFiles();
app.MapRazorPages();
app.MapHub<ChatHub>("/chatHub");

app.Run();
