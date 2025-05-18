# Guia de Implementação do Servidor MCP Εεm

Este guia fornece instruções passo a passo para implementar o servidor MCP (Model Context Protocol) do sistema Εεm, utilizando Visual Studio Enterprise e Azure Sponsorship do Founders Hub.

## Arquivos para Exclusão

Estes arquivos podem ser excluídos pois serão substituídos ou integrados de forma mais eficiente:

```
- repository-interfaces.cs (após integração em EemCore/Data/Repositories)
- resilient-services.cs (após integração em EemCore/Services)
- service-extensions.cs (após integração em EemCore/Infrastructure)
- configuration-classes.cs (após integração em EemCore/Configuration)
- updated-eemcore-csproj.txt (após aplicação)
- updated-eemserver-csproj.txt (após aplicação)
- updated-program.cs (após aplicação)
```

## 1. Atualização dos Arquivos de Projeto

### EemCore.csproj

Substitua o conteúdo do arquivo EemCore.csproj por:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>

  <ItemGroup>
    <!-- Azure Services -->
    <PackageReference Include="Azure.AI.OpenAI" Version="1.0.0" />
    <PackageReference Include="Azure.Storage.Blobs" Version="12.19.1" />
    <PackageReference Include="Microsoft.Azure.Cosmos" Version="3.37.0" />
    
    <!-- Azure Functions -->
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="1.20.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Timer" Version="4.3.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http" Version="3.1.0" />
    
    <!-- Semantic Kernel & MCP -->
    <PackageReference Include="Microsoft.SemanticKernel" Version="1.5.0" />
    <PackageReference Include="Microsoft.SemanticKernel.Connectors.AzureAI" Version="1.5.0" />
    <PackageReference Include="Microsoft.MCP" Version="1.0.0" />
    
    <!-- Monitoring & Resilience -->
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.ApplicationInsights" Version="2.22.0" />
    <PackageReference Include="Microsoft.ApplicationInsights.WorkerService" Version="2.22.0" />
    
    <!-- Utilities -->
    <PackageReference Include="Microsoft.Extensions.Azure" Version="1.7.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="8.0.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="8.0.0" />
    <PackageReference Include="Polly" Version="8.2.0" />
    <PackageReference Include="FluentValidation" Version="11.8.1" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="EemCore.Tests" />
  </ItemGroup>

</Project>
```

### EemServer.csproj

Substitua o conteúdo do arquivo EemServer.csproj por:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>true</InvariantGlobalization>
    <UserSecretsId>eem-mcp-server</UserSecretsId>
    <DockerDefaultTargetOS>Linux</DockerDefaultTargetOS>
    <DockerfileFile>../Dockerfile</DockerfileFile>
  </PropertyGroup>

  <ItemGroup>
    <!-- ASP.NET Core -->
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.2" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    
    <!-- Azure Functions & MCP -->
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="1.20.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Timer" Version="4.3.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http" Version="3.1.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="1.16.2" />
    <PackageReference Include="Microsoft.MCP" Version="1.0.0" />
    <PackageReference Include="Microsoft.MCP.Server" Version="1.0.0" />
    
    <!-- Monitoring & Health Checks -->
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="8.0.0" />
    <PackageReference Include="AspNetCore.HealthChecks.AzureStorage" Version="7.0.0" />
    <PackageReference Include="AspNetCore.HealthChecks.CosmosDb" Version="7.0.0" />
    <PackageReference Include="AspNetCore.HealthChecks.AzureOpenAI" Version="7.0.0" />
    <PackageReference Include="Microsoft.ApplicationInsights.AspNetCore" Version="2.22.0" />
    
    <!-- Security -->
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.2" />
    <PackageReference Include="Microsoft.Identity.Web" Version="2.16.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\EemCore\EemCore.csproj" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="EemServer.Tests" />
  </ItemGroup>

</Project>
```

## 2. Estrutura de Pastas Necessária

Crie as seguintes pastas no projeto EemCore:

```
EemCore/
  ├── Configuration/
  ├── Data/
  │    ├── Repositories/
  │    └── Models/
  ├── Services/
  │    ├── Resilient/
  │    └── Interfaces/
  └── Infrastructure/
```

## 3. Implementação dos Componentes Principais

### Configuration/EemOptions.cs

