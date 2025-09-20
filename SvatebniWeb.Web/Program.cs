// Tento soubor obsahuje hlavní vstupní bod aplikace.
// Konfiguruje a spouští webovou aplikaci, nastavuje sluby,
// middleware a zpracování poadavkù.
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
    /// Hlavní tøída aplikace, která obsahuje vstupní bod Main.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Vstupní bod aplikace. Tato asynchronní metoda inicializuje, konfiguruje a spouští webovou aplikaci.
        /// Zahrnuje nastavení slueb, databázového kontextu, autentizace, autorizace a definuje,
        /// jak aplikace reaguje na HTTP poadavky, vèetnì vlastních endpointù pro pøihlášení a odhlášení.
        /// </summary>
        /// <param name="args">Argumenty pøíkazového øádku pøedané aplikaci pøi spuštìní.</param>
        public static async Task Main(string[] args)
        {
            // Vytvoøení instance WebApplicationBuilderu, která slouí k sestavení a konfiguraci webové aplikace.
            var builder = WebApplication.CreateBuilder(args);

            // Pøidání slueb pro Razor Components a nastavení interaktivního serverového reimu.
            // To umoòuje Blazor komponentám bìet na serveru a komunikovat s klientem pøes SignalR.
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();

            // Získání pøipojovacího øetìzce k databázi z konfiguraèního souboru.
            // Pokud øetìzec není nalezen, je vyvolána vıjimka.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // Registrace databázového kontextu (ApplicationDbContext) a konfigurace pouití PostgreSQL (Npgsql).
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Konfigurace autentizaèních slueb s pouitím Identity a cookies.
            builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();

            // Konfigurace a registrace slueb pro ASP.NET Core Identity.
            // Nastavuje pravidla pro hesla a propojuje Identity s databázovım kontextem.
            builder.Services.AddIdentityCore<ApplicationUser>(options =>
            {
                // Zde jsou zámìrnì oslabena pravidla pro hesla pro zjednodušení vıvoje.
                // V produkèním prostøedí by mìla bıt pravidla pøísnìjší.
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 3;
            })
                // Pøidání podpory pro uivatelské role (napø. "Admin", "User").
                .AddRoles<IdentityRole>()
                // Propojení Identity s Entity Framework Core a konkrétním databázovım kontextem.
                .AddEntityFrameworkStores<ApplicationDbContext>()
                // Pøidání SignInManageru, kterı spravuje operace pøihlášení a odhlášení.
                .AddSignInManager();

            // Zajišuje, e stav autentizace (informace o pøihlášeném uivateli) je dostupnı
            // v Blazor komponentách prostøednictvím kaskádovıch parametrù.
            builder.Services.AddCascadingAuthenticationState();

            // Registrace sluby, která poskytuje informace o stavu autentizace pro serverové Blazor aplikace.
            builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

            // Sestavení a vytvoøení instance webové aplikace na základì nakonfigurovanıch slueb.
            var app = builder.Build();

            // Konfigurace pipeline pro zpracování HTTP poadavkù v závislosti na prostøedí.
            // Pokud aplikace nebìí ve vıvojovém prostøedí, zapne se globální handler vıjimek a HSTS.
            if (!app.Environment.IsDevelopment())
            {
                // V pøípadì neošetøené vıjimky pøesmìruje uivatele na stránku "/error".
                app.UseExceptionHandler("/error");
                // Zapnutí HSTS (HTTP Strict Transport Security) pro zabezpeèení komunikace.
                app.UseHsts();
            }

            // Pøesmìrování všech HTTP poadavkù na HTTPS.
            app.UseHttpsRedirection();

            // Umonìní servírování statickıch souborù (CSS, JavaScript, obrázky) ze sloky wwwroot.
            app.UseStaticFiles();

            // Aktivace routovacího middleware, kterı na základì URL adresy rozhoduje, kterı endpoint se má vykonat.
            app.UseRouting();

            // Aktivace autentizaèního middleware, kterı ovìøuje identitu uivatele.
            app.UseAuthentication();

            // Aktivace autorizaèního middleware, kterı kontroluje, zda má uivatel oprávnìní k pøístupu.
            app.UseAuthorization();

            // Ochrana proti CSRF (Cross-Site Request Forgery) útokùm.
            app.UseAntiforgery();

            // Po spuštìní aplikace zavolá metodu, která zajistí vytvoøení základních rolí a administrátorského úètu,
            // pokud v databázi ještì neexistují.
            await SeedRolesAndAdminAsync(app.Services);

            // Mapování Razor Components na koøenovou komponentu App a zapnutí serverového renderování.
            app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

            // =================================================================
            // VLASTNÍ ENDPOINTY PRO PØIHLÁŠENÍ A ODHLÁŠENÍ
            // =================================================================

            // Tento endpoint zpracovává POST poadavek na adresu "/account/login".
            // Pouívá se pro pøihlášení uivatele na základì údajù z formuláøe (email a heslo).
            app.MapPost("/account/login", async (HttpContext httpContext, SignInManager<ApplicationUser> signInManager, [FromForm] string email, [FromForm] string password) =>
            {
                // Pokus o pøihlášení uivatele pomocí SignInManageru.
                var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    // V pøípadì úspìšného pøihlášení pøesmìruje uivatele na stránku "/moje-weby".
                    return Results.LocalRedirect("/moje-weby");
                }
                // Pokud pøihlášení sele, uivatel je pøesmìrován zpìt na pøihlašovací stránku s chybovou zprávou.
                var errorMessage = "Neplatné pøihlašovací údaje.";
                return Results.LocalRedirect($"/account/login?ErrorMessage={HttpUtility.UrlEncode(errorMessage)}");
            });

            // Tento endpoint zpracovává POST poadavek na adresu "/account/logout".
            // Slouí k odhlášení aktuálnì pøihlášeného uivatele.
            app.MapPost("/account/logout", async (HttpContext httpContext, SignInManager<ApplicationUser> signInManager) =>
            {
                // Odhlášení uivatele.
                await signInManager.SignOutAsync();
                // Pøesmìrování uivatele na hlavní stránku.
                return Results.LocalRedirect("/");
            });

            // Spuštìní aplikace. Aplikace zaène naslouchat na pøíchozí HTTP poadavky.
            app.Run();
        }

        /// <summary>
        /// Inicializuje databázi se základními daty, pokud ještì neexistují.
        /// Konkrétnì tato metoda vytváøí role "Admin" a "User" a zakládá administrátorskı úèet
        /// s pøeddefinovanımi pøihlašovacími údaji, aby bylo moné se do aplikace po prvním spuštìní pøihlásit.
        /// </summary>
        /// <param name="serviceProvider">Poskytovatel slueb pro získání instancí RoleManager a UserManager.</param>
        private static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            // Vytvoøení nového scope, aby bylo moné získat instance slueb s ivotním cyklem "scoped".
            using var scope = serviceProvider.CreateScope();

            // Získání instance RoleManageru pro správu rolí.
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Získání instance UserManageru pro správu uivatelù.
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Seznam rolí, které mají bıt v aplikaci vytvoøeny.
            string[] roleNames = { "Admin", "User" };

            // Procházení seznamu rolí a vytvoøení kadé z nich, pokud ještì neexistuje.
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Definice emailu pro administrátorskı úèet.
            var adminEmail = "admin@svatebniweb.cz";

            // Zjištìní, zda uivatel s danım emailem ji existuje.
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                // Pokud administrátor neexistuje, vytvoøí se novı.
                var adminUser = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };

                // Vytvoøení uivatele s heslem "admin".
                await userManager.CreateAsync(adminUser, "admin");

                // Pøiøazení role "Admin" novì vytvoøenému uivateli.
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
    }
}