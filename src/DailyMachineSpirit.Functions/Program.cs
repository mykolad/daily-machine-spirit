using DailyMachineSpirit.Data;
using DailyMachineSpirit.Data.Repositories;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// ASP.NET Core integration: HTTP functions take HttpRequest and return IActionResult.
builder.ConfigureFunctionsWebApplication();

// Entra ID only (Authentication=Active Directory Managed Identity in Azure, Active Directory Default locally): no password
// anywhere. The longer connect timeout lets a paused serverless database (staging) resume during the login.
builder.Services.AddDbContext<MachineSpiritDbContext>(options =>
    options.UseSqlServer(SqlConnectionStrings.WithResumeTimeout(builder.Configuration.GetConnectionString("DefaultConnection") ?? ""),
        MachineSpiritDbContext.ConfigureSqlServer));
builder.Services.AddScoped<IItemRepository, ItemRepository>();

builder.Build().Run();
