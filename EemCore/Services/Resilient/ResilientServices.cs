using Azure.AI.OpenAI;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
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
        /// Obt�m um cliente de container resiliente
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
        /// Obt�m um container resiliente
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
        /// Obt�m completions de chat de forma resiliente
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
        /// Obt�m embeddings de forma resiliente
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