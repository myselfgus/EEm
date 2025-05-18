using EemCore.Models;
using EemCore.Services.Resilient;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Memory;
using System.Text.Json;

namespace EemCore.Data.Repositories
{
    /// <summary>
    /// Implementa��o de reposit�rio para rela��es interpretadas
    /// </summary>
    public class RelationRepository : BaseBlobRepository, IRelationRepository
    {
        private const string ContainerName = "ire-files";
        private const string MemoryCollection = "relations";
        
        public RelationRepository(
            ResilientBlobServiceClient blobServiceClient,
            ISemanticTextMemory memory,
            ILogger<RelationRepository> logger)
            : base(blobServiceClient, memory, logger)
        {
        }
        
        /// <summary>
        /// Salva um novo evento de rela��o no armazenamento
        /// </summary>
        public async Task<string> SaveRelationEventAsync(InterpretedRelationEvent relationEvent, CancellationToken cancellationToken = default)
        {
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            
            // Gerar ID �nico para o evento se n�o definido
            if (string.IsNullOrEmpty(relationEvent.Id))
            {
                relationEvent.Id = Guid.NewGuid().ToString();
            }
            
            // Criar nome do blob baseado no tipo de rela��o e timestamp
            string typeSegment = NormalizeBlobName(relationEvent.RelationType);
            string blobName = $"{typeSegment}/{DateTime.UtcNow:yyyyMMddHHmmss}_{relationEvent.Id}.ire";
            
            // Serializar e salvar o evento
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            
            string jsonContent = JsonSerializer.Serialize(relationEvent);
            await blobClient.UploadAsync(
                new BinaryData(jsonContent),
                overwrite: true,
                cancellationToken: cancellationToken
            );
            
            // Adicionar a eventos para busca sem�ntica
            await _memory.SaveInformationAsync(
                collection: MemoryCollection,
                id: relationEvent.Id,
                text: relationEvent.Description,
                description: $"Rela��o: {relationEvent.RelationType}, For�a: {relationEvent.RelationStrength:P0}",
                additionalMetadata: blobName,
                cancellationToken: cancellationToken
            );
            
            _logger.LogInformation("Evento de rela��o salvo: {Id}", relationEvent.Id);
            
            return relationEvent.Id;
        }
        
        /// <summary>
        /// Obt�m rela��es para eventos de atividade espec�ficos
        /// </summary>
        public async Task<IEnumerable<InterpretedRelationEvent>> GetRelationsForEventsAsync(
            IEnumerable<string> activityEventIds, CancellationToken cancellationToken = default)
        {
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var results = new List<InterpretedRelationEvent>();
            var eventIdSet = activityEventIds.ToHashSet();
            
            await foreach (var blob in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                var blobClient = containerClient.GetBlobClient(blob.Name);
                var content = await blobClient.DownloadContentAsync(cancellationToken);
                var jsonContent = content.Value.Content.ToString();
                
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    try
                    {
                        var relationEvent = JsonSerializer.Deserialize<InterpretedRelationEvent>(jsonContent);
                        if (relationEvent != null && relationEvent.RelatedEventIds.Any(id => eventIdSet.Contains(id)))
                        {
                            results.Add(relationEvent);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Erro ao deserializar evento de rela��o: {BlobName}", blob.Name);
                    }
                }
            }
            
            return results.OrderByDescending(e => e.Timestamp);
        }
        
        /// <summary>
        /// Obt�m rela��es dentro de uma janela de tempo espec�fica
        /// </summary>
        public async Task<IEnumerable<InterpretedRelationEvent>> GetRelationsInTimeRangeAsync(
            DateTime startTime, DateTime endTime, int maxRelations = 1000, CancellationToken cancellationToken = default)
        {
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            
            var results = new List<InterpretedRelationEvent>();
            
            await foreach (var blob in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                // Verificar se atingimos o m�ximo de rela��es
                if (results.Count >= maxRelations)
                    break;
                
                var blobClient = containerClient.GetBlobClient(blob.Name);
                var content = await blobClient.DownloadContentAsync(cancellationToken);
                var jsonContent = content.Value.Content.ToString();
                
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    try
                    {
                        var relationEvent = JsonSerializer.Deserialize<InterpretedRelationEvent>(jsonContent);
                        if (relationEvent != null && 
                            relationEvent.Timestamp >= startTime && 
                            relationEvent.Timestamp <= endTime)
                        {
                            results.Add(relationEvent);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Erro ao deserializar evento de rela��o: {BlobName}", blob.Name);
                    }
                }
            }
            
            return results.OrderByDescending(e => e.Timestamp);
        }
        
        /// <summary>
        /// Busca rela��es baseado em uma consulta textual
        /// </summary>
        public async Task<IEnumerable<InterpretedRelationEvent>> SearchRelationsAsync(
            string searchQuery, int maxResults = 10, CancellationToken cancellationToken = default)
        {
            // Usar busca sem�ntica para encontrar rela��es relacionadas
            var searchResults = await _memory.SearchAsync(
                collection: MemoryCollection,
                query: searchQuery,
                limit: maxResults,
                minRelevanceScore: 0.5,
                cancellationToken: cancellationToken
            );
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var results = new List<InterpretedRelationEvent>();
            
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
                            var relationEvent = JsonSerializer.Deserialize<InterpretedRelationEvent>(jsonContent);
                            if (relationEvent != null)
                            {
                                results.Add(relationEvent);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao recuperar evento de rela��o: {BlobPath}", blobPath);
                    }
                }
            }
            
            return results;
        }
    }
}