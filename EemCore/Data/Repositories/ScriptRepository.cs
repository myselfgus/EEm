using EemCore.Models;
using EemCore.Services.Resilient;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Memory;
using System.Text.Json;

namespace EemCore.Data.Repositories
{
    /// <summary>
    /// Implementa��o de reposit�rio para scripts GenAIScript
    /// </summary>
    public class ScriptRepository : BaseBlobRepository, IScriptRepository
    {
        private const string ContainerName = "scripts";
        private const string MemoryCollection = "scripts";
        private const string MetaSuffix = ".meta";
        private const string ContentSuffix = ".genai";
        
        public ScriptRepository(
            ResilientBlobServiceClient blobServiceClient,
            ISemanticTextMemory memory,
            ILogger<ScriptRepository> logger)
            : base(blobServiceClient, memory, logger)
        {
        }
        
        /// <summary>
        /// Salva um novo script no armazenamento
        /// </summary>
        public async Task<string> SaveScriptAsync(ScriptInfo script, string content, CancellationToken cancellationToken = default)
        {
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            
            // Gerar ID �nico para o script se n�o definido
            if (string.IsNullOrEmpty(script.Id))
            {
                script.Id = Guid.NewGuid().ToString();
            }
            
            // Atualizar timestamp de modifica��o
            script.ModifiedDateTime = DateTime.UtcNow;
            
            // Normalizar tags para ser uma lista de strings �nicas
            script.Tags = script.Tags
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct()
                .ToList();
            
            // Criar nomes dos blobs para metadados e conte�do
            string metaBlobName = $"{script.Id}{MetaSuffix}";
            string contentBlobName = $"{script.Id}{ContentSuffix}";
            
            // Serializar e salvar os metadados
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var metaBlobClient = containerClient.GetBlobClient(metaBlobName);
            
            string metaJson = JsonSerializer.Serialize(script);
            await metaBlobClient.UploadAsync(
                new BinaryData(metaJson),
                overwrite: true,
                cancellationToken: cancellationToken
            );
            
            // Salvar o conte�do
            var contentBlobClient = containerClient.GetBlobClient(contentBlobName);
            await contentBlobClient.UploadAsync(
                new BinaryData(content),
                overwrite: true,
                cancellationToken: cancellationToken
            );
            
            // Adicionar a scripts para busca sem�ntica
            string searchableText = $"{script.Name} {script.Description} {string.Join(" ", script.Tags)}";
            string scriptDescription = $"Script: {script.Name}, Ativo: {script.IsActive}";
            
            await _memory.SaveInformationAsync(
                collection: MemoryCollection,
                id: script.Id,
                text: searchableText,
                description: scriptDescription,
                additionalMetadata: script.Id,
                cancellationToken: cancellationToken
            );
            
            _logger.LogInformation("Script salvo: {Id}, {Name}", script.Id, script.Name);
            
            return script.Id;
        }
        
        /// <summary>
        /// Obt�m um script pelo nome
        /// </summary>
        public async Task<(ScriptInfo Info, string Content)?> GetScriptByNameAsync(string scriptName, CancellationToken cancellationToken = default)
        {
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            
            // Buscar script por nome usando busca sem�ntica
            var searchResults = await _memory.SearchAsync(
                collection: MemoryCollection,
                query: scriptName,
                limit: 10,
                minRelevanceScore: 0.7,
                withEmbeddings: false,
                cancellationToken: cancellationToken
            );
            
            // Obter o resultado mais relevante
            var bestMatch = searchResults.FirstOrDefault();
            if (bestMatch == null)
            {
                _logger.LogInformation("Script n�o encontrado: {Name}", scriptName);
                return null;
            }
            
            string scriptId = bestMatch.Metadata.AdditionalMetadata;
            return await GetScriptByIdAsync(scriptId, cancellationToken);
        }
        
        /// <summary>
        /// Obt�m um script pelo ID
        /// </summary>
        private async Task<(ScriptInfo Info, string Content)?> GetScriptByIdAsync(string scriptId, CancellationToken cancellationToken = default)
        {
            string metaBlobName = $"{scriptId}{MetaSuffix}";
            string contentBlobName = $"{scriptId}{ContentSuffix}";
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            
            // Obter metadados
            try
            {
                var metaBlobClient = containerClient.GetBlobClient(metaBlobName);
                var metaContent = await metaBlobClient.DownloadContentAsync(cancellationToken);
                var metaJson = metaContent.Value.Content.ToString();
                
                if (string.IsNullOrEmpty(metaJson))
                {
                    _logger.LogWarning("Metadados vazios para script: {Id}", scriptId);
                    return null;
                }
                
                var scriptInfo = JsonSerializer.Deserialize<ScriptInfo>(metaJson);
                if (scriptInfo == null)
                {
                    _logger.LogWarning("Falha ao deserializar metadados para script: {Id}", scriptId);
                    return null;
                }
                
                // Obter conte�do
                var contentBlobClient = containerClient.GetBlobClient(contentBlobName);
                var contentResult = await contentBlobClient.DownloadContentAsync(cancellationToken);
                var content = contentResult.Value.Content.ToString();
                
                _logger.LogInformation("Script obtido: {Id}, {Name}", scriptInfo.Id, scriptInfo.Name);
                
                return (scriptInfo, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter script: {Id}", scriptId);
                return null;
            }
        }
        
        /// <summary>
        /// Lista todos os scripts dispon�veis
        /// </summary>
        public async Task<IEnumerable<ScriptInfo>> ListScriptsAsync(CancellationToken cancellationToken = default)
        {
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var results = new List<ScriptInfo>();
            
            // Buscar todos os blobs de metadados
            await foreach (var blob in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                if (blob.Name.EndsWith(MetaSuffix))
                {
                    try
                    {
                        var blobClient = containerClient.GetBlobClient(blob.Name);
                        var content = await blobClient.DownloadContentAsync(cancellationToken);
                        var jsonContent = content.Value.Content.ToString();
                        
                        if (!string.IsNullOrEmpty(jsonContent))
                        {
                            var scriptInfo = JsonSerializer.Deserialize<ScriptInfo>(jsonContent);
                            if (scriptInfo != null)
                            {
                                results.Add(scriptInfo);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao listar script: {BlobName}", blob.Name);
                    }
                }
            }
            
            _logger.LogInformation("Scripts listados: {Count}", results.Count);
            
            return results.OrderBy(s => s.Name);
        }
        
        /// <summary>
        /// Exclui um script espec�fico
        /// </summary>
        public async Task<bool> DeleteScriptAsync(string scriptId, CancellationToken cancellationToken = default)
        {
            await EnsureContainerExistsAsync(ContainerName, cancellationToken);
            
            string metaBlobName = $"{scriptId}{MetaSuffix}";
            string contentBlobName = $"{scriptId}{ContentSuffix}";
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            
            try
            {
                // Excluir metadados
                var metaBlobClient = containerClient.GetBlobClient(metaBlobName);
                await metaBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                
                // Excluir conte�do
                var contentBlobClient = containerClient.GetBlobClient(contentBlobName);
                await contentBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                
                // Remover da mem�ria sem�ntica
                await _memory.RemoveAsync(MemoryCollection, scriptId, cancellationToken);
                
                _logger.LogInformation("Script exclu�do: {Id}", scriptId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao excluir script: {Id}", scriptId);
                return false;
            }
        }
    }
}