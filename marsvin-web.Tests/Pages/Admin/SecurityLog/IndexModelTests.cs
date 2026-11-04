using MarsvinWebExample.Data;
using MarsvinWebExample.Models;
using MarsvinWebExample.Tests.Data;
using PageModel = MarsvinWebExample.Pages.Admin.SecurityLog.IndexModel;

namespace MarsvinWebExample.Tests.Pages.Admin.SecurityLog;

[Collection("SqlCatalog collection")]
public class IndexModelTests(SqlCatalogFixture fixture)
{
    [Fact]
    public void OnGet_ShowsTheRecordedEvents_AndCountsTheLast24Hours()
    {
        var store = new SqlSecurityEventStore(fixture.ConnectionString);
        var ip = "ip-" + Guid.NewGuid().ToString("N");
        store.Record(new SecurityEvent { Type = SecurityEventType.AccessDenied, IpAddress = ip, Path = "/Admin/Users" });
        var model = new PageModel(store);

        model.OnGet();

        Assert.Contains(model.Events, e => e.IpAddress == ip && e.Type == SecurityEventType.AccessDenied);
        Assert.True(model.Last24Hours >= 1);
    }
}
