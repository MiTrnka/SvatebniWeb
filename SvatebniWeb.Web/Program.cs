using SvatebniWeb.Web.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SvatebniWeb.Web.Data;

namespace SvatebniWeb.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            // P�ipojen� k datab�zi a registrace DbContextu
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // ��k�me aplikaci, aby pou��vala Identity pro u�ivatele (ApplicationUser) a role (IdentityRole)
            // a ukl�dala je pomoc� na�eho ApplicationDbContext.
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Zde si m��ete nastavit pravidla pro hesla, atd.
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 4;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
            })
                .AddEntityFrameworkStores<ApplicationDbContext>();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            // Po sestaven� aplikace zavol�me na�i metodu pro vytvo�en� admina a rol�
            await SeedRolesAndAdminAsync(app.Services);
            // --- KONEC NOV� ��STI ---

            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }

        // Metoda pro vytvo�en� rol� a admina ---
        private static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            // Vytvo��me si "scope", abychom mohli z�skat p��stup ke slu�b�m
            using var scope = serviceProvider.CreateScope();
            var services = scope.ServiceProvider;

            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            // Vytvo�en� role "Admin", pokud neexistuje
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            // Vytvo�en� u�ivatele "admin", pokud neexistuje
            if (await userManager.FindByNameAsync("admin") == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = "admin",
                    Email = "mitrnka@gmail.com", // Dopl�te skute�n� email
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, "admin123");

                // Pokud se u�ivatel �sp�n� vytvo�il, p�i�ad�me mu roli "Admin"
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}