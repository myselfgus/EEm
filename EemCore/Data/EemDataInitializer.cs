using EemCore.Configuration;
using EemCore.Services.Resilient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EemCore.Data
{
    /// <summary>
    /// Inicializador de dados para o sistema ??m
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
        /// Inicializa todos os recursos necessários para o sistema ??m
        /// </summary>
        public async Task InitializeAsync()
        {
            _logger.LogInformation("Iniciando inicialização de recursos do ??m...");
            
            try
            {
                // Inicializar containers do Blob Storage
                await InitializeBlobStorageAsync();
                
                // Inicializar bancos de dados e containers Cosmos DB
                await InitializeCosmosDbAsync();
                
                _logger.LogInformation("Inicialização de recursos do ??m concluída com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante inicialização de recursos do ??m");
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