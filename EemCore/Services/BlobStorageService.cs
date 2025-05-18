using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using EemCore.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EemCore.Services
{
    /// <summary>
    /// Serviço para interação com Azure Blob Storage
    /// Gerencia armazenamento e recuperação de arquivos .aje, .ire e .e
    /// </summary>
    public class BlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BlobStorageService> _logger;

        // Nomes dos containers para os diferentes tipos de arquivos
        public const string AjeContainer = "aje-files";
        public const string IreContainer = "ire-files";
        public const string EContainer = "e-files";
        public const string ScriptsContainer = "genai-scripts";

        /// <summary>
        /// Construtor do serviço
        /// </summary>
        public BlobStorageService(BlobServiceClient blobServiceClient, ILogger<BlobStorageService> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
        }

        /// <summary>
        /// Inicializa os containers necessários para o sistema
        /// </summary>
        public async Task InitializeContainersAsync()
        {
            _logger.LogInformation("Inicializando containers do Azure Blob Storage");

            await CreateContainerIfNotExistsAsync(AjeContainer);
            await CreateContainerIfNotExistsAsync(IreContainer);
            await CreateContainerIfNotExistsAsync(EContainer);
            await CreateContainerIfNotExistsAsync(ScriptsContainer);

            _logger.LogInformation("Containers inicializados com sucesso");
        }

        /// <summary>
        /// Cria um container se ele não existir
        /// </summary>
        private async Task CreateContainerIfNotExistsAsync(string containerName)
        {
            try
            {
                BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync();
                _logger.LogInformation($"Container {containerName} verificado/criado com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao criar container {containerName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Salva um ActivityJournalEvent no storage
        /// </summary>
        public async Task<string> SaveActivityJournalEventAsync(ActivityJournalEvent activityEvent)
        {
            if (activityEvent == null)
                throw new ArgumentNullException(nameof(activityEvent));

            try
            {
                // Calcular hash de conteúdo se não estiver definido
                if (string.IsNullOrEmpty(activityEvent.ContentHash))
                {
                    activityEvent.GenerateContentHash();
                }

                // Obter container
                var containerClient = _blobServiceClient.GetBlobContainerClient(AjeContainer);
                
                // Verificar se o container existe
                await containerClient.CreateIfNotExistsAsync();

                // Preparar o blobClient usando o nome do arquivo definido no modelo
                var blobClient = containerClient.GetBlobClient(activityEvent.FileName);

                // Converter para JSON e fazer upload
                string json = JsonSerializer.Serialize(activityEvent);
                
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                await blobClient.UploadAsync(stream, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = "application/json"
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        { "activityType", activityEvent.ActivityType },
                        { "source", activityEvent.Source },
                        { "sessionId", activityEvent.SessionId },
                        { "timestamp", activityEvent.Timestamp.ToString("o") }
                    }
                });

                _logger.LogInformation($"ActivityJournalEvent salvo com sucesso: {activityEvent.Id}");
                return activityEvent.FileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao salvar ActivityJournalEvent: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Salva um InterpretedRelationEvent no storage
        /// </summary>
        public async Task<string> SaveInterpretedRelationEventAsync(InterpretedRelationEvent relationEvent)
        {
            if (relationEvent == null)
                throw new ArgumentNullException(nameof(relationEvent));

            try
            {
                // Obter container
                var containerClient = _blobServiceClient.GetBlobContainerClient(IreContainer);
                
                // Verificar se o container existe
                await containerClient.CreateIfNotExistsAsync();

                // Preparar o blobClient usando o nome do arquivo definido no modelo
                var blobClient = containerClient.GetBlobClient(relationEvent.FileName);

                // Converter para JSON e fazer upload
                string json = JsonSerializer.Serialize(relationEvent);
                
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                await blobClient.UploadAsync(stream, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = "application/json"
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        { "relationType", relationEvent.RelationType },
                        { "sessionId", relationEvent.SessionId },
                        { "timestamp", relationEvent.Timestamp.ToString("o") },
                        { "confidence", relationEvent.Confidence.ToString() }
                    }
                });

                _logger.LogInformation($"InterpretedRelationEvent salvo com sucesso: {relationEvent.Id}");
                return relationEvent.FileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao salvar InterpretedRelationEvent: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Salva um EulerianFlow no storage
        /// </summary>
        public async Task<string> SaveEulerianFlowAsync(EulerianFlow eulerianFlow)
        {
            if (eulerianFlow == null)
                throw new ArgumentNullException(nameof(eulerianFlow));

            try
            {
                // Obter container
                var containerClient = _blobServiceClient.GetBlobContainerClient(EContainer);
                
                // Verificar se o container existe
                await containerClient.CreateIfNotExistsAsync();

                // Preparar o blobClient usando o nome do arquivo definido no modelo
                var blobClient = containerClient.GetBlobClient(eulerianFlow.FileName);

                // Converter para JSON e fazer upload
                string json = JsonSerializer.Serialize(eulerianFlow);
                
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                await blobClient.UploadAsync(stream, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = "application/json"
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        { "flowType", eulerianFlow.FlowType },
                        { "sessionId", eulerianFlow.SessionId },
                        { "startTime", eulerianFlow.TimeRange.Start.ToString("o") },
                        { "endTime", eulerianFlow.TimeRange.End.ToString("o") },
                        { "isCompleteEulerian", eulerianFlow.IsCompleteEulerian.ToString() }
                    }
                });

                _logger.LogInformation($"EulerianFlow salvo com sucesso: {eulerianFlow.FlowId}");
                return eulerianFlow.FileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao salvar EulerianFlow: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtém um ActivityJournalEvent por ID ou por nome de arquivo
        /// </summary>
        public async Task<ActivityJournalEvent?> GetActivityJournalEventAsync(string idOrFileName)
        {
            if (string.IsNullOrEmpty(idOrFileName))
                throw new ArgumentException("ID ou nome do arquivo não pode ser vazio", nameof(idOrFileName));

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(AjeContainer);

                // Se for um ID puro, precisamos buscar nos metadados
                if (!idOrFileName.Contains('/') && !idOrFileName.EndsWith(".aje"))
                {
                    // Buscar por ID em todos os blobs (isso é ineficiente, mas usado apenas em casos específicos)
                    await foreach (var blobItem in containerClient.GetBlobsAsync())
                    {
                        var blobClient = containerClient.GetBlobClient(blobItem.Name);
                        
                        var properties = await blobClient.GetPropertiesAsync();
                        var content = await blobClient.DownloadContentAsync();
                        var data = JsonSerializer.Deserialize<ActivityJournalEvent>(content.Value.Content);
                        
                        if (data?.Id == idOrFileName)
                            return data;
                    }
                    
                    // Não encontrado
                    _logger.LogWarning($"ActivityJournalEvent com ID {idOrFileName} não encontrado");
                    return null;
                }
                else
                {
                    // É um caminho de arquivo, acessar diretamente
                    string blobPath = idOrFileName;
                    
                    // Adicionar extensão se necessário
                    if (!blobPath.EndsWith(".aje"))
                        blobPath = $"{blobPath}.aje";
                    
                    var blobClient = containerClient.GetBlobClient(blobPath);
                    
                    if (!await blobClient.ExistsAsync())
                    {
                        _logger.LogWarning($"ActivityJournalEvent com caminho {blobPath} não encontrado");
                        return null;
                    }
                    
                    var content = await blobClient.DownloadContentAsync();
                    return JsonSerializer.Deserialize<ActivityJournalEvent>(content.Value.Content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao obter ActivityJournalEvent: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtém um InterpretedRelationEvent por ID ou por nome de arquivo
        /// </summary>
        public async Task<InterpretedRelationEvent?> GetInterpretedRelationEventAsync(string idOrFileName)
        {
            if (string.IsNullOrEmpty(idOrFileName))
                throw new ArgumentException("ID ou nome do arquivo não pode ser vazio", nameof(idOrFileName));

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(IreContainer);

                // Se for um ID puro, precisamos buscar nos metadados
                if (!idOrFileName.Contains('/') && !idOrFileName.EndsWith(".ire"))
                {
                    // Buscar por ID em todos os blobs (isso é ineficiente, mas usado apenas em casos específicos)
                    await foreach (var blobItem in containerClient.GetBlobsAsync())
                    {
                        var blobClient = containerClient.GetBlobClient(blobItem.Name);
                        
                        var content = await blobClient.DownloadContentAsync();
                        var data = JsonSerializer.Deserialize<InterpretedRelationEvent>(content.Value.Content);
                        
                        if (data?.Id == idOrFileName)
                            return data;
                    }
                    
                    // Não encontrado
                    _logger.LogWarning($"InterpretedRelationEvent com ID {idOrFileName} não encontrado");
                    return null;
                }
                else
                {
                    // É um caminho de arquivo, acessar diretamente
                    string blobPath = idOrFileName;
                    
                    // Adicionar extensão se necessário
                    if (!blobPath.EndsWith(".ire"))
                        blobPath = $"{blobPath}.ire";
                    
                    var blobClient = containerClient.GetBlobClient(blobPath);
                    
                    if (!await blobClient.ExistsAsync())
                    {
                        _logger.LogWarning($"InterpretedRelationEvent com caminho {blobPath} não encontrado");
                        return null;
                    }
                    
                    var content = await blobClient.DownloadContentAsync();
                    return JsonSerializer.Deserialize<InterpretedRelationEvent>(content.Value.Content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao obter InterpretedRelationEvent: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtém um EulerianFlow por ID ou por nome de arquivo
        /// </summary>
        public async Task<EulerianFlow?> GetEulerianFlowAsync(string idOrFileName)
        {
            if (string.IsNullOrEmpty(idOrFileName))
                throw new ArgumentException("ID ou nome do arquivo não pode ser vazio", nameof(idOrFileName));

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(EContainer);

                // Se for um ID puro, precisamos buscar nos metadados
                if (!idOrFileName.Contains('/') && !idOrFileName.EndsWith(".e"))
                {
                    // Buscar por ID em todos os blobs (isso é ineficiente, mas usado apenas em casos específicos)
                    await foreach (var blobItem in containerClient.GetBlobsAsync())
                    {
                        var blobClient = containerClient.GetBlobClient(blobItem.Name);
                        
                        var content = await blobClient.DownloadContentAsync();
                        var data = JsonSerializer.Deserialize<EulerianFlow>(content.Value.Content);
                        
                        if (data?.FlowId == idOrFileName)
                            return data;
                    }
                    
                    // Não encontrado
                    _logger.LogWarning($"EulerianFlow com ID {idOrFileName} não encontrado");
                    return null;
                }
                else
                {
                    // É um caminho de arquivo, acessar diretamente
                    string blobPath = idOrFileName;
                    
                    // Adicionar extensão se necessário
                    if (!blobPath.EndsWith(".e"))
                        blobPath = $"{blobPath}.e";
                    
                    var blobClient = containerClient.GetBlobClient(blobPath);
                    
                    if (!await blobClient.ExistsAsync())
                    {
                        _logger.LogWarning($"EulerianFlow com caminho {blobPath} não encontrado");
                        return null;
                    }
                    
                    var content = await blobClient.DownloadContentAsync();
                    return JsonSerializer.Deserialize<EulerianFlow>(content.Value.Content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao obter EulerianFlow: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtém todos os ActivityJournalEvents de uma sessão específica
        /// </summary>
        public async Task<List<ActivityJournalEvent>> GetSessionActivityEventsAsync(string sessionId, DateTime? startTime = null, DateTime? endTime = null)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("ID da sessão não pode ser vazio", nameof(sessionId));

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(AjeContainer);
                var results = new List<ActivityJournalEvent>();

                // Buscar todos os blobs com o prefixo da sessão
                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: $"{sessionId}/"))
                {
                    // Aplicar filtro de tempo se necessário
                    if (startTime.HasValue && blobItem.Properties.CreatedOn < startTime.Value)
                        continue;
                    
                    if (endTime.HasValue && blobItem.Properties.CreatedOn > endTime.Value)
                        continue;

                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    var content = await blobClient.DownloadContentAsync();
                    var data = JsonSerializer.Deserialize<ActivityJournalEvent>(content.Value.Content);
                    
                    if (data != null)
                    {
                        results.Add(data);
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao obter ActivityJournalEvents da sessão {sessionId}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtém todos os InterpretedRelationEvents de uma sessão específica
        /// </summary>
        public async Task<List<InterpretedRelationEvent>> GetSessionRelationEventsAsync(string sessionId, DateTime? startTime = null, DateTime? endTime = null)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("ID da sessão não pode ser vazio", nameof(sessionId));

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(IreContainer);
                var results = new List<InterpretedRelationEvent>();

                // Buscar todos os blobs com o prefixo da sessão
                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: $"{sessionId}/"))
                {
                    // Aplicar filtro de tempo se necessário
                    if (startTime.HasValue && blobItem.Properties.CreatedOn < startTime.Value)
                        continue;
                    
                    if (endTime.HasValue && blobItem.Properties.CreatedOn > endTime.Value)
                        continue;

                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    var content = await blobClient.DownloadContentAsync();
                    var data = JsonSerializer.Deserialize<InterpretedRelationEvent>(content.Value.Content);
                    
                    if (data != null)
                    {
                        results.Add(data);
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao obter InterpretedRelationEvents da sessão {sessionId}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtém todos os EulerianFlows de uma sessão específica
        /// </summary>
        public async Task<List<EulerianFlow>> GetSessionEulerianFlowsAsync(string sessionId, DateTime? startTime = null, DateTime? endTime = null)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("ID da sessão não pode ser vazio", nameof(sessionId));

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(EContainer);
                var results = new List<EulerianFlow>();

                // Buscar todos os blobs com o prefixo da sessão
                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: $"{sessionId}/"))
                {
                    // Aplicar filtro de tempo se necessário
                    if (startTime.HasValue && blobItem.Properties.CreatedOn < startTime.Value)
                        continue;
                    
                    if (endTime.HasValue && blobItem.Properties.CreatedOn > endTime.Value)
                        continue;

                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    var content = await blobClient.DownloadContentAsync();
                    var data = JsonSerializer.Deserialize<EulerianFlow>(content.Value.Content);
                    
                    if (data != null)
                    {
                        results.Add(data);
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao obter EulerianFlows da sessão {sessionId}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Deleta um blob específico
        /// </summary>
        public async Task DeleteBlobAsync(string containerName, string blobName)
        {
            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentException("Nome do container não pode ser vazio", nameof(containerName));
            
            if (string.IsNullOrEmpty(blobName))
                throw new ArgumentException("Nome do blob não pode ser vazio", nameof(blobName));

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);
                
                await blobClient.DeleteIfExistsAsync();
                _logger.LogInformation($"Blob {blobName} deletado com sucesso do container {containerName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao deletar blob {blobName} do container {containerName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtém uma SAS (assinatura de acesso compartilhado) para um blob
        /// </summary>
        public string GetBlobSasUri(string containerName, string blobName, TimeSpan validity)
        {
            if (string.IsNullOrEmpty(containerName))
                throw new ArgumentException("Nome do container não pode ser vazio", nameof(containerName));
            
            if (string.IsNullOrEmpty(blobName))
                throw new ArgumentException("Nome do blob não pode ser vazio", nameof(blobName));

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);
                
                // Verificar se o blob existe
                if (!blobClient.Exists())
                {
                    _logger.LogWarning($"Blob {blobName} não encontrado no container {containerName}");
                    return string.Empty;
                }
                
                // Gerar URI com SAS apenas para leitura
                if (blobClient is BlobBaseClient blobBaseClient)
                {
                    var sasBuilder = new Azure.Storage.Sas.BlobSasBuilder
                    {
                        BlobContainerName = containerName,
                        BlobName = blobName,
                        Resource = "b", // Blob
                        ExpiresOn = DateTimeOffset.UtcNow.Add(validity)
                    };
                    
                    sasBuilder.SetPermissions(Azure.Storage.Sas.BlobSasPermissions.Read);
                    
                    // Obter SAS Token e URI
                    var sasUri = blobBaseClient.GenerateSasUri(sasBuilder);
                    return sasUri.AbsoluteUri;
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao gerar SAS URI para blob {blobName}: {ex.Message}");
                throw;
            }
        }
    }
}