// Tento soubor obsahuje hlavn� vstupn� bod aplikace.
// Konfiguruje a spou�t� webovou aplikaci, nastavuje slu�by,
// middleware a zpracov�n� po�adavk�.
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SvatebniWeb.Web.Components;
using SvatebniWeb.Web.Data;
using System.Security.Claims;
using System.Web;

namespace SvatebniWeb.Web
{
    /// <summary>
    /// Hlavn� t��da aplikace, kter� obsahuje vstupn� bod Main.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Vstupn� bod aplikace. Tato asynchronn� metoda inicializuje, konfiguruje a spou�t� webovou aplikaci.
        /// Zahrnuje nastaven� slu�eb, datab�zov�ho kontextu, autentizace, autorizace a definuje,
        /// jak aplikace reaguje na HTTP po�adavky, v�etn� vlastn�ch endpoint� pro p�ihl�en� a odhl�en�.
        /// </summary>
        /// <param name="args">Argumenty p��kazov�ho ��dku p�edan� aplikaci p�i spu�t�n�.</param>
        public static async Task Main(string[] args)
        {
            // Vytvo�en� instance WebApplicationBuilderu, kter� slou�� k sestaven� a konfiguraci webov� aplikace.
            var builder = WebApplication.CreateBuilder(args);

            // P�id�n� slu�eb pro Razor Components a nastaven� interaktivn�ho serverov�ho re�imu.
            // To umo��uje Blazor komponent�m b�et na serveru a komunikovat s klientem p�es SignalR.
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();

            // Z�sk�n� p�ipojovac�ho �et�zce k datab�zi z konfigura�n�ho souboru.
            // Pokud �et�zec nen� nalezen, je vyvol�na v�jimka.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // Registrace datab�zov�ho kontextu (ApplicationDbContext) a konfigurace pou�it� PostgreSQL (Npgsql).
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Konfigurace autentiza�n�ch slu�eb s pou�it�m Identity a cookies.
            builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();

            // Konfigurace a registrace slu�eb pro ASP.NET Core Identity.
            // Nastavuje pravidla pro hesla a propojuje Identity s datab�zov�m kontextem.
            builder.Services.AddIdentityCore<ApplicationUser>(options =>
            {
                // Zde jsou z�m�rn� oslabena pravidla pro hesla pro zjednodu�en� v�voje.
                // V produk�n�m prost�ed� by m�la b�t pravidla p��sn�j��.
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 3;
            })
                // P�id�n� podpory pro u�ivatelsk� role (nap�. "Admin", "User").
                .AddRoles<IdentityRole>()
                // Propojen� Identity s Entity Framework Core a konkr�tn�m datab�zov�m kontextem.
                .AddEntityFrameworkStores<ApplicationDbContext>()
                // P�id�n� SignInManageru, kter� spravuje operace p�ihl�en� a odhl�en�.
                .AddSignInManager();

            // Zaji��uje, �e stav autentizace (informace o p�ihl�en�m u�ivateli) je dostupn�
            // v Blazor komponent�ch prost�ednictv�m kask�dov�ch parametr�.
            builder.Services.AddCascadingAuthenticationState();

            // Registrace slu�by, kter� poskytuje informace o stavu autentizace pro serverov� Blazor aplikace.
            builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

            // Sestaven� a vytvo�en� instance webov� aplikace na z�klad� nakonfigurovan�ch slu�eb.
            var app = builder.Build();

            // Konfigurace pipeline pro zpracov�n� HTTP po�adavk� v z�vislosti na prost�ed�.
            // Pokud aplikace neb�� ve v�vojov�m prost�ed�, zapne se glob�ln� handler v�jimek a HSTS.
            if (!app.Environment.IsDevelopment())
            {
                // V p��pad� neo�et�en� v�jimky p�esm�ruje u�ivatele na str�nku "/error".
                app.UseExceptionHandler("/error");
                // Zapnut� HSTS (HTTP Strict Transport Security) pro zabezpe�en� komunikace.
                app.UseHsts();
            }

            // P�esm�rov�n� v�ech HTTP po�adavk� na HTTPS.
            app.UseHttpsRedirection();

            // Umo�n�n� serv�rov�n� statick�ch soubor� (CSS, JavaScript, obr�zky) ze slo�ky wwwroot.
            app.UseStaticFiles();

            // Aktivace routovac�ho middleware, kter� na z�klad� URL adresy rozhoduje, kter� endpoint se m� vykonat.
            app.UseRouting();

            // Aktivace autentiza�n�ho middleware, kter� ov��uje identitu u�ivatele.
            app.UseAuthentication();

            // Aktivace autoriza�n�ho middleware, kter� kontroluje, zda m� u�ivatel opr�vn�n� k p��stupu.
            app.UseAuthorization();

            // Ochrana proti CSRF (Cross-Site Request Forgery) �tok�m.
            app.UseAntiforgery();

            // Po spu�t�n� aplikace zavol� metodu, kter� zajist� vytvo�en� z�kladn�ch rol� a administr�torsk�ho ��tu,
            // pokud v datab�zi je�t� neexistuj�.
            await SeedRolesAndAdminAsync(app.Services);

            // Mapov�n� Razor Components na ko�enovou komponentu App a zapnut� serverov�ho renderov�n�.
            app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

            // =================================================================
            // VLASTN� ENDPOINTY PRO P�IHL��EN� A ODHL��EN�
            // =================================================================

            // Tento endpoint zpracov�v� POST po�adavek na adresu "/account/login".
            // Pou��v� se pro p�ihl�en� u�ivatele na z�klad� �daj� z formul��e (email a heslo).
            app.MapPost("/account/login", async (HttpContext httpContext, SignInManager<ApplicationUser> signInManager, [FromForm] string email, [FromForm] string password) =>
            {
                // Pokus o p�ihl�en� u�ivatele pomoc� SignInManageru.
                var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    // V p��pad� �sp�n�ho p�ihl�en� p�esm�ruje u�ivatele na str�nku "/moje-weby".
                    return Results.LocalRedirect("/moje-weby");
                }
                // Pokud p�ihl�en� sel�e, u�ivatel je p�esm�rov�n zp�t na p�ihla�ovac� str�nku s chybovou zpr�vou.
                var errorMessage = "Neplatn� p�ihla�ovac� �daje.";
                return Results.LocalRedirect($"/account/login?ErrorMessage={HttpUtility.UrlEncode(errorMessage)}");
            });

