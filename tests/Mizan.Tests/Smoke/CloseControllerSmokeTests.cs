namespace Mizan.Tests.Smoke;

// A smoke test, not an exact-total test (that's AccountBalanceServiceTests/
// MonthlyFlowsServiceTests) — deliberately doesn't assert specific figures, since which month
// this page shows depends on when the test happens to run, and the seed data has a fixed end
// date. What must always be true regardless of when this runs: the page loads, and it's showing
// real accounts from the database, not a template's mock content.
[Collection("Database")]
public class CloseControllerSmokeTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Close_page_loads_and_shows_seeded_accounts()
    {
        using var factory = new MizanWebApplicationFactory(fixture.Connection);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Savings rate", body);
        Assert.Contains("Bank - Current", body);
    }
}
