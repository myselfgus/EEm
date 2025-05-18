using EemCore.Agents;
using EemCore.Configuration;
using EemCore.Data;
using EemCore.Infrastructure;
using EemCore.Processing;
using EemCore.Services;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.MCP;
using Microsoft.OpenApi.Models;
using System.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Adicionar configurações
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.Configure<EemOptions>(builder.Configuration.GetSection("Eem"));
builder.Services.Configure<AzureOptions>(builder.Configuration.GetSection("Azure"));
builder.Services.Configure<McpOptions>(builder.Configuration.GetSection("MCP"));

// Adicionar serviços de logging e telemetria
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSingleton<ITelemetryInitializer, EemTelemetryInitializer>();
builder.Logging.AddApplicationInsights();

// Adicionar autenticação e autorização
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
    
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EemApiScope", policy =>
        policy.RequireAuthenticatedUser().RequireRole("EemUser"));
});

// Adicionar serviços de acesso a dados
builder.Services.AddEemDataServices();

// Adicionar agentes e processadores core
builder.Services.AddEemCoreServices();

// Adicionar serviços resilientes para Azure
builder.Services.AddEemAzureServices();

// Adicionar MCP Server
builder.Services.AddControllers();
builder.Services
    .AddMcpServer()
    .WithHttpServerTransport()
    .AddMcpToolType<CaptureAgent>()
    .AddMcpToolType<ContextAgent>()
    .AddMcpToolType<EulerianAgent>()
    .AddMcpToolType<CorrelationAgent>()
    .AddMcpToolType<GenAIScriptProcessor>();

// Adicionar health checks
builder.Services.AddHealthChecks()
    .AddAzureBlobStorage(
        name: "blob-storage",
        connectionString: sp => sp.GetRequiredService<IOptions<AzureOptions>>().Value.StorageConnectionString)
    .AddCosmosDb(
        name: "cosmos-db",
        cosmosDbEndpoint: sp => sp.GetRequiredService<IOptions<AzureOptions>>().Value.CosmosEndpoint,
        authKeyOrResourceToken: sp => sp.GetRequiredService<IOptions<AzureOptions>>().Value.CosmosKey)
    .AddCheck<EemAgentsHealthCheck>("eem-agents", HealthStatus.Degraded);

// Adicionar Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { 
        Title = "Εεm MCP Server API", 
        Version = "v1",
        Description = "API para o servidor MCP do sistema Εεm (Ευ-εnable-memory)"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Configurar endpoints
app.MapControllers();
app.MapMcpEndpoints();

// Configurar health checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = EemHealthCheckHelper.WriteHealthCheckResponse
});

// Configurar endpoint de metrics
app.MapHealthChecks("/metrics", new HealthCheckOptions
{
    Predicate = _ => false, // No health checks, just metrics
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        
        var metrics = new
        {
            timestamp = DateTime.UtcNow,
            version = typeof(Program).Assembly.GetName().Version?.ToString(),
            uptime = (DateTime.UtcNow - Process.GetCurrentProcess().StartTime).TotalSeconds,
            memory = GC.GetTotalMemory(false) / 1024 / 1024, // MB
            threads = Process.GetCurrentProcess().Threads.Count
        };
        
        await context.Response.WriteAsync(JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
    }
});

app.MapGet("/", () => Results.Redirect("/swagger"));

// Inicializar banco de dados e recursos
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<EemDataInitializer>();
    await initializer.InitializeAsync();
}

// Iniciar servidor MCP
app.Run();
