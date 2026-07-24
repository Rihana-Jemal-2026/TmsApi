using Microsoft.Extensions.DependencyInjection;
namespace TmsApi.Api;
public class EnrollmentWorker
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EnrollmentWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void ProcessBatch()
    {
        using var scope = _scopeFactory.CreateScope();

       var service = scope.ServiceProvider.GetRequiredService<IEnrollmentService_M4>();
        // now safe to use scoped service
        // example test call:
        service.GetAllAsync().Wait();
    }
}