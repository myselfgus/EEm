using Azure.AI.OpenAI;
using Azure.Storage.Blobs;
using EemCore.Agents;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Polly;

namespace EemCore.Services
{
    /// <summary>
    /// Health check para verificar o status dos agentes do Εεm
    /// </summary>
    public class EemAgentsHealthCheck : IHealthCheck
    {
        private readonly CaptureAgent _captureAgent;
        private readonly ContextAgent _contextAgent;
        private readonly ILogger<EemAgentsHealthCheck> _logger;
        
        public EemAgentsHealthCheck(
            CaptureAgent captureAgent,
            ContextAgent contextAgent,
            ILogger<EemAgentsHealthCheck> logger)
        {
            _captureAgent = captureAgent;
            _contextAgent = contextAgent;
            _logger = logger;
        }
        
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>();
            try
            {
                // Verificar CaptureAgent
                var captureStatus = await _captureAgent.CheckHealthAsync(cancellationToken);
                data["CaptureAgent"] = captureStatus.Status.ToString();
                
                // Verificar ContextAgent
                var contextStatus = await _contextAgent.CheckHealthAsync(cancellationToken);
                data["ContextAgent"] = contextStatus.Status.ToString();
                
                // Determinar o status geral
                if (captureStatus.Status == HealthStatus.Healthy && 
                    contextStatus.Status == HealthStatus.Healthy)
                {
                    return HealthCheckResult.Healthy("Todos os agentes estão saudáveis", data);
                }
                else if (captureStatus.Status == HealthStatus.Unhealthy || 
                         contextStatus.Status == HealthStatus.Unhealthy)
                {
                    return HealthCheckResult.Unhealthy("Um ou mais agentes estão não saudáveis", null, data);
                }
                else
                {
                    return HealthCheckResult.Degraded("Um ou mais agentes estão degradados", null, data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar saúde dos agentes");
                return HealthCheckResult.Unhealthy("Erro ao verificar saúde dos agentes", ex, data);
            }
        }
    }
    
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