```csharp
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;

namespace EemCore.Configuration
{
    /// <summary>
    /// Opções de configuração principal para o sistema Εεm
    /// </summary>
    public class EemOptions
    {
        /// <summary>
        /// Intervalo de tempo em minutos para a captura automática de atividades
        /// </summary>
        public int CaptureInterval { get; set; } = 15;
        
        /// <summary>
        /// Período de retenção de dados em dias
        /// </summary>
        public int RetentionPeriodDays { get; set; } = 90;
        
        /// <summary>
        /// Define se o processamento euleriano está habilitado
        /// </summary>
        public bool EnableEulerianProcessing { get; set; } = true;
        
        /// <summary>
        /// Define se a análise de correlação está habilitada
        /// </summary>
        public bool EnableCorrelationAnalysis { get; set; } = true;
        
        /// <summary>
        /// Número máximo de eventos por atividade
        /// </summary>
        public int MaxEventsPerActivity { get; set; } = 1000;
        
        /// <summary>
        /// Número máximo de atividades por sessão
        /// </summary>
        public int MaxActivitiesPerSession { get; set; } = 100;
        
        /// <summary>
        /// Nível de enriquecimento de contexto (1-5)
        /// </summary>
        public int EnrichmentLevel { get; set; } = 3;
        
        /// <summary>
        /// Configurações de privacidade
        /// </summary>
        public PrivacySettings PrivacySettings { get; set; } = new();
    }
    
    /// <summary>
    /// Configurações de privacidade para filtragem de dados
    /// </summary>
    public class PrivacySettings
    {
        /// <summary>
        /// Indica se dados pessoais devem ser excluídos
        /// </summary>
        public bool ExcludePersonalData { get; set; } = true;
        
        /// <summary>
        /// Indica se segredos devem ser excluídos
        /// </summary>
        public bool ExcludeSecrets { get; set; } = true;
        
        /// <summary>
        /// Tipos de arquivo a serem considerados
        /// </summary>
        public string[] FilterFileTypes { get; set; } = new[] 
        { 
            ".cs", ".fs", ".md", ".json", ".xml", ".csproj", ".fsproj" 
        };
    }
    
    /// <summary>
    /// Opções de configuração para serviços Azure
    /// </summary>
    public class AzureOptions
    {
        /// <summary>
        /// String de conexão do Azure Storage
        /// </summary>
        public string StorageConnectionString { get; set; } = string.Empty;
        
        /// <summary>
        /// Endpoint do Cosmos DB
        /// </summary>
        public string CosmosEndpoint { get; set; } = string.Empty;
        
        /// <summary>
        /// Chave do Cosmos DB
        /// </summary>
        public string CosmosKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Nome do banco de dados Cosmos
        /// </summary>
        public string CosmosDatabase { get; set; } = "EemDatabase";
        
        /// <summary>
        /// Endpoint do Azure OpenAI
        /// </summary>
        public string OpenAIEndpoint { get; set; } = string.Empty;
        
        /// <summary>
        /// Chave do Azure OpenAI
        /// </summary>
        public string OpenAIKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Nome do deployment do modelo de chat
        /// </summary>
        public string OpenAIDeployment { get; set; } = "gpt-4";
        
        /// <summary>
        /// Nome do modelo de embeddings
        /// </summary>
        public string OpenAIEmbeddingModel { get; set; } = "text-embedding-ada-002";
    }
    
    /// <summary>
    /// Opções de configuração para o servidor MCP
    /// </summary>
    public class McpOptions
    {
        /// <summary>
        /// Porta para o servidor MCP
        /// </summary>
        public int Port { get; set; } = 5100;
        
        /// <summary>
        /// Indica se a autenticação está habilitada
        /// </summary>
        public bool AuthEnabled { get; set; } = true;
        
        /// <summary>
        /// Chave de autenticação para MCP
        /// </summary>
        public string AuthKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Número máximo de requisições concorrentes
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 100;
        
        /// <summary>
        /// Timeout padrão em segundos
        /// </summary>
        public int DefaultTimeoutSeconds { get; set; } = 30;
        
        /// <summary>
        /// Tamanho máximo de contexto em tokens
        /// </summary>
        public int MaxContextSize { get; set; } = 16384;
    }
    
    /// <summary>
    /// Inicializador de telemetria para Application Insights
    /// </summary>
    public class EemTelemetryInitializer : ITelemetryInitializer
    {
        private readonly EemOptions _options;
        
        public EemTelemetryInitializer(IOptions<EemOptions> options)
        {
            _options = options.Value;
        }
        
        public void Initialize(ITelemetry telemetry)
        {
            telemetry.Context.Cloud.RoleName = "EemMcpServer";
            telemetry.Context.Component.Version = GetType().Assembly.GetName().Version?.ToString();
            
            // Adicionar propriedades personalizadas
            telemetry.Context.GlobalProperties["CaptureInterval"] = _options.CaptureInterval.ToString();
            telemetry.Context.GlobalProperties["EulerianProcessingEnabled"] = _options.EnableEulerianProcessing.ToString();
            telemetry.Context.GlobalProperties["CorrelationAnalysisEnabled"] = _options.EnableCorrelationAnalysis.ToString();
        }
    }
}
```

### Services/Resilient/ResilientServices.cs

