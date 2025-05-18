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
        /// Inicializa todos os recursos necess�rios para o sistema ??m
        /// </summary>
        public async Task InitializeAsync()
        {
            _logger.LogInformation("Iniciando inicializa��o de recursos do ??m...");
            
            try
            {
                // Inicializar containers do Blob Storage
                await InitializeBlobStorageAsync();
                
                // Inicializar bancos de dados e containers Cosmos DB
                await InitializeCosmosDbAsync();
                
                _logger.LogInformation("Inicializa��o de recursos do ??m conclu�da com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante inicializa��o de recursos do ??m");
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
                "ire-files", // Arquivos de Rela��es Interpretadas
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
            
            // Criar banco de dados se n�o existir
            await _cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId, 400);
            
            // Definir os containers necess�rios
            var containers = new[]
            {
                ("EmbeddingsCollection", "/id"),         // Container para embeddings
                ("relationalgraph", "/type"),            // Container para o grafo relacional
                ("EulerianFlows", "/id"),                // Container para fluxos eulerianos
                ("ActivityJournalEvents", "/SessionId"), // Container para eventos de atividade
                ("InterpretedRelationEvents", "/id")     // Container para eventos de rela��o
            };
            
            // Criar cada container se n�o existir
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