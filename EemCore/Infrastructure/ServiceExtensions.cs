using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Storage.Blobs;
using EemCore.Agents;
using EemCore.Configuration;
using EemCore.Data.Repositories;
using EemCore.Processing;
using EemCore.Services;
using EemCore.Services.Resilient;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureAI;
using Microsoft.SemanticKernel.Memory;
using Polly;
using Polly.Registry;
using System.Net;

namespace EemCore.Infrastructure
{
    /// <summary>
    /// Extens�es para configura��o de servi�os do ??m
    /// </summary>
    public static class ServiceExtensions
    {
        /// <summary>
        /// Adiciona servi�os de acesso a dados do ??m
        /// </summary>
        public static IServiceCollection AddEemDataServices(this IServiceCollection services)
        {
            // Reposit�rios
            services.AddScoped<IActivityRepository, ActivityRepository>();
            services.AddScoped<IRelationRepository, RelationRepository>();
            services.AddScoped<IEulerianFlowRepository, EulerianFlowRepository>();
            services.AddScoped<IScriptRepository, ScriptRepository>();
            
            // Inicializador de dados
            services.AddScoped<EemDataInitializer>();
            
            return services;
        }
        
        /// <summary>
        /// Adiciona servi�os core do ??m
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
        /// Adiciona servi�os Azure com resili�ncia e telemetria
        /// </summary>
        public static IServiceCollection AddEemAzureServices(this IServiceCollection services)
        {
            // Configurar pol�tica de resili�ncia
            services.AddSingleton<IResiliencePipelineRegistry>(serviceProvider =>
            {
                var registry = new ResiliencePipelineRegistry();
                
                // Pol�tica de retry para Azure Storage
                registry.TryAddBuilder("BlobStorage", (builder, _) => builder
                    .AddRetry(new()
                    {
                        MaxRetryAttempts = 5,
                        BackoffType = DelayBackoffType.Exponential,
                        BaseDelay = TimeSpan.FromSeconds(1),
                        MaxDelay = TimeSpan.FromSeconds(10)
                    })
                    .AddTimeout(TimeSpan.FromSeconds(30)));
                
                // Pol�tica de retry para Cosmos DB
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
                
                // Pol�tica de retry para Azure OpenAI
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
                    deploymentName: options.OpenAIDeployment,
                    endpoint: options.OpenAIEndpoint,
                    apiKey: options.OpenAIKey);
                
                return kernelBuilder.Build();
            });
            
            // Configurar Mem�ria Sem�ntica
            services.AddSingleton<ISemanticTextMemory>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AzureOptions>>().Value;
                
                return new MemoryBuilder()
                    .WithAzureOpenAITextEmbeddingGeneration(
                        deploymentName: options.OpenAIEmbeddingModel,
                        endpoint: options.OpenAIEndpoint,
                        apiKey: options.OpenAIKey)
                    .WithMemoryStore(new VolatileMemoryStore())
                    .Build();
            });
            
            return services;
        }
    }
}