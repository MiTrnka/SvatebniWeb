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

            // Pøipojení k databázi a registrace DbContextu
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Øíkáme aplikaci, aby používala Identity pro uživatele (ApplicationUser) a role (IdentityRole)
            // a ukládala je pomocí našeho ApplicationDbContext.
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Zde si mùžete nastavit pravidla pro hesla, atd.
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

            // Po sestavení aplikace zavoláme naši metodu pro vytvoøení admina a rolí
            await SeedRolesAndAdminAsync(app.Services);
            // --- KONEC NOVÉ ÈÁSTI ---

            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }

        // Metoda pro vytvoøení rolí a admina ---
        private static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            // Vytvoøíme si "scope", abychom mohli získat pøístup ke službám
            using var scope = serviceProvider.CreateScope();
            var services = scope.ServiceProvider;

            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            // Vytvoøení role "Admin", pokud neexistuje
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            // Vytvoøení uživatele "admin", pokud neexistuje
            if (await userManager.FindByNameAsync("admin") == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = "admin",
                    Email = "mitrnka@gmail.com", // Doplòte skuteèný email
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, "admin123");

                // Pokud se uživatel úspìšnì vytvoøil, pøiøadíme mu roli "Admin"
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}