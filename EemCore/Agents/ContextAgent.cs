using Microsoft.MCP;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using System.ComponentModel;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.AI.OpenAI;
using Microsoft.Azure.Cosmos;
using System.Text.Json;

namespace EemCore.Agents
{
    /// <summary>
    /// Implementação do Agente de Contexto para o sistema Εεm (Ευ-εnable-memory).
    /// 
    /// O Agente de Contexto é responsável por:
    /// 1. Responder às requisições MCP fornecendo contexto relevante
    /// 2. Navegar pela meta-estrutura .Re para acessar informações hierárquicas
    /// 3. Recuperar dados de arquivos .aje, .ire e .e conforme necessário
    /// 4. Formatar o contexto para uso em assistentes de IA
    /// </summary>
    [McpToolType]
    public class ContextAgent
    {
        private readonly ISemanticTextMemory _memory;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly CosmosClient _cosmosClient;
        private readonly OpenAIClient _openAIClient;
        private readonly ILogger<ContextAgent> _logger;

        private const string AjeContainer = "aje-files";
        private const string IreContainer = "ire-files";
        private const string EContainer = "e-files";
        private const string GraphDatabase = "eemmemory";
        private const string GraphContainer = "relationalgraph";

        /// <summary>
        /// Construtor do Agente de Contexto com injeção de dependências
        /// </summary>
        public ContextAgent(
            ISemanticTextMemory memory,
            BlobServiceClient blobServiceClient,
            CosmosClient cosmosClient,
            OpenAIClient openAIClient,
            ILogger<ContextAgent> logger)
        {
            _memory = memory;
            _blobServiceClient = blobServiceClient;
            _cosmosClient = cosmosClient;
            _openAIClient = openAIClient;
            _logger = logger;
        }

