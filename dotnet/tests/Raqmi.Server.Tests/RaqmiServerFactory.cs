using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Raqmi.Server.Tests;

public class RaqmiServerFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DEMO_MODE", "true");
        builder.UseSetting("PORT", "0");
        builder.UseSetting("ConnectionStrings:Default", "");
    }
}
