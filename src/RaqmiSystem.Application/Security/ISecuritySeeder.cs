namespace RaqmiSystem.Application.Security;

public interface ISecuritySeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}