            // Tento endpoint zpracov�v� POST po�adavek na adresu "/account/logout".
            // Slou�� k odhl�en� aktu�ln� p�ihl�en�ho u�ivatele.
            app.MapPost("/account/logout", async (HttpContext httpContext, SignInManager<ApplicationUser> signInManager) =>
            {
                // Odhl�en� u�ivatele.
                await signInManager.SignOutAsync();
                // P�esm�rov�n� u�ivatele na hlavn� str�nku.
                return Results.LocalRedirect("/");
            });

            // Spu�t�n� aplikace. Aplikace za�ne naslouchat na p��choz� HTTP po�adavky.
            app.Run();
        }

        /// <summary>
        /// Inicializuje datab�zi se z�kladn�mi daty, pokud je�t� neexistuj�.
        /// Konkr�tn� tato metoda vytv��� role "Admin" a "User" a zakl�d� administr�torsk� ��et
        /// s p�eddefinovan�mi p�ihla�ovac�mi �daji, aby bylo mo�n� se do aplikace po prvn�m spu�t�n� p�ihl�sit.
        /// </summary>
        /// <param name="serviceProvider">Poskytovatel slu�eb pro z�sk�n� instanc� RoleManager a UserManager.</param>
        private static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            // Vytvo�en� nov�ho scope, aby bylo mo�n� z�skat instance slu�eb s �ivotn�m cyklem "scoped".
            using var scope = serviceProvider.CreateScope();

            // Z�sk�n� instance RoleManageru pro spr�vu rol�.
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Z�sk�n� instance UserManageru pro spr�vu u�ivatel�.
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Seznam rol�, kter� maj� b�t v aplikaci vytvo�eny.
            string[] roleNames = { "Admin", "User" };

            // Proch�zen� seznamu rol� a vytvo�en� ka�d� z nich, pokud je�t� neexistuje.
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Definice emailu pro administr�torsk� ��et.
            var adminEmail = "admin@svatebniweb.cz";

            // Zji�t�n�, zda u�ivatel s dan�m emailem ji� existuje.
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                // Pokud administr�tor neexistuje, vytvo�� se nov�.
                var adminUser = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };

                // Vytvo�en� u�ivatele s heslem "admin".
                await userManager.CreateAsync(adminUser, "admin");

                // P�i�azen� role "Admin" nov� vytvo�en�mu u�ivateli.
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
    }
}