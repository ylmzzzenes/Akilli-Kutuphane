using AkilliKutuphane.Business.Extensions;
using AkilliKutuphane.Data.Entities;
using AkilliKutuphane.Data.Extensions;
using AkilliKutuphane.Data.Persistence;
using AkilliKutuphane.Web.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
if (builder.Environment.IsProduction() &&
    (connectionString.Contains("__SET_IN_ENV_OR_SECRET_STORE__", StringComparison.OrdinalIgnoreCase) ||
     connectionString.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
     connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase)))
{
    throw new InvalidOperationException(
        "Invalid production connection string. Set 'ConnectionStrings__DefaultConnection' from environment variables or a secret manager.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddMemoryCache();
builder.Services.AddBusinessServices(builder.Configuration);
builder.Services.AddDataAccessServices();
builder.Services.AddControllersWithViews();

var app = builder.Build();
await SeedData.InitializeAsync(app.Services, app.Configuration, app.Logger);

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
