using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SvatebniWeb.Web.Components;
using SvatebniWeb.Web.Data;
using System.Security.Claims;

namespace SvatebniWeb.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorComponents().AddInteractiveServerComponents();
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Registrace základních služeb pro Identity a propojení s Blazorem
            builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
            builder.Services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 4;
            })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager();

            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();

            await SeedRolesAndAdminAsync(app.Services);

            app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

            // =================================================================
            // ZDE JE KLÍÈOVÁ ZMÌNA: Vlastní endpointy pro pøihlášení a odhlášení
            // =================================================================
            // Tento endpoint pøijme data z pøihlašovacího formuláøe
            app.MapPost("/account/login", async (HttpContext httpContext, SignInManager<ApplicationUser> signInManager, [FromForm] string email, [FromForm] string password) =>
            {
                var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    return Results.LocalRedirect("/moje-weby");
                }
                // V pøípadì chyby pøesmìrujeme zpìt s chybovou hláškou
                return Results.LocalRedirect("/login?ErrorMessage=Neplatné pøihlašovací údaje.");
            });

            // Tento endpoint zpracuje odhlášení
            app.MapPost("/account/logout", async (HttpContext httpContext, SignInManager<ApplicationUser> signInManager) =>
            {
                await signInManager.SignOutAsync();
                return Results.LocalRedirect("/");
            });

            app.Run();
        }

        private static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            string[] roleNames = { "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
            var adminEmail = "admin@svatebniweb.cz";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
                await userManager.CreateAsync(adminUser, "admin");
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
    }
}