```csharp
using Azure.AI.OpenAI;
using Azure.Storage.Blobs;
using EemCore.Agents;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Polly;

namespace EemCore.Services.Resilient
{
    /// <summary>
    /// Cliente resiliente para Azure Blob Storage
    /// </summary>
    public class ResilientBlobServiceClient
    {
        private readonly BlobServiceClient _innerClient;
        private readonly ResiliencePipeline _pipeline;
        
        public ResilientBlobServiceClient(BlobServiceClient innerClient, ResiliencePipeline pipeline)
        {
            _innerClient = innerClient;
            _pipeline = pipeline;
        }
        
        /// <summary>
        /// Obtém um cliente de container resiliente
        /// </summary>
        public BlobContainerClient GetBlobContainerClient(string containerName)
        {
            return _innerClient.GetBlobContainerClient(containerName);
        }
        
        /// <summary>
        /// Cria um container de forma resiliente
        /// </summary>
        public async Task<BlobContainerClient> CreateBlobContainerAsync(
            string containerName, 
            CancellationToken cancellationToken = default)
        {
            return await _pipeline.ExecuteAsync(async token => 
            {
                var container = _innerClient.GetBlobContainerClient(containerName);
                await container.CreateIfNotExistsAsync(cancellationToken: token);
                return container;
            }, cancellationToken);
        }
    }
    
    /// <summary>
    /// Cliente resiliente para Cosmos DB
    /// </summary>
    public class ResilientCosmosClient
    {
        private readonly CosmosClient _innerClient;
        private readonly ResiliencePipeline _pipeline;
        
        public ResilientCosmosClient(CosmosClient innerClient, ResiliencePipeline pipeline)
        {
            _innerClient = innerClient;
            _pipeline = pipeline;
        }
        
        /// <summary>
        /// Obtém um container resiliente
        /// </summary>
        public Container GetContainer(string databaseId, string containerId)
        {
            return _innerClient.GetContainer(databaseId, containerId);
        }
        
        /// <summary>
        /// Cria um banco de dados de forma resiliente
        /// </summary>
        public async Task<DatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string databaseId, 
            int? throughput = null,
            CancellationToken cancellationToken = default)
        {
            return await _pipeline.ExecuteAsync(async token => 
            {
                return await _innerClient.CreateDatabaseIfNotExistsAsync(
                    databaseId, throughput, cancellationToken: token);
            }, cancellationToken);
        }
        
        /// <summary>
        /// Cria um container de forma resiliente
        /// </summary>
        public async Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            string databaseId, 
            string containerId, 
            string partitionKeyPath,
            int? throughput = null,
            CancellationToken cancellationToken = default)
        {
            return await _pipeline.ExecuteAsync(async token => 
            {
                var database = _innerClient.GetDatabase(databaseId);
                return await database.CreateContainerIfNotExistsAsync(
                    containerId, partitionKeyPath, throughput, cancellationToken: token);
            }, cancellationToken);
        }
    }
    
    /// <summary>
    /// Cliente resiliente para Azure OpenAI
    /// </summary>
    public class ResilientOpenAIClient
    {
        private readonly OpenAIClient _innerClient;
        private readonly ResiliencePipeline _pipeline;
        
        public ResilientOpenAIClient(OpenAIClient innerClient, ResiliencePipeline pipeline)
        {
            _innerClient = innerClient;
            _pipeline = pipeline;
        }
        
        /// <summary>
        /// Obtém completions de chat de forma resiliente
        /// </summary>
        public async Task<Azure.Response<ChatCompletions>> GetChatCompletionsAsync(
            ChatCompletionsOptions options,
            CancellationToken cancellationToken = default)
        {
            return await _pipeline.ExecuteAsync(async token => 
            {
                return await _innerClient.GetChatCompletionsAsync(options, token);
            }, cancellationToken);
        }
        
        /// <summary>
        /// Obtém embeddings de forma resiliente
        /// </summary>
        public async Task<Azure.Response<Embeddings>> GetEmbeddingsAsync(
            EmbeddingsOptions options,
            CancellationToken cancellationToken = default)
        {
            return await _pipeline.ExecuteAsync(async token => 
            {
                return await _innerClient.GetEmbeddingsAsync(options, token);
            }, cancellationToken);
        }
    }
}
```

### Infrastructure/ServiceExtensions.cs

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Storage.Blobs;
using EemCore.Agents;
using EemCore.Configuration;
using EemCore.Data.Repositories;
using EemCore.Processing;
using EemCore.Services.Resilient;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Polly;
using Polly.Registry;
using System.Net;

namespace EemCore.Infrastructure
{
    /// <summary>
    /// Extensões para configuração de serviços do Εεm
    /// </summary>
    public static class ServiceExtensions
    {
        /// <summary>
        /// Adiciona serviços de acesso a dados do Εεm
        /// </summary>
        public static IServiceCollection AddEemDataServices(this IServiceCollection services)
        {
            // Repositórios
            services.AddScoped<IActivityRepository, ActivityRepository>();
            services.AddScoped<IRelationRepository, RelationRepository>();
            services.AddScoped<IEulerianFlowRepository, EulerianFlowRepository>();
            services.AddScoped<IScriptRepository, ScriptRepository>();
            
            // Inicializador de dados
            services.AddScoped<EemDataInitializer>();
            
            return services;
        }
        
        /// <summary>
        /// Adiciona serviços core do Εεm
        /// </summary>
        public static IServiceCollection AddEemCoreServices(this IServiceCollection services)
        {
            // Agentes
            services.AddScoped<CaptureAgent>();
            services.AddScoped<ContextAgent>();
            services.AddScoped<CorrelationAgent>();
            services.AddScoped<EulerianAgent>();
            
            // Processadores
            services.AddScoped<GenAIScriptProcessor>();
            services.AddScoped<IdeIntegrationService>();
            services.AddScoped<ContextEnrichmentService>();
            
            // Health checks
            services.AddSingleton<EemAgentsHealthCheck>();
            
            return services;
        }
        
        /// <summary>
        /// Adiciona serviços Azure com resiliência e telemetria
        /// </summary>
        public static IServiceCollection AddEemAzureServices(this IServiceCollection services)
        {
            // Configurar política de resiliência
            services.AddSingleton<IResiliencePipelineRegistry>(serviceProvider =>
            {
                var registry = new ResiliencePipelineRegistry();
                
                // Política de retry para Azure Storage
                registry.TryAddBuilder("BlobStorage", (builder, _) => builder
                    .AddRetry(new()
                    {
                        MaxRetryAttempts = 5,
                        BackoffType = DelayBackoffType.Exponential,
                        BaseDelay = TimeSpan.FromSeconds(1),
                        MaxDelay = TimeSpan.FromSeconds(10)
                    })
                    .AddTimeout(TimeSpan.FromSeconds(30)));
                
                // Política de retry para Cosmos DB
                registry.TryAddBuilder("CosmosDB", (builder, _) => builder
                    .AddRetry(new()
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        BaseDelay = TimeSpan.FromSeconds(1),
                        ShouldHandle = new PredicateBuilder().Handle<CosmosException>(ex => 
                            ex.StatusCode == HttpStatusCode.TooManyRequests || 
                            ex.StatusCode == HttpStatusCode.ServiceUnavailable)
                    })
                    .AddTimeout(TimeSpan.FromSeconds(20)));
                
                // Política de retry para Azure OpenAI
                registry.TryAddBuilder("OpenAI", (builder, _) => builder
                    .AddRetry(new()
                    {
                        MaxRetryAttempts = 4,
                        BackoffType = DelayBackoffType.Exponential,
                        BaseDelay = TimeSpan.FromSeconds(2),
                        MaxDelay = TimeSpan.FromSeconds(16)
                    })
                    .AddTimeout(TimeSpan.FromSeconds(60)));
                
                return registry;
            });
            
