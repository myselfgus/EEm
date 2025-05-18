using EemCore.Models;
using EemCore.Services.Resilient;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Memory;
using System.Text.Json;

namespace EemCore.Data.Repositories
{
    /// <summary>
    /// Implementação de repositório para fluxos eulerianos
    /// </summary>
    public class EulerianFlowRepository : BaseBlobRepository, IEulerianFlowRepository
    {
        private const string ContainerName = "e-files";
        private const string MemoryCollection = "eulerianflows";
        
        public EulerianFlowRepository(
            ResilientBlobServiceClient blobServiceClient,
            ISemanticTextMemory memory,
            ILogger<EulerianFlowRepository> logger)
            : base(blobServiceClient, memory, logger)
        {
        }
        
        /// <summary>
        /// Salva um novo fluxo euleriano no armazenamento
        /// </summary>
        public async Task<string> SaveFlowAsync(EulerianFlow flow, CancellationToken cancellationToken = default)
        {
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            
            // Gerar ID único para o fluxo se não definido
            if (string.IsNullOrEmpty(flow.Id))
            {
                flow.Id = Guid.NewGuid().ToString();
            }
            
            // Garantir que o nome é válido para o blob
            string safeName = NormalizeBlobName(flow.Name);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = flow.Id;
            }
            
            // Criar nome do blob usando o timestamp e ID
            string blobName = $"{DateTime.UtcNow:yyyyMMdd}/{safeName}_{flow.Id}.e";
            
            // Serializar e salvar o fluxo
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            
            string jsonContent = JsonSerializer.Serialize(flow, new JsonSerializerOptions
            {
                WriteIndented = true // Para melhor legibilidade
            });
            
            await blobClient.UploadAsync(
                new BinaryData(jsonContent),
                overwrite: true,
                cancellationToken: cancellationToken
            );
            
            // Preparar texto para busca semântica
            string searchableText = $"{flow.Name} {flow.Summary} {string.Join(" ", flow.Categories)}";
            
            // Adicionar a fluxos para busca semântica
            await _memory.SaveInformationAsync(
                collection: MemoryCollection,
                id: flow.Id,
                text: searchableText,
                description: $"Fluxo: {flow.Name}, Nós: {flow.Nodes.Count}, Arestas: {flow.Edges.Count}",
                additionalMetadata: blobName,
                cancellationToken: cancellationToken
            );
            
            _logger.LogInformation("Fluxo euleriano salvo: {Id}, {Name}", flow.Id, flow.Name);
            
            return flow.Id;
        }
        
        /// <summary>
        /// Obtém fluxos eulerianos dentro de uma janela de tempo específica
        /// </summary>
        public async Task<IEnumerable<EulerianFlow>> GetFlowsInTimeRangeAsync(
            DateTime startTime, DateTime endTime, int maxFlows = 100, CancellationToken cancellationToken = default)
        {
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var results = new List<EulerianFlow>();
            
            await foreach (var blob in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                // Verificar se atingimos o máximo de fluxos
                if (results.Count >= maxFlows)
                    break;
                
                try
                {
                    var blobClient = containerClient.GetBlobClient(blob.Name);
                    var content = await blobClient.DownloadContentAsync(cancellationToken);
                    var jsonContent = content.Value.Content.ToString();
                    
                    if (!string.IsNullOrEmpty(jsonContent))
                    {
                        var flow = JsonSerializer.Deserialize<EulerianFlow>(jsonContent);
                        if (flow != null && 
                            flow.Timestamp >= startTime && 
                            flow.Timestamp <= endTime)
                        {
                            results.Add(flow);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar fluxo euleriano: {BlobName}", blob.Name);
                }
            }
            
            return results.OrderByDescending(f => f.Timestamp);
        }
        
        /// <summary>
        /// Obtém um fluxo euleriano específico pelo ID
        /// </summary>
        public async Task<EulerianFlow?> GetFlowByIdAsync(string flowId, CancellationToken cancellationToken = default)
        {
            // Buscar no memory store para obter o caminho do blob
            var memoryResults = await _memory.GetAsync(
                collection: MemoryCollection,
                key: flowId,
                withEmbedding: false,
                cancellationToken: cancellationToken);
                
            if (memoryResults == null)
            {
                _logger.LogWarning("Fluxo euleriano não encontrado em memória: {Id}", flowId);
                return null;
            }
            
            string blobPath = memoryResults.Metadata.AdditionalMetadata;
            
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
                var blobClient = containerClient.GetBlobClient(blobPath);
                
                var content = await blobClient.DownloadContentAsync(cancellationToken);
                var jsonContent = content.Value.Content.ToString();
                
                if (string.IsNullOrEmpty(jsonContent))
                {
                    _logger.LogWarning("Conteúdo vazio para fluxo euleriano: {Id}", flowId);
                    return null;
                }
                
                var flow = JsonSerializer.Deserialize<EulerianFlow>(jsonContent);
                return flow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter fluxo euleriano: {Id}", flowId);
                return null;
            }
        }
        
        /// <summary>
        /// Busca fluxos eulerianos baseado em uma consulta textual
        /// </summary>
        public async Task<IEnumerable<EulerianFlow>> SearchFlowsAsync(
            string searchQuery, int maxResults = 10, CancellationToken cancellationToken = default)
        {
            // Usar busca semântica para encontrar fluxos relacionados
            var searchResults = await _memory.SearchAsync(
                collection: MemoryCollection,
                query: searchQuery,
                limit: maxResults,
                minRelevanceScore: 0.5,
                cancellationToken: cancellationToken
            );
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var results = new List<EulerianFlow>();
            
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
                            var flow = JsonSerializer.Deserialize<EulerianFlow>(jsonContent);
                            if (flow != null)
                            {
                                results.Add(flow);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao recuperar fluxo euleriano: {BlobPath}", blobPath);
                    }
                }
            }
            
            return results;
        }
    }
}