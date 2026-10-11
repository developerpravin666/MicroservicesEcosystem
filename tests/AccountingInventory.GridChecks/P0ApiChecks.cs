using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using AccountingInventory.Api.Endpoints;
using AccountingInventory.Application;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using AccountingInventory.Infrastructure.Persistence.MultiTenancy;
using AccountingInventory.Infrastructure.Security;
using BuildingBlocks.Application.Exceptions;
using BuildingBlocks.Domain.MultiTenancy;
using BuildingBlocks.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal static class P0ApiChecks
{
    public static async Task RunAsync(string connectionString)
    {
        var tenants = new[] { Tenant.Create("p0_api_check_a", "p0-api-a"), Tenant.Create("p0_api_check_b", "p0-api-b") };
        foreach (var tenant in tenants) tenant.Activate();
        var directorySchema = "api_directory_check_" + Guid.NewGuid().ToString("N");
        var schemas = tenants.Select(x => x.SchemaName).Append(directorySchema).ToArray();
        await using var control = new NpgsqlConnection(connectionString); await control.OpenAsync();
        var owners = new List<User>(); var saleIds = new List<Guid>();
        WebApplication? app = null;
        try
        {
            foreach (var schema in schemas)
            { await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", control); await command.ExecuteNonQueryAsync(); }
            DbContextOptions<AccountingInventoryDbContext> Options(string schema) => new DbContextOptionsBuilder<AccountingInventoryDbContext>()
                .UseNpgsql(new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString).UseSnakeCaseNamingConvention().Options;
            var directoryOptions = new DbContextOptionsBuilder<FixtureDirectory>()
                .UseNpgsql(new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = directorySchema }.ConnectionString).UseSnakeCaseNamingConvention().Options;
            await using (var directory = new FixtureDirectory(directoryOptions))
            { await directory.Database.ExecuteSqlRawAsync(directory.Database.GenerateCreateScript()); directory.Tenants.AddRange(tenants); await directory.SaveChangesAsync(); }
            foreach (var tenant in tenants)
            {
                await using var db = new AccountingInventoryDbContext(Options(tenant.SchemaName), new FixtureProvider(tenant.SchemaName));
                await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
                var owner = User.Create("owner", "9000000000", "owner@example.com"); owner.SetOwner(true); owner.AcceptInvitation(); owner.SetPasswordHash("fixture"); owners.Add(owner);
                var customer = Customer.Create("Customer", "8000000000", "Address", null);
                var vendor = Vendor.Create("Vendor", "V", "7000000000", "vendor@example.com");
                var brand = Brand.Create("Brand", "Brand"); var type = ProductType.Create("Type", "Type"); var model = ProductModel.Create(brand.Id, type.Id, "M", "Model", "Model");
                var variant = Variant.Create("Variant", "Variant"); var color = Color.Create("Blue", "Blue");
                db.AddRange(owner, customer, vendor, brand, type, model, variant, color);
                var product = Product.Create(vendor.Id, brand.Id, type.Id, model.Id, variant.Id, color.Id,"API-SERIAL", "API-ALT", "API-PURCHASE",100,0,9,9,18,new(2026,1,1));
                var sale = SalesProduct.Create("API-SALE",product.Id.ToString(),customer.Id,new(2026,1,2),100,200,0,null,9,9); saleIds.Add(sale.Id);
                product.MarkSold(); db.AddRange(product,sale); var accounts = await LedgerPosting.EnsureSystemAccountsAsync(db,default);
                LedgerPosting.Add(db,LedgerPosting.ForPurchase(product,accounts)); LedgerPosting.Add(db,LedgerPosting.ForSale(sale,product,accounts));
                var receipt = SalesReceipt.Create(sale.Id,236,"Cash",new(2026,1,2),null,null); db.SalesReceipts.Add(receipt); LedgerPosting.Add(db,LedgerPosting.ForSalesReceipt(receipt,accounts)); await db.SaveChangesAsync();
            }
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            // This fixture host exposes only the accounting routes and supplies no control-plane provisioning/messaging services.
            builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Jwt:Issuer"]="p0-api-fixture",["Jwt:Audience"]="p0-api-fixture",["Jwt:SigningKey"]="P0-FIXTURE-ONLY-KEY-NOT-FOR-PRODUCTION-1234567890",["Frontend:BaseUrl"]="https://example.test" });
            builder.Services.AddPlatformSecurity(builder.Configuration); builder.Services.AddApplication();
            builder.Services.AddScoped<ITenantDirectoryContext>(_ => new FixtureDirectory(directoryOptions));
            builder.Services.AddScoped<ITenantProvider, DynamicTenantProvider>();
            builder.Services.AddScoped(sp => new AccountingInventoryDbContext(Options(sp.GetRequiredService<ITenantProvider>().SchemaName),sp.GetRequiredService<ITenantProvider>()));
            builder.Services.AddScoped<IAccountingInventoryDbContext>(sp=>sp.GetRequiredService<AccountingInventoryDbContext>());
            builder.Services.AddScoped<IRequestIdentity, HttpActor>(); builder.Services.AddSingleton<IEmailSender, NoMail>();
            builder.Services.AddSingleton<ITokenService, TokenService>();
            builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BusinessTransactionBehavior<,>));
            app=builder.Build(); app.Use(async (http,next)=>{
                try { await next(); } catch (Exception e) when(e is ForbiddenException or NotFoundException or ConflictException or FluentValidation.ValidationException) {
                    http.Response.StatusCode=e is ForbiddenException?403:e is NotFoundException?404:e is ConflictException?409:400;
                    await http.Response.WriteAsJsonAsync(new {message=e.Message});
                }
            }); app.UsePlatformSecurity(); app.MapAccountingP0Endpoints(); app.MapSalesAccountingEndpoints();
            app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
            using var client=new HttpClient { BaseAddress=new Uri(app.Urls.Single()),Timeout=TimeSpan.FromSeconds(20) };
            var tokens = new TokenService(builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!);
            var token = tokens.GenerateTokenPair(owners[0].Id,"owner",tenants[0].Id,tenants[0].SchemaName,[],[]).AccessToken;
            async Task<HttpResponseMessage> Send(string path, HttpMethod method, object? body=null, string? jwt=null, Guid? header=null) {
                var request=new HttpRequestMessage(method,path); if(jwt is not null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",jwt);
                request.Headers.Add("X-Tenant-Id",(header??tenants[0].Id).ToString()); if(body is not null)request.Content=JsonContent.Create(body);
                return await client.SendAsync(request);
            }
            var checks=0; void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
            Check((await Send("/api/accounting/access",HttpMethod.Get)).StatusCode==HttpStatusCode.Unauthorized,"Anonymous accounting API access must fail");
            Check((await Send("/api/accounting/access",HttpMethod.Get,jwt:token,header:tenants[1].Id)).StatusCode==HttpStatusCode.Forbidden,"Signed tenant A token must not accept tenant B header");
            Check((await Send("/api/accounting/access",HttpMethod.Get,jwt:token+"tampered")).StatusCode==HttpStatusCode.Unauthorized,"Tampered JWT must fail");
            Check((await Send($"/api/sales/accounting/bills/{saleIds[1]}",HttpMethod.Get,jwt:token)).StatusCode==HttpStatusCode.NotFound,"Tenant A must not read tenant B invoice IDs");
            var body=new ReturnInvoiceCommand("Sale",saleIds[0],new(2026,1,3),"API return","Restock");
            Check((await Send("/api/accounting/corrections",HttpMethod.Post,body with{SourceId=saleIds[1]},token)).StatusCode==HttpStatusCode.NotFound,"Cross-tenant source correction must fail");
            var response=await Send("/api/accounting/corrections",HttpMethod.Post,body,token);
            Check(response.IsSuccessStatusCode,"Owner must be able to post a linked return through the API");
            var result=await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var noteId=result.GetProperty("id").GetGuid();
            Check((await Send("/api/accounting/corrections",HttpMethod.Post,body,token)).StatusCode==HttpStatusCode.Conflict,"API duplicate return must fail");
            Check((await Send($"/api/accounting/corrections/{noteId}/refunds",HttpMethod.Post,new {paymentDate="2026-01-03",amount=237,paymentMode="Cash"},token)).StatusCode==HttpStatusCode.Conflict,"API over-refund must fail");
            Check((await Send($"/api/accounting/corrections/{noteId}/refunds",HttpMethod.Post,new {paymentDate="2026-01-03",amount=236,paymentMode="Cash"},token)).IsSuccessStatusCode,"API refund must post");
            await using(var db=new AccountingInventoryDbContext(Options(tenants[0].SchemaName),new FixtureProvider(tenants[0].SchemaName))) {
                var staff=User.Create("staff","9000000001","staff@example.com");staff.AcceptInvitation();staff.SetPasswordHash("fixture");db.Users.Add(staff);await db.SaveChangesAsync();
                var staffToken=tokens.GenerateTokenPair(staff.Id,"staff",tenants[0].Id,tenants[0].SchemaName,[],[]).AccessToken;
                Check((await Send("/api/accounting/corrections",HttpMethod.Post,body,staffToken)).StatusCode==HttpStatusCode.Forbidden,"API staff writes require sales.manage before handler execution");
                Check(await db.InvoiceCorrections.CountAsync()==1 && await db.CorrectionRefunds.CountAsync()==1,"Rejected API writes must not leave partial records");
            }
            Console.WriteLine($"PASS: {checks} P0 API/JWT/tenant-isolation checks");
        }
        finally
        {
            if(app is not null){await app.StopAsync();await app.DisposeAsync();}
            foreach(var schema in schemas){await using var cleanup=new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE",control);await cleanup.ExecuteNonQueryAsync();}
        }
    }
    private sealed class FixtureProvider(string schema):ITenantProvider { public string SchemaName=>schema; }
    private sealed class HttpActor(IHttpContextAccessor http):IRequestIdentity { public Guid? UserId=>Guid.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:null; }
    private sealed class NoMail:IEmailSender { public Task SendAsync(string recipient,string subject,string htmlBody,CancellationToken cancellationToken)=>Task.CompletedTask; public Task<bool> SendTestAsync(string recipient, CancellationToken cancellationToken) => Task.FromResult(false); }
    private sealed class FixtureDirectory(DbContextOptions<FixtureDirectory> options):DbContext(options),ITenantDirectoryContext
    {
        public DbSet<Tenant> Tenants=>Set<Tenant>();
        protected override void OnModelCreating(ModelBuilder model){model.Entity<Tenant>().ToTable("tenants");model.Entity<Tenant>().HasKey(x=>x.Id);model.Entity<Tenant>().Ignore(x=>x.DomainEvents);}
    }
}