            // Configurar Azure Storage
            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AzureOptions>>().Value;
                var registry = sp.GetRequiredService<IResiliencePipelineRegistry>();
                
                var blobServiceClient = new BlobServiceClient(options.StorageConnectionString);
                
                return new ResilientBlobServiceClient(
                    blobServiceClient,
                    registry.GetPipeline("BlobStorage"));
            });
            
            // Configurar Cosmos DB
            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AzureOptions>>().Value;
                var registry = sp.GetRequiredService<IResiliencePipelineRegistry>();
                
                var cosmosClientOptions = new CosmosClientOptions
                {
                    SerializerOptions = new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                    },
                    ConnectionMode = ConnectionMode.Direct,
                    ApplicationName = "EemServer"
                };
                
                var cosmosClient = new CosmosClient(options.CosmosEndpoint, options.CosmosKey, cosmosClientOptions);
                
                return new ResilientCosmosClient(
                    cosmosClient,
                    registry.GetPipeline("CosmosDB"));
            });
            
            // Configurar Azure OpenAI
            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AzureOptions>>().Value;
                var registry = sp.GetRequiredService<IResiliencePipelineRegistry>();
                
                var openAIClient = new OpenAIClient(
                    new Uri(options.OpenAIEndpoint),
                    new AzureKeyCredential(options.OpenAIKey));
                
                return new ResilientOpenAIClient(
                    openAIClient,
                    registry.GetPipeline("OpenAI"));
            });
            
            // Configurar Semantic Kernel
            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AzureOptions>>().Value;
                
                var kernelBuilder = Kernel.CreateBuilder();
                
                kernelBuilder.AddAzureOpenAIChatCompletion(
                    options.OpenAIDeployment,
                    options.OpenAIEndpoint,
                    options.OpenAIKey);
                
                return kernelBuilder.Build();
            });
            
            // Configurar Memória Semântica
            services.AddSingleton<ISemanticTextMemory>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AzureOptions>>().Value;
                
                return new MemoryBuilder()
                    .WithAzureOpenAITextEmbeddingGeneration(
                        options.OpenAIEmbeddingModel,
                        options.OpenAIEndpoint,
                        options.OpenAIKey)
                    .WithCosmosDbMemoryStore(
                        options.CosmosEndpoint,
                        options.CosmosKey,
                        options.CosmosDatabase,
                        "EmbeddingsCollection")
                    .Build();
            });
            
            return services;
        }
    }
}
```

### Data/Repositories/IRepositories.cs

```csharp
using EemCore.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EemCore.Data.Repositories
{
    /// <summary>
    /// Interface para repositório de eventos de atividade
    /// </summary>
    public interface IActivityRepository
    {
        /// <summary>
        /// Salva um novo evento de atividade no armazenamento
        /// </summary>
        Task<string> SaveActivityEventAsync(ActivityJournalEvent activityEvent, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obtém eventos de atividade para uma sessão específica
        /// </summary>
        Task<IEnumerable<ActivityJournalEvent>> GetActivityEventsForSessionAsync(string sessionId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obtém eventos de atividade dentro de uma janela de tempo específica
        /// </summary>
        Task<IEnumerable<ActivityJournalEvent>> GetActivityEventsInTimeRangeAsync(
            DateTime startTime, 
            DateTime endTime, 
            int maxEvents = 1000,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Busca eventos de atividade baseado em uma consulta textual
        /// </summary>
        Task<IEnumerable<ActivityJournalEvent>> SearchActivityEventsAsync(
            string searchQuery, 
            int maxResults = 10,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Exclui eventos mais antigos que um período de retenção
        /// </summary>
        Task<int> PurgeOldEventsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Interface para repositório de relações interpretadas
    /// </summary>
    public interface IRelationRepository
    {
        /// <summary>
        /// Salva um novo evento de relação no armazenamento
        /// </summary>
        Task<string> SaveRelationEventAsync(InterpretedRelationEvent relationEvent, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obtém relações para eventos de atividade específicos
        /// </summary>
        Task<IEnumerable<InterpretedRelationEvent>> GetRelationsForEventsAsync(
            IEnumerable<string> activityEventIds, 
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Obtém relações dentro de uma janela de tempo específica
        /// </summary>
        Task<IEnumerable<InterpretedRelationEvent>> GetRelationsInTimeRangeAsync(
            DateTime startTime, 
            DateTime endTime, 
            int maxRelations = 1000,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Busca relações baseado em uma consulta textual
        /// </summary>
        Task<IEnumerable<InterpretedRelationEvent>> SearchRelationsAsync(
            string searchQuery, 
            int maxResults = 10,
            CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Interface para repositório de fluxos eulerianos
    /// </summary>
    public interface IEulerianFlowRepository
    {
        /// <summary>
        /// Salva um novo fluxo euleriano no armazenamento
        /// </summary>
        Task<string> SaveFlowAsync(EulerianFlow flow, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obtém fluxos eulerianos dentro de uma janela de tempo específica
        /// </summary>
        Task<IEnumerable<EulerianFlow>> GetFlowsInTimeRangeAsync(
            DateTime startTime, 
            DateTime endTime, 
            int maxFlows = 100,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Obtém um fluxo euleriano específico pelo ID
        /// </summary>
        Task<EulerianFlow?> GetFlowByIdAsync(string flowId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Busca fluxos eulerianos baseado em uma consulta textual
        /// </summary>
        Task<IEnumerable<EulerianFlow>> SearchFlowsAsync(
            string searchQuery, 
            int maxResults = 10,
            CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Interface para repositório de scripts GenAIScript
    /// </summary>
    public interface IScriptRepository
    {
        /// <summary>
        /// Salva um novo script no armazenamento
        /// </summary>
        Task<string> SaveScriptAsync(ScriptInfo script, string content, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obtém um script pelo nome
        /// </summary>
        Task<(ScriptInfo Info, string Content)?> GetScriptByNameAsync(string scriptName, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Lista todos os scripts disponíveis
        /// </summary>
        Task<IEnumerable<ScriptInfo>> ListScriptsAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Exclui um script específico
        /// </summary>
        Task<bool> DeleteScriptAsync(string scriptId, CancellationToken cancellationToken = default);
    }
}
```

### Data/Repositories/BaseBlobRepository.cs

```csharp
using EemCore.Services.Resilient;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Memory;

namespace EemCore.Data.Repositories
{
    /// <summary>
    /// Classe base abstrata para repositórios que usam Azure Blob Storage
    /// </summary>
    public abstract class BaseBlobRepository
    {
        protected readonly ResilientBlobServiceClient _blobServiceClient;
        protected readonly ISemanticTextMemory _memory;
        protected readonly ILogger _logger;
        
        public BaseBlobRepository(
            ResilientBlobServiceClient blobServiceClient,
            ISemanticTextMemory memory,
            ILogger logger)
        {
            _blobServiceClient = blobServiceClient;
            _memory = memory;
            _logger = logger;
        }
        
        /// <summary>
        /// Garante que um container existe
        /// </summary>
        /// <param name="containerName">Nome do container</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        protected async Task EnsureContainerExistsAsync(string containerName, CancellationToken cancellationToken)
        {
            await _blobServiceClient.CreateBlobContainerAsync(containerName, cancellationToken);
        }
        
        /// <summary>
        /// Normaliza um nome de blob
        /// </summary>
        protected string NormalizeBlobName(string name)
        {
            // Remove caracteres inválidos para nomes de blob
            return name.Replace('\\', '/').Replace('?', '_').Replace('&', '_')
                       .Replace(':', '_').Replace('*', '_').Replace('"', '_')
                       .Replace('<', '_').Replace('>', '_').Replace('|', '_');
        }
    }
}
```

### Data/Repositories/ActivityRepository.cs

```csharp
using EemCore.Models;
using EemCore.Services.Resilient;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Memory;
using System.Text.Json;

namespace EemCore.Data.Repositories
{
    /// <summary>
    /// Implementação de repositório para eventos de atividade
    /// </summary>
    public class ActivityRepository : BaseBlobRepository, IActivityRepository
    {
        private const string ContainerName = "aje-files";
        private const string MemoryCollection = "activities";
        
        public ActivityRepository(
            ResilientBlobServiceClient blobServiceClient,
            ISemanticTextMemory memory,
            ILogger<ActivityRepository> logger)
            : base(blobServiceClient, memory, logger)
        {
        }
        
        /// <summary>
        /// Salva um novo evento de atividade no armazenamento
        /// </summary>
        public async Task<string> SaveActivityEventAsync(ActivityJournalEvent activityEvent, CancellationToken cancellationToken = default)
        {
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            
            // Gerar ID único para o evento se não definido
            if (string.IsNullOrEmpty(activityEvent.Id))
            {
                activityEvent.Id = Guid.NewGuid().ToString();
            }
            
            // Criar nome do blob baseado na sessão e timestamp
            string sessionSegment = string.IsNullOrEmpty(activityEvent.SessionId) 
                ? "no-session" 
                : NormalizeBlobName(activityEvent.SessionId);
                
            string blobName = $"{sessionSegment}/{DateTime.UtcNow:yyyyMMddHHmmss}_{activityEvent.Id}.aje";
            
            // Serializar e salvar o evento
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            
            string jsonContent = JsonSerializer.Serialize(activityEvent);
            await blobClient.UploadAsync(
                new BinaryData(jsonContent),
                overwrite: true,
                cancellationToken: cancellationToken
            );
            
            // Adicionar a eventos para busca semântica
            await _memory.SaveInformationAsync(
                collection: MemoryCollection,
                id: activityEvent.Id,
                text: activityEvent.Content,
                description: $"Atividade: {activityEvent.ActivityType}, Sessão: {activityEvent.SessionId}",
                additionalMetadata: blobName,
                cancellationToken: cancellationToken
            );
            
            _logger.LogInformation("Evento de atividade salvo: {Id}", activityEvent.Id);
            
            return activityEvent.Id;
        }
        
        /// <summary>
        /// Obtém eventos de atividade para uma sessão específica
        /// </summary>
        public async Task<IEnumerable<ActivityJournalEvent>> GetActivityEventsForSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            string normalizedSessionId = NormalizeBlobName(sessionId);
            string prefix = $"{normalizedSessionId}/";
            
            var results = new List<ActivityJournalEvent>();
            
            await foreach (var blob in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            {
                var blobClient = containerClient.GetBlobClient(blob.Name);
                var content = await blobClient.DownloadContentAsync(cancellationToken);
                var jsonContent = content.Value.Content.ToString();
                
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    try
                    {
                        var activityEvent = JsonSerializer.Deserialize<ActivityJournalEvent>(jsonContent);
                        if (activityEvent != null)
                        {
                            results.Add(activityEvent);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Erro ao deserializar evento de atividade: {BlobName}", blob.Name);
                    }
                }
            }
            
            return results.OrderByDescending(e => e.Timestamp);
        }
        
        /// <summary>
        /// Obtém eventos de atividade dentro de uma janela de tempo específica
        /// </summary>
        public async Task<IEnumerable<ActivityJournalEvent>> GetActivityEventsInTimeRangeAsync(
            DateTime startTime, DateTime endTime, int maxEvents = 1000, CancellationToken cancellationToken = default)
        {
            // Implementação de exemplo; em produção, use indexação para melhor desempenho
            
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            
            var results = new List<ActivityJournalEvent>();
            
            await foreach (var blob in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                // Verificar se atingimos o máximo de eventos
                if (results.Count >= maxEvents)
                    break;
                
                var blobClient = containerClient.GetBlobClient(blob.Name);
                var content = await blobClient.DownloadContentAsync(cancellationToken);
                var jsonContent = content.Value.Content.ToString();
                
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    try
                    {
                        var activityEvent = JsonSerializer.Deserialize<ActivityJournalEvent>(jsonContent);
                        if (activityEvent != null && 
                            activityEvent.Timestamp >= startTime && 
                            activityEvent.Timestamp <= endTime)
                        {
                            results.Add(activityEvent);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Erro ao deserializar evento de atividade: {BlobName}", blob.Name);
                    }
                }
            }
            
            return results.OrderByDescending(e => e.Timestamp);
        }
        
        /// <summary>
        /// Busca eventos de atividade baseado em uma consulta textual
        /// </summary>
        public async Task<IEnumerable<ActivityJournalEvent>> SearchActivityEventsAsync(
            string searchQuery, int maxResults = 10, CancellationToken cancellationToken = default)
        {
            // Usar busca semântica para encontrar eventos relacionados
            var searchResults = await _memory.SearchAsync(
                collection: MemoryCollection,
                query: searchQuery,
                limit: maxResults,
                minRelevanceScore: 0.5,
                cancellationToken: cancellationToken
            );
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var results = new List<ActivityJournalEvent>();
            
            foreach (var result in searchResults)
            {
                string blobPath = result.Metadata.AdditionalMetadata;
                
                if (!string.IsNullOrEmpty(blobPath))
                {
                    try
                    {
                        var blobClient = containerClient.GetBlobClient(blobPath);
                        var content = await blobClient.DownloadContentAsync(cancellationToken);
                        var jsonContent = content.Value.Content.ToString();
                        
                        if (!string.IsNullOrEmpty(jsonContent))
                        {
                            var activityEvent = JsonSerializer.Deserialize<ActivityJournalEvent>(jsonContent);
                            if (activityEvent != null)
                            {
                                results.Add(activityEvent);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao recuperar evento de atividade: {BlobPath}", blobPath);
                    }
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// Exclui eventos mais antigos que um período de retenção
        /// </summary>
        public async Task<int> PurgeOldEventsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
        {
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            
            var cutoffDate = DateTime.UtcNow - retentionPeriod;
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            int deletedCount = 0;
            
            await foreach (var blob in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                // Verificar a data de modificação do blob
                if (blob.Properties.LastModified.HasValue && 
                    blob.Properties.LastModified.Value.UtcDateTime < cutoffDate)
                {
                    // Tentar excluir o blob
                    var blobClient = containerClient.GetBlobClient(blob.Name);
                    await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                    deletedCount++;
                    
                    // Também remover da memória semântica
                    string eventId = Path.GetFileNameWithoutExtension(blob.Name);
                    await _memory.RemoveAsync(MemoryCollection, eventId, cancellationToken);
                }
            }
            
            _logger.LogInformation("Eventos antigos excluídos: {Count}", deletedCount);
            return deletedCount;
        }
    }
}
```

### EemServer/Program.cs

```csharp
using EemCore.Agents;
using EemCore.Configuration;
using EemCore.Data;
using EemCore.Processing;
using EemCore.Services;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Identity.Web;
using Microsoft.MCP;
using Microsoft.OpenApi.Models;
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
    .AddAzureBlobStorage(name: "blob-storage")
    .AddCosmosDb(name: "cosmos-db")
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
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
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
```

## 4. Criação de Classes de Modelo Adicionais

### Models/ActivityJournalEvent.cs

```csharp
using System.Text.Json.Serialization;

namespace EemCore.Models
{
    /// <summary>
    /// Representa um evento de atividade do usuário (.aje)
    /// </summary>
    public class ActivityJournalEvent
    {
        /// <summary>
        /// Identificador único do evento
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Timestamp de quando o evento ocorreu
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tipo de atividade (edição, navegação, consulta, etc.)
        /// </summary>
        public string ActivityType { get; set; } = string.Empty;

        /// <summary>
        /// Conteúdo principal do evento
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Fonte da atividade (IDE, Assistente AI, etc.)
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// ID da sessão à qual o evento pertence
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Metadados adicionais do evento
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
```

### Models/InterpretedRelationEvent.cs

```csharp
namespace EemCore.Models
{
    /// <summary>
    /// Representa uma relação interpretada entre eventos (.ire)
    /// </summary>
    public class InterpretedRelationEvent
    {
        /// <summary>
        /// Identificador único da relação
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Timestamp de quando a relação foi criada
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tipo de relação (causal, temporal, semântica, etc.)
        /// </summary>
        public string RelationType { get; set; } = string.Empty;

        /// <summary>
        /// Lista de IDs de eventos relacionados
        /// </summary>
        public List<string> RelatedEventIds { get; set; } = new();

        /// <summary>
        /// Peso ou força da relação (0-1)
        /// </summary>
        public double RelationStrength { get; set; }

        /// <summary>
        /// Descrição da relação
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Tags ou categorias para a relação
        /// </summary>
        public List<string> Tags { get; set; } = new();
    }
}
```

### Models/EulerianFlow.cs

```csharp
namespace EemCore.Models
{
    /// <summary>
    /// Representa um fluxo euleriano de atividades (.e)
    /// </summary>
    public class EulerianFlow
    {
        /// <summary>
        /// Identificador único do fluxo
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Timestamp de quando o fluxo foi criado
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Nome descritivo do fluxo
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Sequência ordenada de eventos no fluxo
        /// </summary>
        public List<FlowNode> Nodes { get; set; } = new();

        /// <summary>
        /// Conexões entre os nós (arestas do grafo)
        /// </summary>
        public List<FlowEdge> Edges { get; set; } = new();

        /// <summary>
        /// Categorias associadas ao fluxo
        /// </summary>
        public List<string> Categories { get; set; } = new();

        /// <summary>
        /// Resumo do fluxo
        /// </summary>
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Representa um nó em um fluxo euleriano
    /// </summary>
    public class FlowNode
    {
        /// <summary>
        /// Identificador único do nó
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Tipo de nó
        /// </summary>
        public string NodeType { get; set; } = string.Empty;

        /// <summary>
        /// ID do evento associado
        /// </summary>
        public string EventId { get; set; } = string.Empty;

        /// <summary>
        /// Rótulo do nó
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Metadados adicionais para o nó
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Representa uma aresta em um fluxo euleriano
    /// </summary>
    public class FlowEdge
    {
        /// <summary>
        /// Identificador único da aresta
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// ID do nó de origem
        /// </summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// ID do nó de destino
        /// </summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de relacionamento
        /// </summary>
        public string RelationType { get; set; } = string.Empty;

        /// <summary>
        /// Peso ou força da conexão
        /// </summary>
        public double Weight { get; set; } = 1.0;
    }
}
```

### Models/ScriptInfo.cs

```csharp
namespace EemCore.Models
{
    /// <summary>
    /// Informações sobre um script GenAIScript
    /// </summary>
    public class ScriptInfo
    {
        /// <summary>
        /// Identificador único do script
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Nome do script
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Descrição do script
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp de criação do script
        /// </summary>
        public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp da última modificação do script
        /// </summary>
        public DateTime ModifiedDateTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Indica se o script está ativo
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Tags para o script
        /// </summary>
        public List<string> Tags { get; set; } = new();
    }
}
```

## 5. Implementação do Inicializador de Dados

### Data/EemDataInitializer.cs

```csharp
using EemCore.Configuration;
using EemCore.Services.Resilient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EemCore.Data
{
    /// <summary>
    /// Inicializador de dados para o sistema Εεm
    /// </summary>
    public class EemDataInitializer
    {
        private readonly ResilientBlobServiceClient _blobServiceClient;
        private readonly ResilientCosmosClient _cosmosClient;
        private readonly IOptions<AzureOptions> _azureOptions;
        private readonly ILogger<EemDataInitializer> _logger;
        
        public EemDataInitializer(
            ResilientBlobServiceClient blobServiceClient,
            ResilientCosmosClient cosmosClient,
            IOptions<AzureOptions> azureOptions,
            ILogger<EemDataInitializer> logger)
        {
            _blobServiceClient = blobServiceClient;
            _cosmosClient = cosmosClient;
            _azureOptions = azureOptions;
            _logger = logger;
        }
        
        /// <summary>
        /// Inicializa todos os recursos necessários para o sistema Εεm
        /// </summary>
        public async Task InitializeAsync()
        {
            _logger.LogInformation("Iniciando inicialização de recursos do Εεm...");
            
            try
            {
                // Inicializar containers do Blob Storage
                await InitializeBlobStorageAsync();
                
                // Inicializar bancos de dados e containers Cosmos DB
                await InitializeCosmosDbAsync();
                
                _logger.LogInformation("Inicialização de recursos do Εεm concluída com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante inicialização de recursos do Εεm");
                throw;
            }
        }
        
        /// <summary>
        /// Inicializa os containers do Blob Storage
        /// </summary>
        private async Task InitializeBlobStorageAsync()
        {
            string[] containers = new[]
            {
                "aje-files", // Arquivos de Journal de Atividades
                "ire-files", // Arquivos de Relações Interpretadas
                "e-files",   // Arquivos de Fluxo Euleriano
                "scripts"    // Scripts GenAIScript
            };
            
            foreach (var containerName in containers)
            {
                _logger.LogInformation("Verificando container de Blob Storage: {Container}", containerName);
                await _blobServiceClient.CreateBlobContainerAsync(containerName);
            }
        }
        
        /// <summary>
        /// Inicializa bancos de dados e containers Cosmos DB
        /// </summary>
        private async Task InitializeCosmosDbAsync()
        {
            string databaseId = _azureOptions.Value.CosmosDatabase;
            
            _logger.LogInformation("Verificando banco de dados Cosmos DB: {Database}", databaseId);
            
            // Criar banco de dados se não existir
            await _cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId, 400);
            
            // Definir os containers necessários
            var containers = new[]
            {
                ("EmbeddingsCollection", "/id"),         // Container para embeddings
                ("relationalgraph", "/type"),            // Container para o grafo relacional
                ("EulerianFlows", "/id"),                // Container para fluxos eulerianos
                ("ActivityJournalEvents", "/SessionId"), // Container para eventos de atividade
                ("InterpretedRelationEvents", "/id")     // Container para eventos de relação
            };
            
            // Criar cada container se não existir
            foreach (var (containerId, partitionKeyPath) in containers)
            {
                _logger.LogInformation("Verificando container Cosmos DB: {Container}", containerId);
                await _cosmosClient.CreateContainerIfNotExistsAsync(
                    databaseId,
                    containerId,
                    partitionKeyPath,
                    400
                );
            }
        }
    }
}
```

## 6. Configuração do Docker

Crie um arquivo Dockerfile na raiz do projeto:

```dockerfile
# Estágio de build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["EemServer/EemServer.csproj", "EemServer/"]
COPY ["EemCore/EemCore.csproj", "EemCore/"]
RUN dotnet restore "EemServer/EemServer.csproj"
COPY . .
WORKDIR "/src/EemServer"
RUN dotnet build "EemServer.csproj" -c Release -o /app/build

# Estágio de publicação
FROM build AS publish
RUN dotnet publish "EemServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio final
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EemServer.dll"]
```

## 7. Configuração do Azure

Script para provisionar recursos no Azure:

```bash
#!/bin/bash

# Configuração
RESOURCE_GROUP="eem-resources"
LOCATION="eastus"
STORAGE_ACCOUNT="eemstorage"
COSMOS_ACCOUNT="eem-cosmos"
OPENAI_ACCOUNT="eem-openai"
APP_SERVICE_PLAN="eem-appplan"
APP_SERVICE="eem-mcp-server"

# Criar grupo de recursos
az group create --name $RESOURCE_GROUP --location $LOCATION --tags Project=Eem Program=FoundersHub

# Criar conta de Storage
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2 \
  --tags Project=Eem Program=FoundersHub

# Criar conta Cosmos DB
az cosmosdb create \
  --name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --locations regionName=$LOCATION \
  --capabilities EnableGremlin \
  --tags Project=Eem Program=FoundersHub

# Criar conta OpenAI
az cognitiveservices account create \
  --name $OPENAI_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --kind OpenAI \
  --sku S0 \
  --tags Project=Eem Program=FoundersHub

# Criar plano App Service
az appservice plan create \
  --name $APP_SERVICE_PLAN \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku B1 \
  --is-linux \
  --tags Project=Eem Program=FoundersHub

# Criar App Service
az webapp create \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --plan $APP_SERVICE_PLAN \
  --runtime "DOTNETCORE|8.0" \
  --tags Project=Eem Program=FoundersHub

# Obter e exibir as chaves
echo "Obtendo chaves para configuração..."
STORAGE_KEY=$(az storage account keys list --resource-group $RESOURCE_GROUP --account-name $STORAGE_ACCOUNT --query [0].value -o tsv)
COSMOS_KEY=$(az cosmosdb keys list --resource-group $RESOURCE_GROUP --name $COSMOS_ACCOUNT --query primaryMasterKey -o tsv)
OPENAI_KEY=$(az cognitiveservices account keys list --resource-group $RESOURCE_GROUP --name $OPENAI_ACCOUNT --query key1 -o tsv)

echo "Azure Storage Connection String:"
echo "DefaultEndpointsProtocol=https;AccountName=$STORAGE_ACCOUNT;AccountKey=$STORAGE_KEY;EndpointSuffix=core.windows.net"

echo "Cosmos DB Endpoint:"
echo "https://$COSMOS_ACCOUNT.documents.azure.com:443/"

echo "Cosmos DB Key:"
echo "$COSMOS_KEY"

echo "OpenAI Endpoint:"
echo "https://$OPENAI_ACCOUNT.openai.azure.com/"

echo "OpenAI Key:"
echo "$OPENAI_KEY"
```

## 8. Próximos Passos

1. **Implementar repositórios restantes**:
   - RelationRepository.cs
   - EulerianFlowRepository.cs
   - ScriptRepository.cs

2. **Completar a implementação dos agentes**:
   - Refatorar CaptureAgent.cs para usar os novos serviços resilientes
   - Atualizar EulerianAgent.cs
   - Atualizar CorrelationAgent.cs

3. **Adicionar testes unitários**:
   - Criar projeto EemCore.Tests
   - Implementar testes para repositórios e agentes

4. **Configurar CI/CD para Azure**:
   - Criar fluxo GitHub Actions ou Azure DevOps

5. **Documentação final**:
   - Atualizar os arquivos F# para documentação executável
   - Completar a documentação XML para geração automática

Estas instruções fornecerão um guia completo para implementar o servidor MCP do sistema Εεm de forma profissional e pronta para produção, otimizada para uso com os créditos do Microsoft for Startups Founders Hub.
