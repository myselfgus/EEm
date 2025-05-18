using Microsoft.MCP;
using Microsoft.SemanticKernel.Memory;
using System.ComponentModel;
using System.Text;
using EemCore.Data.Repositories;
using EemCore.Models;
using EemCore.Services;
using Microsoft.Extensions.Logging;
using EemCore.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Encodings.Web;

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
        private readonly IActivityRepository _activityRepository;
        private readonly IRelationRepository _relationRepository;
        private readonly IEulerianFlowRepository _flowRepository;
        private readonly ISemanticTextMemory _memory;
        private readonly ContextEnrichmentService _enrichmentService;
        private readonly ILogger<ContextAgent> _logger;
        private readonly EemOptions _options;
        
        private const string ActivitiesCollection = "activities";
        private const string RelationsCollection = "relations";
        private const string FlowsCollection = "flows";
        
        public ContextAgent(
            IActivityRepository activityRepository,
            IRelationRepository relationRepository,
            IEulerianFlowRepository flowRepository,
            ISemanticTextMemory memory,
            ContextEnrichmentService enrichmentService,
            ILogger<ContextAgent> logger,
            IOptions<EemOptions> options)
        {
            _activityRepository = activityRepository;
            _relationRepository = relationRepository;
            _flowRepository = flowRepository;
            _memory = memory;
            _enrichmentService = enrichmentService;
            _logger = logger;
            _options = options.Value;
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
            try
            {
                _logger.LogInformation("Buscando contexto para consulta: {Query}", query);
                
                // Validar os parâmetros
                maxResults = Math.Clamp(maxResults, 1, 20);
                searchIn = NormalizeSearchTypes(searchIn);
                
                // Definir janela de tempo
                DateTime startTime = timeWindowHours > 0 
                    ? DateTime.UtcNow.AddHours(-timeWindowHours)
                    : DateTime.MinValue;
                
                // Buscar primeiro via memória semântica para melhor relevância
                var relevantItems = await SearchMemoryAsync(query, searchIn, maxResults);
                
                // Se não encontrou resultados suficientes, buscar por tempo
                if (relevantItems.Count < maxResults)
                {
                    await AppendTimeBasedResultsAsync(relevantItems, query, searchIn, 
                        startTime, maxResults - relevantItems.Count);
                }
                
                if (relevantItems.Count == 0)
                {
                    return $"Nenhum contexto relevante encontrado para: \"{query}\"\n" +
                           $"Tente expandir a janela de tempo ou mudar os termos da consulta.";
                }
                
                // Separar IDs por tipo para enriquecimento
                string[] activityIds = relevantItems
                    .Where(i => i.Type == "aje")
                    .Select(i => i.Id)
                    .ToArray();
                    
                string[] flowIds = relevantItems
                    .Where(i => i.Type == "e")
                    .Select(i => i.Id)
                    .ToArray();
                
                // Enriquecer o contexto usando o serviço de enriquecimento
                string enrichedContext = await _enrichmentService.EnrichContextAsync(
                    query,
                    activityIds,
                    flowIds);
                
                return enrichedContext;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao recuperar contexto para consulta: {Query}", query);
                return $"Erro ao recuperar contexto: {ex.Message}";
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
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    return "Erro: O conteúdo da observação não pode estar vazio.";
                }
                
                if (string.IsNullOrWhiteSpace(observationType))
                {
                    return "Erro: O tipo da observação não pode estar vazio.";
                }
                
                // Criar um novo evento de atividade
                var activityEvent = new ActivityJournalEvent
                {
                    ActivityType = observationType,
                    Content = content,
                    Source = "mcp_manual",
                    SessionId = sessionId ?? Guid.NewGuid().ToString()
                };
                
                // Salvar o evento
                string eventId = await _activityRepository.SaveActivityEventAsync(activityEvent);
                
                _logger.LogInformation("Observação armazenada com sucesso: {Id}, Tipo: {Type}",
                    eventId, observationType);
                
                return $"Observação armazenada com sucesso. ID: {eventId}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao armazenar observação");
                return $"Erro ao armazenar observação: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Obtém informações sobre uma sessão específica
        /// </summary>
        [McpTool("GetSessionInfo")]
        [Description("Gets information about a specific session")]
        public async Task<string> GetSessionInfoAsync(
            [Description("Session ID to get information about")]
            string sessionId)
        {
            try
            {
                // Buscar atividades da sessão
                var activities = await _activityRepository.GetActivityEventsForSessionAsync(sessionId);
                
                if (!activities.Any())
                {
                    return $"Nenhuma atividade encontrada para a sessão {sessionId}.";
                }
                
                // Agrupar por tipo de atividade
                var groupedActivities = activities.GroupBy(a => a.ActivityType);
                
                // Calcular estatísticas
                var firstActivity = activities.OrderBy(a => a.Timestamp).First();
                var lastActivity = activities.OrderBy(a => a.Timestamp).Last();
                var duration = lastActivity.Timestamp - firstActivity.Timestamp;
                
                // Formatar a saída
                var sb = new StringBuilder();
                sb.AppendLine($"# Informações da Sessão: {sessionId}");
                sb.AppendLine();
                sb.AppendLine($"Total de atividades: {activities.Count()}");
                sb.AppendLine($"Início: {firstActivity.Timestamp:g}");
                sb.AppendLine($"Fim: {lastActivity.Timestamp:g}");
                sb.AppendLine($"Duração: {FormatDuration(duration)}");
                sb.AppendLine();
                
                sb.AppendLine("## Tipos de Atividade");
                foreach (var group in groupedActivities)
                {
                    sb.AppendLine($"- {group.Key}: {group.Count()} atividades");
                }
                
                sb.AppendLine();
                sb.AppendLine("## Fontes de Atividade");
                foreach (var source in activities.GroupBy(a => a.Source))
                {
                    sb.AppendLine($"- {source.Key}: {source.Count()} atividades");
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter informações da sessão {SessionId}", sessionId);
                return $"Erro ao obter informações da sessão: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Gera um contexto baseado em atividades recentes
        /// </summary>
        [McpTool("GenerateContext")]
        [Description("Generates a context based on recent activities")]
        public async Task<string> GenerateContextAsync(
            [Description("Session ID to get activities from")]
            string sessionId,
            
            [Description("Optional focus topic to prioritize in the context")]
            string? focusTopic = null,
            
            [Description("Maximum number of activities to include")]
            int maxActivities = 20)
        {
            _logger.LogInformation("Gerando contexto para sessão: {SessionId}, foco: {Focus}", sessionId, focusTopic ?? "não especificado");
            
            // Obter atividades recentes
            var activities = (await _activityRepository.GetActivityEventsForSessionAsync(sessionId))
                .OrderByDescending(a => a.Timestamp)
                .Take(maxActivities)
                .ToList();
                
            if (activities.Count == 0)
            {
                return "Não há atividades recentes para gerar contexto.";
            }
            
            // Filtrar atividades por relevância se um tópico de foco for fornecido
            if (!string.IsNullOrWhiteSpace(focusTopic))
            {
                var focusActivities = await _activityRepository.SearchActivityEventsAsync(
                    focusTopic, maxActivities);
                    
                // Combinar atividades recentes com atividades relacionadas ao foco
                activities = activities
                    .Union(focusActivities)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(maxActivities)
                    .ToList();
            }
            
            // Obter relações para as atividades
            var activityIds = activities.Select(a => a.Id).ToList();
            var relations = await _relationRepository.GetRelationsForEventsAsync(activityIds);
            
            // Gerar contexto
            var contextBuilder = new StringBuilder();
            
            // Adicionar cabeçalho
            contextBuilder.AppendLine("# Contexto de Atividades");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine($"Gerado em: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            contextBuilder.AppendLine($"Sessão: {sessionId}");
            if (!string.IsNullOrWhiteSpace(focusTopic))
            {
                contextBuilder.AppendLine($"Foco: {focusTopic}");
            }
            contextBuilder.AppendLine();
            
            // Adicionar resumo
            contextBuilder.AppendLine("## Resumo");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine($"- {activities.Count} atividades recentes");
            contextBuilder.AppendLine($"- {relations.Count()} relações entre atividades");
            contextBuilder.AppendLine($"- Período: {activities.Min(a => a.Timestamp):yyyy-MM-dd HH:mm} a {activities.Max(a => a.Timestamp):yyyy-MM-dd HH:mm}");
            contextBuilder.AppendLine();
            
            // Adicionar atividades
            contextBuilder.AppendLine("## Atividades Recentes");
            contextBuilder.AppendLine();
            
            foreach (var activity in activities.Take(10)) // Limitar a 10 para o contexto
            {
                contextBuilder.AppendLine($"### {activity.ActivityType} ({activity.Timestamp:HH:mm:ss})");
                contextBuilder.AppendLine($"**Fonte**: {activity.Source}");
                contextBuilder.AppendLine($"**ID**: {activity.Id}");
                contextBuilder.AppendLine();
                
                // Limitar o tamanho do conteúdo para não sobrecarregar o contexto
                var contentPreview = activity.Content.Length > 300
                    ? activity.Content.Substring(0, 300) + "..."
                    : activity.Content;
                
                contextBuilder.AppendLine("```");
                contextBuilder.AppendLine(contentPreview);
                contextBuilder.AppendLine("```");
                contextBuilder.AppendLine();
            }
            
            // Adicionar relações se existirem
            if (relations.Any())
            {
                contextBuilder.AppendLine("## Relações Relevantes");
                contextBuilder.AppendLine();
                
                foreach (var relation in relations.Take(5)) // Limitar a 5 para o contexto
                {
                    contextBuilder.AppendLine($"- **{relation.RelationType}** (Força: {relation.RelationStrength:P0})");
                    if (!string.IsNullOrWhiteSpace(relation.Description))
                    {
                        contextBuilder.AppendLine($"  {relation.Description}");
                    }
                    contextBuilder.AppendLine($"  IDs relacionados: {string.Join(", ", relation.RelatedEventIds)}");
                    contextBuilder.AppendLine();
                }
            }
            
            return contextBuilder.ToString();
        }
        
        /// <summary>
        /// Exporta contexto em um formato específico
        /// </summary>
        [McpTool("ExportContext")]
        [Description("Exports context in a specific format")]
        public async Task<string> ExportContextAsync(
            [Description("Session ID to export context for")]
            string sessionId,
            
            [Description("Format to export (markdown, json, html)")]
            string format = "markdown",
            
            [Description("Time range in hours to include activities")]
            int timeRangeHours = 24)
        {
            _logger.LogInformation("Exportando contexto para sessão {SessionId} em formato {Format}", sessionId, format);
            
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddHours(-timeRangeHours);
            
            // Obter atividades no intervalo de tempo
            var activities = await _activityRepository.GetActivityEventsInTimeRangeAsync(
                startTime, endTime, _options.MaxEventsPerActivity);
                
            // Filtrar atividades para a sessão específica
            activities = activities.Where(a => a.SessionId == sessionId).ToList();
            
            if (!activities.Any())
            {
                return $"Não há atividades para a sessão {sessionId} no intervalo de tempo especificado.";
            }
            
            // Exportar de acordo com o formato
            format = format.ToLowerInvariant();
            
            if (format == "json")
            {
                return JsonSerializer.Serialize(activities, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            else if (format == "html")
            {
                return GenerateHtmlContext(activities, sessionId);
            }
            else // markdown (default)
            {
                return await GenerateContextAsync(sessionId, null, activities.Count());
            }
        }
        
        /// <summary>
        /// Analisa a relevância de um item de contexto para uma consulta
        /// </summary>
        [McpTool("AnalyzeContextRelevance")]
        [Description("Analyzes the relevance of a context item for a query")]
        public async Task<Dictionary<string, double>> AnalyzeContextRelevanceAsync(
            [Description("Query to analyze relevance for")]
            string query,
            
            [Description("Session ID to get context from")]
            string sessionId,
            
            [Description("Maximum number of results to return")]
            int maxResults = 5)
        {
            _logger.LogInformation("Analisando relevância de contexto para consulta: {Query}", query);
            
            // Buscar atividades relacionadas à consulta
            var relevantActivities = await _activityRepository.SearchActivityEventsAsync(
                query, maxResults * 2); // Buscar mais para filtrar depois
                
            // Filtrar para a sessão especificada
            relevantActivities = relevantActivities
                .Where(a => a.SessionId == sessionId)
                .Take(maxResults)
                .ToList();
                
            var relevanceScores = new Dictionary<string, double>();
            
            // Calcular relevância (simulada - em produção usaria embeddings reais)
            foreach (var activity in relevantActivities)
            {
                // Cálculo de relevância simulado - na versão real usaria similaridade de embeddings
                double score = 0.5; // Base score
                
                // Aumentar score para atividades mais recentes
                var hoursAgo = (DateTime.UtcNow - activity.Timestamp).TotalHours;
                if (hoursAgo < 1)
                    score += 0.3;
                else if (hoursAgo < 4)
                    score += 0.2;
                else if (hoursAgo < 24)
                    score += 0.1;
                
                // Aumentar score com base em correspondência da consulta no conteúdo
                if (activity.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                    score += 0.2;
                
                relevanceScores[activity.Id] = Math.Min(0.95, score); // Cap no máximo em 0.95
            }
            
            return relevanceScores
                .OrderByDescending(kvp => kvp.Value)
                .Take(maxResults)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        
        /// <summary>
        /// Gera um contexto HTML para visualização
        /// </summary>
        private string GenerateHtmlContext(IEnumerable<ActivityJournalEvent> activities, string sessionId)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"pt-br\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("  <title>Contexto Εεm</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; margin: 0; padding: 20px; color: #333; }");
            sb.AppendLine("    h1 { color: #0066cc; }");
            sb.AppendLine("    h2 { color: #0099cc; margin-top: 20px; }");
            sb.AppendLine("    h3 { color: #444; margin-top: 15px; }");
            sb.AppendLine("    .activity { border: 1px solid #ddd; padding: 15px; margin-bottom: 15px; border-radius: 5px; }");
            sb.AppendLine("    .activity-header { display: flex; justify-content: space-between; }");
            sb.AppendLine("    .activity-content { background-color: #f9f9f9; padding: 10px; border-radius: 3px; margin-top: 10px; white-space: pre-wrap; }");
            sb.AppendLine("    .metadata { color: #666; font-size: 0.9em; }");
            sb.AppendLine("    .summary { background-color: #f0f8ff; padding: 15px; border-radius: 5px; margin-bottom: 20px; }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            
            sb.AppendLine("<h1>Contexto de Atividades Εεm</h1>");
            
            sb.AppendLine("<div class=\"summary\">");
            sb.AppendLine($"<p><strong>Sessão:</strong> {sessionId}</p>");
            sb.AppendLine($"<p><strong>Gerado em:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
            sb.AppendLine($"<p><strong>Total de Atividades:</strong> {activities.Count()}</p>");
            
            if (activities.Any())
            {
                sb.AppendLine($"<p><strong>Período:</strong> {activities.Min(a => a.Timestamp):yyyy-MM-dd HH:mm} a {activities.Max(a => a.Timestamp):yyyy-MM-dd HH:mm}</p>");
            }
            
            sb.AppendLine("</div>");
            
            sb.AppendLine("<h2>Atividades</h2>");
            
            foreach (var activity in activities)
            {
                sb.AppendLine("<div class=\"activity\">");
                sb.AppendLine("  <div class=\"activity-header\">");
                sb.AppendLine($"    <h3>{activity.ActivityType}</h3>");
                sb.AppendLine($"    <span class=\"metadata\">{activity.Timestamp:yyyy-MM-dd HH:mm:ss}</span>");
                sb.AppendLine("  </div>");
                
                sb.AppendLine($"  <p class=\"metadata\"><strong>Fonte:</strong> {activity.Source}</p>");
                sb.AppendLine($"  <p class=\"metadata\"><strong>ID:</strong> {activity.Id}</p>");
                
                sb.AppendLine("  <div class=\"activity-content\">");
                sb.AppendLine(HtmlEncode(activity.Content));
                sb.AppendLine("  </div>");
                sb.AppendLine("</div>");
            }
            
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Codifica uma string para HTML
        /// </summary>
        private string HtmlEncode(string text)
        {
            return HtmlEncoder.Default.Encode(text);
        }
        
        // Métodos auxiliares
        
        /// <summary>
        /// Busca na memória semântica por itens relevantes
        /// </summary>
        private async Task<List<RelevantItem>> SearchMemoryAsync(
            string query, 
            string searchIn,
            int maxResults)
        {
            var relevantItems = new List<RelevantItem>();
            
            // Buscar em coleções conforme o tipo solicitado
            if (searchIn.Contains("aje") || searchIn == "all")
            {
                var activityResults = await _memory.SearchAsync(
                    ActivitiesCollection,
                    query,
                    limit: maxResults,
                    minRelevanceScore: 0.5);
                    
                foreach (var result in activityResults)
                {
                    relevantItems.Add(new RelevantItem
                    {
                        Id = result.Metadata.Id,
                        Type = "aje",
                        Score = result.Relevance,
                        Description = result.Metadata.Description
                    });
                }
            }
            
            if (searchIn.Contains("ire") || searchIn == "all")
            {
                var relationResults = await _memory.SearchAsync(
                    RelationsCollection,
                    query,
                    limit: maxResults,
                    minRelevanceScore: 0.5);
                    
                foreach (var result in relationResults)
                {
                    relevantItems.Add(new RelevantItem
                    {
                        Id = result.Metadata.Id,
                        Type = "ire",
                        Score = result.Relevance,
                        Description = result.Metadata.Description
                    });
                }
            }
            
            if (searchIn.Contains("e") || searchIn == "all")
            {
                var flowResults = await _memory.SearchAsync(
                    FlowsCollection,
                    query,
                    limit: maxResults,
                    minRelevanceScore: 0.5);
                    
                foreach (var result in flowResults)
                {
                    relevantItems.Add(new RelevantItem
                    {
                        Id = result.Metadata.Id,
                        Type = "e",
                        Score = result.Relevance,
                        Description = result.Metadata.Description
                    });
                }
            }
            
            // Ordenar por relevância e limitar resultados
            return relevantItems
                .OrderByDescending(i => i.Score)
                .Take(maxResults)
                .ToList();
        }
        
        /// <summary>
        /// Adiciona resultados baseados em tempo se não houver resultados suficientes da busca semântica
        /// </summary>
        private async Task AppendTimeBasedResultsAsync(
            List<RelevantItem> existingItems,
            string query, 
            string searchIn,
            DateTime startTime,
            int maxAdditionalResults)
        {
            var existingIds = new HashSet<string>(existingItems.Select(i => i.Id));
            
            // Buscar atividades recentes
            if (searchIn.Contains("aje") || searchIn == "all")
            {
                var recentActivities = await _activityRepository.GetActivityEventsInTimeRangeAsync(
                    startTime, DateTime.UtcNow, maxAdditionalResults * 2);
                    
                foreach (var activity in recentActivities)
                {
                    if (existingIds.Contains(activity.Id))
                        continue;
                        
                    existingItems.Add(new RelevantItem
                    {
                        Id = activity.Id,
                        Type = "aje",
                        Score = 0.4, // Score arbitrário para resultados baseados em tempo
                        Description = $"Atividade: {activity.ActivityType}, Fonte: {activity.Source}"
                    });
                    
                    existingIds.Add(activity.Id);
                    
                    if (existingItems.Count >= maxAdditionalResults)
                        return;
                }
            }
            
            // Buscar relações recentes
            if (searchIn.Contains("ire") || searchIn == "all")
            {
                var recentRelations = await _relationRepository.GetRelationsInTimeRangeAsync(
                    startTime, DateTime.UtcNow, maxAdditionalResults);
                    
                foreach (var relation in recentRelations)
                {
                    if (existingIds.Contains(relation.Id))
                        continue;
                        
                    existingItems.Add(new RelevantItem
                    {
                        Id = relation.Id,
                        Type = "ire",
                        Score = 0.3,
                        Description = $"Relação: {relation.RelationType}"
                    });
                    
                    existingIds.Add(relation.Id);
                    
                    if (existingItems.Count >= maxAdditionalResults)
                        return;
                }
            }
            
            // Buscar fluxos recentes
            if (searchIn.Contains("e") || searchIn == "all")
            {
                var recentFlows = await _flowRepository.GetFlowsInTimeRangeAsync(
                    startTime, DateTime.UtcNow, maxAdditionalResults);
                    
                foreach (var flow in recentFlows)
                {
                    if (existingIds.Contains(flow.Id))
                        continue;
                        
                    existingItems.Add(new RelevantItem
                    {
                        Id = flow.Id,
                        Type = "e",
                        Score = 0.3,
                        Description = $"Fluxo: {flow.Name}"
                    });
                    
                    existingIds.Add(flow.Id);
                    
                    if (existingItems.Count >= maxAdditionalResults)
                        return;
                }
            }
        }
        
        /// <summary>
        /// Normaliza os tipos de busca
        /// </summary>
        private string NormalizeSearchTypes(string searchIn)
        {
            if (string.IsNullOrWhiteSpace(searchIn))
                return "all";
                
            searchIn = searchIn.ToLowerInvariant();
            
            if (searchIn == "all" || searchIn == "*")
                return "all";
                
            var validTypes = new[] { "aje", "ire", "e" };
            var requestedTypes = searchIn.Split(',')
                .Select(t => t.Trim())
                .Where(t => validTypes.Contains(t));
                
            return requestedTypes.Any() ? string.Join(",", requestedTypes) : "all";
        }
        
        /// <summary>
        /// Formata uma duração de tempo de forma amigável
        /// </summary>
        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
                return $"{duration.Days} dias, {duration.Hours} horas";
                
            if (duration.TotalHours >= 1)
                return $"{duration.Hours} horas, {duration.Minutes} minutos";
                
            if (duration.TotalMinutes >= 1)
                return $"{duration.Minutes} minutos, {duration.Seconds} segundos";
                
            return $"{duration.Seconds} segundos";
        }
        
        /// <summary>
        /// Classe privada que representa um item relevante encontrado
        /// </summary>
        private class RelevantItem
        {
            public string Id { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public double Score { get; set; }
            public string Description { get; set; } = string.Empty;
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