        /// <summary>
        /// Recupera o contexto relevante com base em uma consulta.
        /// Esta é a principal ferramenta MCP exposta pelo agente.
        /// </summary>
        [McpTool("GetRelevantContext")]
        [Description("Retrieves relevant context from Εεm memory systems based on a query")]
        public async Task<string> GetRelevantContextAsync(
            [Description("The query to search for relevant context")]
            string query,
            
            [Description("Maximum number of results to return")]
            int maxResults = 5,
            
            [Description("Types of files to search: 'aje', 'ire', 'e', or 'all'")]
            string searchIn = "all",
            
            [Description("Time window in hours to search back, 0 for unlimited")]
            int timeWindowHours = 24)
        {
            _logger.LogInformation($"Buscando contexto para: {query} (max: {maxResults} resultados)");
            
            try
            {
                // 1. Primeiro, buscar no grafo de relacionamentos (.Re)
                var graphResults = await SearchRelationalGraphAsync(query, maxResults);
                
                // 2. Buscar em arquivos específicos baseado nos parâmetros
                var fileResults = await SearchFilesAsync(query, searchIn, timeWindowHours, maxResults);
                
                // 3. Combinar e priorizar resultados
                var combinedResults = CombineAndPrioritizeResults(graphResults, fileResults, maxResults);
                
                // 4. Formatar resposta para o cliente MCP
                return FormatContextResponse(combinedResults, query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao buscar contexto para: {query}");
                return $"Erro ao buscar contexto: {ex.Message}";
            }
        }

        /// <summary>
        /// Armazena uma nova observação no sistema Εεm
        /// </summary>
        [McpTool("StoreObservation")]
        [Description("Stores a new observation in the Εεm memory system")]
        public async Task<string> StoreObservationAsync(
            [Description("Content of the observation")]
            string content,
            
            [Description("Type of the observation, e.g., 'ide_activity', 'search', 'ai_interaction'")]
            string observationType,
            
            [Description("Optional session ID to associate with the observation")]
            string? sessionId = null)
        {
            _logger.LogInformation($"Armazenando observação do tipo '{observationType}'");
            
            try
            {
                // Criar ID para a sessão se não for fornecido
                sessionId ??= Guid.NewGuid().ToString();
                
                // Criar evento de atividade
                var activityEvent = new ActivityJournalEvent
                {
                    Timestamp = DateTime.UtcNow,
                    ActivityType = observationType,
                    Content = content,
                    Source = "MCP Client",
                    SessionId = sessionId
                };
                
                // Salvar no Blob Storage como arquivo .aje
                var blobName = $"{sessionId}/{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid()}.aje";
                var containerClient = _blobServiceClient.GetBlobContainerClient(AjeContainer);
                var blobClient = containerClient.GetBlobClient(blobName);
                
                await blobClient.UploadAsync(
                    BinaryData.FromString(JsonSerializer.Serialize(activityEvent)),
                    new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } }
                );
                
                // Adicionar à memória vetorial para busca futura
                await _memory.SaveInformationAsync(
                    collection: "observations",
                    id: blobName,
                    text: content,
                    description: $"Activity: {observationType}"
                );
                
                return $"Observação armazenada com ID: {blobName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao armazenar observação: {ex.Message}");
                return $"Erro ao armazenar observação: {ex.Message}";
            }
        }

        /// <summary>
        /// Busca relacionamentos no grafo Eulerian (.Re)
        /// </summary>
        private async Task<List<ContextResult>> SearchRelationalGraphAsync(string query, int limit)
        {
            // Em uma implementação real, isto executaria uma consulta Gremlin no Cosmos DB
            var results = new List<ContextResult>();
            
            try
            {
                // Conexão com o container Gremlin
                var graphContainer = _cosmosClient.GetContainer(GraphDatabase, GraphContainer);
                
                // Exemplo de consulta (simplificada para este documento)
                // Na implementação real, usaria ParamaterizedQuery para evitar injeção
                var gremlinQuery = $"g.V().has('content', textContainsRegex('{query}')).limit({limit})";
                
                // Simulação de resultados (para este documento executável)
                for (int i = 0; i < Math.Min(3, limit); i++)
                {
                    results.Add(new ContextResult
                    {
                        Source = "RelationalGraph",
                        Content = $"Nó relacionado a '{query}' - ID: {Guid.NewGuid()}",
                        Relevance = 0.85 - (i * 0.1),
                        Timestamp = DateTime.UtcNow.AddHours(-i)
                    });
                }
                
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao buscar no grafo relacional: {ex.Message}");
                return results; // Retorna lista vazia ou parcial em caso de erro
            }
        }

        /// <summary>
        /// Busca nos arquivos .aje, .ire e .e conforme parâmetros
        /// </summary>
        private async Task<List<ContextResult>> SearchFilesAsync(
            string query, string searchIn, int timeWindowHours, int limit)
        {
            var results = new List<ContextResult>();
            
            // Determinar quais tipos de arquivo buscar
            var searchAje = searchIn == "all" || searchIn == "aje";
            var searchIre = searchIn == "all" || searchIn == "ire";
            var searchE = searchIn == "all" || searchIn == "e";
            
            // Calcular janela de tempo
            var cutoffTime = timeWindowHours > 0 
                ? DateTime.UtcNow.AddHours(-timeWindowHours) 
                : DateTime.MinValue;
            
            // Usar a memória semântica para busca vetorial
            if (searchAje)
            {
                var ajeResults = await _memory.SearchAsync(
                    collection: "observations",
                    query: query,
                    limit: limit,
                    minRelevanceScore: 0.7
                );
                
                foreach (var result in ajeResults)
                {
                    // Obter detalhes completos do Blob Storage
                    var blobClient = _blobServiceClient
                        .GetBlobContainerClient(AjeContainer)
                        .GetBlobClient(result.Metadata.Id);
                    
                    // Simular conteúdo para documento executável
                    results.Add(new ContextResult
                    {
                        Source = "ActivityJournal",
                        Content = $"Atividade relacionada a '{query}': {result.Metadata.Description}",
                        Relevance = result.Relevance,
                        Timestamp = DateTime.UtcNow.AddHours(-1)
                    });
                }
            }
            
            // Implementações similares para searchIre e searchE
            // ...
            
            return results;
        }

        /// <summary>
        /// Combina e prioriza resultados de diferentes fontes
        /// </summary>
        private List<ContextResult> CombineAndPrioritizeResults(
            List<ContextResult> graphResults, 
            List<ContextResult> fileResults, 
            int maxResults)
        {
            // Combinar todos os resultados
            var allResults = new List<ContextResult>();
            allResults.AddRange(graphResults);
            allResults.AddRange(fileResults);
            
            // Ordenar por relevância e recenticidade
            return allResults
                .OrderByDescending(r => r.Relevance * 0.7 + (1.0 / (DateTime.UtcNow - r.Timestamp).TotalHours) * 0.3)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Formata a resposta para o cliente MCP
        /// </summary>
        private string FormatContextResponse(List<ContextResult> results, string query)
        {
            var response = new StringBuilder();
            
            response.AppendLine($"## Contexto relevante para: '{query}'");
            response.AppendLine();
            
            if (results.Count == 0)
            {
                response.AppendLine("Nenhum contexto relevante encontrado.");
                return response.ToString();
            }
            
            foreach (var result in results)
            {
                response.AppendLine($"### {result.Source} ({result.Timestamp:g}) - Relevância: {result.Relevance:P0}");
                response.AppendLine(result.Content);
                response.AppendLine();
            }
            
            return response.ToString();
        }
    }

    /// <summary>
    /// Representa um evento de atividade para o journal
    /// </summary>
    public class ActivityJournalEvent
    {
        public DateTime Timestamp { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Representa um resultado de contexto
    /// </summary>
    public class ContextResult
    {
        public string Source { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Relevance { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
