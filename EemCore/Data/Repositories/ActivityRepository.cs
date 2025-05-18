using EemCore.Models;
using EemCore.Services.Resilient;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Memory;
using System.Text.Json;

namespace EemCore.Data.Repositories
{
    /// <summary>
    /// Implementa��o de reposit�rio para eventos de atividade
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
            
            // Gerar ID �nico para o evento se n�o definido
            if (string.IsNullOrEmpty(activityEvent.Id))
            {
                activityEvent.Id = Guid.NewGuid().ToString();
            }
            
            // Criar nome do blob baseado na sess�o e timestamp
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
            
            // Adicionar a eventos para busca sem�ntica
            await _memory.SaveInformationAsync(
                collection: MemoryCollection,
                id: activityEvent.Id,
                text: activityEvent.Content,
                description: $"Atividade: {activityEvent.ActivityType}, Sess�o: {activityEvent.SessionId}",
                additionalMetadata: blobName,
                cancellationToken: cancellationToken
            );
            
            _logger.LogInformation("Evento de atividade salvo: {Id}", activityEvent.Id);
            
            return activityEvent.Id;
        }
        
        /// <summary>
        /// Obt�m eventos de atividade para uma sess�o espec�fica
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
        /// Obt�m eventos de atividade dentro de uma janela de tempo espec�fica
        /// </summary>
        public async Task<IEnumerable<ActivityJournalEvent>> GetActivityEventsInTimeRangeAsync(
            DateTime startTime, DateTime endTime, int maxEvents = 1000, CancellationToken cancellationToken = default)
        {
            // Implementa��o de exemplo; em produ��o, use indexa��o para melhor desempenho
            
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            
            var results = new List<ActivityJournalEvent>();
            
            await foreach (var blob in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                // Verificar se atingimos o m�ximo de eventos
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
            // Usar busca sem�ntica para encontrar eventos relacionados
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
        /// Exclui eventos mais antigos que um per�odo de reten��o
        /// </summary>
        public async Task<int> PurgeOldEventsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
        {
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            
            var cutoffDate = DateTime.UtcNow - retentionPeriod;
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            int deletedCount = 0;
            
            await foreach (var blob in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                // Verificar a data de modifica��o do blob
                if (blob.Properties.LastModified.HasValue && 
                    blob.Properties.LastModified.Value.UtcDateTime < cutoffDate)
                {
                    // Tentar excluir o blob
                    var blobClient = containerClient.GetBlobClient(blob.Name);
                    await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                    deletedCount++;
                    
                    // Tamb�m remover da mem�ria sem�ntica
                    string eventId = Path.GetFileNameWithoutExtension(blob.Name);
                    await _memory.RemoveAsync(MemoryCollection, eventId, cancellationToken);
                }
            }
            
            _logger.LogInformation("Eventos antigos exclu�dos: {Count}", deletedCount);
            return deletedCount;
        }
    }
}