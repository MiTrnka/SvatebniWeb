using Microsoft.AspNetCore.Mvc.Testing;
using SvatebniWeb.Web;


namespace SvatebniWeb.IntegrationTests;

public class HomepageTest : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HomepageTest(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Homepage_ShouldReturn_SuccessStatusCode()
    {
        // Vytvori klienta, ktery bude posilat pozadavky na nasi aplikaci v pameti
        var client = _factory.CreateClient();

        // Posle pozadavek na hlavni stranku ("/")
        var response = await client.GetAsync("/");

        // Overi, ze odpoved byla uspesna (napr. status kód 200 OK)
        response.EnsureSuccessStatusCode();
    }
}