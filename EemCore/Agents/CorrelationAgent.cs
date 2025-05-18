using Microsoft.MCP;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.AzureAI;
using System.ComponentModel;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.AI.OpenAI;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using EemCore.Data.Repositories;
using EemCore.Models;
using EemCore.Services.Resilient;
using Microsoft.Extensions.Logging;
using System.Text;
using EemCore.Configuration;
using Microsoft.Extensions.Options;

namespace EemCore.Agents
{
    /// <summary>
    /// Implementação do Agente de Correlação para o sistema Εεm (Ευ-εnable-memory).
    /// 
    /// O Agente de Correlação é responsável por:
    /// 1. Identificar relações entre eventos de atividade
    /// 2. Criar eventos de relação interpretada (.ire)
    /// 3. Manter o grafo de relações atualizado
    /// 4. Fornecer interfaces para consulta de correlações
    /// </summary>
    [McpToolType]
    public class CorrelationAgent
    {
        private readonly IActivityRepository _activityRepository;
        private readonly IRelationRepository _relationRepository;
        private readonly ResilientOpenAIClient _openAIClient;
        private readonly ILogger<CorrelationAgent> _logger;
        private readonly EemOptions _options;
        
        private const double DefaultCorrelationThreshold = 0.5;
        
        public CorrelationAgent(
            IActivityRepository activityRepository,
            IRelationRepository relationRepository,
            ResilientOpenAIClient openAIClient,
            ILogger<CorrelationAgent> logger,
            IOptions<EemOptions> options)
        {
            _activityRepository = activityRepository;
            _relationRepository = relationRepository;
            _openAIClient = openAIClient;
            _logger = logger;
            _options = options.Value;
        }
        
        /// <summary>
        /// Detecta correlações entre eventos de atividade em uma sessão
        /// </summary>
        [McpTool("DetectCorrelations")]
        [Description("Detects correlations between activity events in a session")]
        public async Task<string> DetectCorrelationsAsync(
            [Description("Session ID to analyze for correlations")]
            string sessionId,
            
            [Description("Correlation threshold (0.0 to 1.0)")]
            double threshold = DefaultCorrelationThreshold,
            
            [Description("Maximum number of events to analyze")]
            int maxEvents = 50,
            
            [Description("Types of correlation to detect (comma-separated): temporal,causal,semantic,contextual,all")]
            string correlationTypes = "all")
        {
            try
            {
                _logger.LogInformation("Iniciando detecção de correlações para sessão {SessionId}", sessionId);
                
                // Normalizar tipos de correlação
                var types = NormalizeCorrelationTypes(correlationTypes);
                
                // Buscar atividades da sessão
                var activities = await _activityRepository.GetActivityEventsForSessionAsync(sessionId);
                
                // Limitar o número de eventos analisados
                var filteredActivities = activities
                    .OrderByDescending(a => a.Timestamp)
                    .Take(maxEvents)
                    .ToList();
                
                if (filteredActivities.Count == 0)
                {
                    return $"Nenhuma atividade encontrada para a sessão {sessionId}.";
                }
                
                _logger.LogInformation("Analisando {Count} atividades para correlações", filteredActivities.Count);
                
                // Detectar correlações
                var correlations = new List<InterpretedRelationEvent>();
                
                // Correlações temporais
                if (types.Contains("temporal") || types.Contains("all"))
                {
                    var temporalCorrelations = DetectTemporalCorrelations(filteredActivities, threshold);
                    correlations.AddRange(temporalCorrelations);
                }
                
                // Correlações causais
                if (types.Contains("causal") || types.Contains("all"))
                {
                    var causalCorrelations = await DetectCausalCorrelations(filteredActivities, threshold);
                    correlations.AddRange(causalCorrelations);
                }
                
                // Correlações semânticas
                if (types.Contains("semantic") || types.Contains("all"))
                {
                    var semanticCorrelations = await DetectSemanticCorrelations(filteredActivities, threshold);
                    correlations.AddRange(semanticCorrelations);
                }
                
                // Correlações contextuais
                if (types.Contains("contextual") || types.Contains("all"))
                {
                    var contextualCorrelations = await DetectContextualCorrelations(filteredActivities, threshold);
                    correlations.AddRange(contextualCorrelations);
                }
                
                // Salvar as correlações detectadas
                int savedCount = 0;
                foreach (var correlation in correlations)
                {
                    correlation.SessionId = sessionId;
                    await _relationRepository.SaveRelationEventAsync(correlation);
                    savedCount++;
                }
                
                _logger.LogInformation("Detectadas e salvas {Count} correlações para a sessão {SessionId}", 
                    savedCount, sessionId);
                
                return $"Detectadas {correlations.Count} correlações na sessão {sessionId}.\n" +
                       $"Tipos de correlação: {string.Join(", ", types)}.\n" +
                       $"Correlações salvas: {savedCount}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na detecção de correlações para a sessão {SessionId}", sessionId);
                return $"Erro na detecção de correlações: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Exibe correlações entre eventos de atividade
        /// </summary>
        [McpTool("ShowCorrelations")]
        [Description("Shows correlations between specific activity events")]
        public async Task<string> ShowCorrelationsAsync(
            [Description("Comma-separated list of activity event IDs to analyze")]
            string activityIds,
            
            [Description("Optional correlation types to include (comma-separated)")]
            string? correlationTypes = null)
        {
            try
            {
                // Validar entrada
                if (string.IsNullOrWhiteSpace(activityIds))
                {
                    return "Erro: A lista de IDs de atividade não pode estar vazia.";
                }
                
                var idList = activityIds.Split(',')
                    .Select(id => id.Trim())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToArray();
                
                if (idList.Length == 0)
                {
                    return "Erro: Nenhum ID de atividade válido fornecido.";
                }
                
                // Normalizar tipos de correlação (se fornecidos)
                var types = correlationTypes != null
                    ? NormalizeCorrelationTypes(correlationTypes)
                    : new[] { "all" };
                
                // Buscar correlações para os eventos
                var relations = await _relationRepository.GetRelationsForEventsAsync(idList);
                
                // Filtrar por tipo (se não for "all")
                if (!types.Contains("all"))
                {
                    relations = relations.Where(r => types.Contains(r.RelationType.ToLowerInvariant())).ToList();
                }
                
                if (!relations.Any())
                {
                    return $"Nenhuma correlação encontrada para os eventos especificados.";
                }
                
                // Formatar a saída
                var sb = new StringBuilder();
                sb.AppendLine($"# Correlações entre Eventos de Atividade");
                sb.AppendLine();
                sb.AppendLine($"Eventos analisados: {idList.Length}");
                sb.AppendLine($"Correlações encontradas: {relations.Count()}");
                sb.AppendLine();
                
                // Agrupar por tipo de correlação
                var groupedRelations = relations.GroupBy(r => r.RelationType);
                
                foreach (var group in groupedRelations)
                {
                    sb.AppendLine($"## Tipo: {group.Key}");
                    sb.AppendLine();
                    
                    foreach (var relation in group)
                    {
                        sb.AppendLine($"### Correlação {relation.Id}");
                        sb.AppendLine($"Força: {relation.RelationStrength:F2}");
                        sb.AppendLine($"Descrição: {relation.Description}");
                        
                        if (relation.Tags.Count > 0)
                        {
                            sb.AppendLine($"Tags: {string.Join(", ", relation.Tags)}");
                        }
                        
                        sb.AppendLine($"Eventos relacionados: {relation.RelatedEventIds.Count}");
                        sb.AppendLine();
                    }
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao exibir correlações para os eventos");
                return $"Erro ao exibir correlações: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Cria uma correlação manual entre eventos
        /// </summary>
        [McpTool("CreateManualCorrelation")]
        [Description("Creates a manual correlation between activity events")]
        public async Task<string> CreateManualCorrelationAsync(
            [Description("Comma-separated list of activity event IDs to correlate")]
            string activityIds,
            
            [Description("Type of correlation to create")]
            string correlationType,
            
            [Description("Description of the correlation")]
            string description,
            
            [Description("Correlation strength (0.0 to 1.0)")]
            double strength = 1.0,
            
            [Description("Optional comma-separated tags")]
            string? tags = null)
        {
            try
            {
                // Validar entrada
                if (string.IsNullOrWhiteSpace(activityIds))
                {
                    return "Erro: A lista de IDs de atividade não pode estar vazia.";
                }
                
                if (string.IsNullOrWhiteSpace(correlationType))
                {
                    return "Erro: O tipo de correlação não pode estar vazio.";
                }
                
                if (strength < 0 || strength > 1)
                {
                    return "Erro: A força da correlação deve estar entre 0.0 e 1.0.";
                }
                
                var idList = activityIds.Split(',')
                    .Select(id => id.Trim())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList();
                
                if (idList.Count < 2)
                {
                    return "Erro: São necessários pelo menos dois IDs de atividade para criar uma correlação.";
                }
                
                // Parseando tags, se houver
                List<string> tagsList = new List<string>();
                if (!string.IsNullOrWhiteSpace(tags))
                {
                    tagsList = tags.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();
                }
                
                // Criar o evento de relação
                var relationEvent = new InterpretedRelationEvent
                {
                    RelationType = correlationType,
                    Description = description,
                    RelatedEventIds = idList,
                    RelationStrength = strength,
                    Tags = tagsList
                };
                
                // Salvar a correlação
                string relationId = await _relationRepository.SaveRelationEventAsync(relationEvent);
                
                _logger.LogInformation("Correlação manual criada: {Id}, Tipo: {Type}, Eventos: {Count}",
                    relationId, correlationType, idList.Count);
                
                return $"Correlação manual criada com sucesso. ID: {relationId}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar correlação manual");
                return $"Erro ao criar correlação manual: {ex.Message}";
            }
        }
        
        // Métodos de detecção de correlações
        
        /// <summary>
        /// Detecta correlações temporais entre eventos
        /// </summary>
        private List<InterpretedRelationEvent> DetectTemporalCorrelations(
            List<ActivityJournalEvent> activities,
            double threshold)
        {
            var correlations = new List<InterpretedRelationEvent>();
            
            // Exemplo simplificado: eventos próximos temporalmente
            
            // Ordenar por timestamp
            var orderedActivities = activities.OrderBy(a => a.Timestamp).ToList();
            
            // Janela deslizante para encontrar eventos próximos
            TimeSpan window = TimeSpan.FromMinutes(5);
            
            for (int i = 0; i < orderedActivities.Count; i++)
            {
                var current = orderedActivities[i];
                var relatedEvents = new List<string> { current.Id };
                
                // Procurar eventos em uma janela próxima
                for (int j = i + 1; j < orderedActivities.Count; j++)
                {
                    var next = orderedActivities[j];
                    
                    if (next.Timestamp - current.Timestamp <= window)
                    {
                        relatedEvents.Add(next.Id);
                    }
                    else
                    {
                        break; // Eventos já estão ordenados, então podemos parar
                    }
                }
                
                // Se houver mais de um evento relacionado, criar uma correlação
                if (relatedEvents.Count > 1)
                {
                    // Calcular força com base na proximidade temporal
                    double strength = Math.Min(1.0, (double)relatedEvents.Count / 10.0);
                    
                    if (strength >= threshold)
                    {
                        var correlation = new InterpretedRelationEvent
                        {
                            RelationType = "temporal",
                            Description = $"Eventos temporalmente próximos dentro de uma janela de {window.TotalMinutes} minutos",
                            RelatedEventIds = relatedEvents,
                            RelationStrength = strength,
                            Tags = new List<string> { "auto-detected", "temporal-window" }
                        };
                        
                        correlations.Add(correlation);
                    }
                }
                
                // Saltar para evitar sobreposição excessiva de janelas
                if (relatedEvents.Count > 2)
                {
                    i += relatedEvents.Count / 2;
                }
            }
            
            return correlations;
        }
        
        /// <summary>
        /// Detecta correlações causais entre eventos
        /// </summary>
        private async Task<List<InterpretedRelationEvent>> DetectCausalCorrelations(
            List<ActivityJournalEvent> activities,
            double threshold)
        {
            // Implementação simplificada: 
            // Relações causais são difíceis de detectar automaticamente
            // Em uma implementação real, usaríamos modelos sofisticados de IA
            
            // Como exemplo, usaremos uma heurística simples: 
            // Eventos sequenciais do mesmo tipo ou fonte são potencialmente causais
            
            var correlations = new List<InterpretedRelationEvent>();
            
            // Ordenar por timestamp
            var orderedActivities = activities.OrderBy(a => a.Timestamp).ToList();
            
            for (int i = 0; i < orderedActivities.Count - 1; i++)
            {
                var current = orderedActivities[i];
                var next = orderedActivities[i + 1];
                
                // Se eventos forem do mesmo tipo ou fonte, podem ter relação causal
                if (current.ActivityType == next.ActivityType || current.Source == next.Source)
                {
                    // Calcular força baseada em heurísticas simples
                    double strength = 0.6; // Valor base
                    
                    // Ajustar força com base em tempo entre eventos
                    TimeSpan timeDiff = next.Timestamp - current.Timestamp;
                    if (timeDiff <= TimeSpan.FromSeconds(30))
                    {
                        strength += 0.3; // Eventos muito próximos têm maior probabilidade causal
                    }
                    else if (timeDiff <= TimeSpan.FromMinutes(5))
                    {
                        strength += 0.1;
                    }
                    
                    if (strength >= threshold)
                    {
                        var correlation = new InterpretedRelationEvent
                        {
                            RelationType = "causal",
                            Description = $"Possível relação causal entre eventos sequenciais de {current.ActivityType}",
                            RelatedEventIds = new List<string> { current.Id, next.Id },
                            RelationStrength = strength,
                            Tags = new List<string> { "auto-detected", "potential-causal" }
                        };
                        
                        correlations.Add(correlation);
                    }
                }
            }
            
            return correlations;
        }
        
        /// <summary>
        /// Detecta correlações semânticas entre eventos
        /// </summary>
        private async Task<List<InterpretedRelationEvent>> DetectSemanticCorrelations(
            List<ActivityJournalEvent> activities,
            double threshold)
        {
            // Em uma implementação real, usaríamos embeddings e comparações semânticas
            // Para este exemplo, usamos uma abordagem simplificada baseada em termos comuns
            
            var correlations = new List<InterpretedRelationEvent>();
            
            // Comparar cada par de eventos
            for (int i = 0; i < activities.Count; i++)
            {
                for (int j = i + 1; j < activities.Count; j++)
                {
                    var event1 = activities[i];
                    var event2 = activities[j];
                    
                    // Calcular similaridade de conteúdo (simplificada)
                    double similarity = CalculateSimpleSimilarity(event1.Content, event2.Content);
                    
                    if (similarity >= threshold)
                    {
                        var correlation = new InterpretedRelationEvent
                        {
                            RelationType = "semantic",
                            Description = $"Conteúdo semanticamente similar com {similarity:P0} de correspondência",
                            RelatedEventIds = new List<string> { event1.Id, event2.Id },
                            RelationStrength = similarity,
                            Tags = new List<string> { "auto-detected", "content-similarity" }
                        };
                        
                        correlations.Add(correlation);
                    }
                }
            }
            
            return correlations;
        }
        
        /// <summary>
        /// Detecta correlações contextuais entre eventos
        /// </summary>
        private async Task<List<InterpretedRelationEvent>> DetectContextualCorrelations(
            List<ActivityJournalEvent> activities,
            double threshold)
        {
            // Implementação simplificada:
            // Correlações contextuais baseadas em metadados compartilhados
            
            var correlations = new List<InterpretedRelationEvent>();
            
            // Agrupar eventos por metadados comuns
            var groupsByFile = activities
                .Where(a => a.Metadata.ContainsKey("file") || a.Metadata.ContainsKey("filepath"))
                .GroupBy(a => GetFileFromMetadata(a))
                .Where(g => g.Count() > 1);
            
            foreach (var group in groupsByFile)
            {
                if (group.Key != null)
                {
                    // Criar correlação para eventos relacionados ao mesmo arquivo
                    var eventIds = group.Select(a => a.Id).ToList();
                    var strength = Math.Min(1.0, 0.7 + ((double)eventIds.Count / 20.0));
                    
                    if (strength >= threshold)
                    {
                        var correlation = new InterpretedRelationEvent
                        {
                            RelationType = "contextual",
                            Description = $"Eventos relacionados ao mesmo arquivo: {group.Key}",
                            RelatedEventIds = eventIds,
                            RelationStrength = strength,
                            Tags = new List<string> { "auto-detected", "same-file" }
                        };
                        
                        correlations.Add(correlation);
                    }
                }
            }
            
            // Agrupar por outros metadados comuns
            foreach (var key in new[] { "project", "namespace", "class", "method" })
            {
                var groupsByKey = activities
                    .Where(a => a.Metadata.ContainsKey(key))
                    .GroupBy(a => a.Metadata[key]?.ToString())
                    .Where(g => g.Count() > 1);
                
                foreach (var group in groupsByKey)
                {
                    if (group.Key != null)
                    {
                        // Criar correlação para eventos com o mesmo metadado
                        var eventIds = group.Select(a => a.Id).ToList();
                        var strength = Math.Min(1.0, 0.6 + ((double)eventIds.Count / 30.0));
                        
                        if (strength >= threshold)
                        {
                            var correlation = new InterpretedRelationEvent
                            {
                                RelationType = "contextual",
                                Description = $"Eventos relacionados ao mesmo {key}: {group.Key}",
                                RelatedEventIds = eventIds,
                                RelationStrength = strength,
                                Tags = new List<string> { "auto-detected", $"same-{key}" }
                            };
                            
                            correlations.Add(correlation);
                        }
                    }
                }
            }
            
            return correlations;
        }
        
        // Métodos auxiliares
        
        /// <summary>
        /// Normaliza os tipos de correlação a serem detectados
        /// </summary>
        private string[] NormalizeCorrelationTypes(string correlationTypes)
        {
            if (string.IsNullOrWhiteSpace(correlationTypes))
                return new[] { "all" };
                
            var types = correlationTypes.ToLowerInvariant()
                .Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToArray();
                
            return types.Length == 0 ? new[] { "all" } : types;
        }
        
        /// <summary>
        /// Calcula similaridade simplificada entre dois textos
        /// </summary>
        private double CalculateSimpleSimilarity(string text1, string text2)
        {
            // Implementação simplificada baseada em palavras comuns
            
            if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
                return 0;
                
            // Normalizar e tokenizar
            var words1 = NormalizeAndTokenize(text1);
            var words2 = NormalizeAndTokenize(text2);
            
            if (words1.Count == 0 || words2.Count == 0)
                return 0;
                
            // Calcular interseção
            var commonWords = words1.Intersect(words2).Count();
            
            // Coeficiente de Jaccard: |A ∩ B| / |A ∪ B|
            var union = words1.Union(words2).Count();
            
            return (double)commonWords / union;
        }
        
        /// <summary>
        /// Normaliza e tokeniza um texto em palavras
        /// </summary>
        private HashSet<string> NormalizeAndTokenize(string text)
        {
            // Normalizar: lowercase, remover pontuação
            var normalized = new string(text.ToLowerInvariant()
                .Where(c => !char.IsPunctuation(c) || c == '_')
                .ToArray());
                
            // Tokenizar: dividir em palavras
            var words = normalized.Split(new[] { ' ', '\n', '\r', '\t' }, 
                StringSplitOptions.RemoveEmptyEntries);
                
            // Filtrar palavras muito curtas e criar conjunto
            return new HashSet<string>(words.Where(w => w.Length > 2));
        }
        
        /// <summary>
        /// Obtém o caminho do arquivo dos metadados
        /// </summary>
        private string? GetFileFromMetadata(ActivityJournalEvent activity)
        {
            if (activity.Metadata.TryGetValue("file", out var file))
                return file?.ToString();
                
            if (activity.Metadata.TryGetValue("filepath", out var filepath))
                return filepath?.ToString();
                
            if (activity.Metadata.TryGetValue("fileName", out var fileName))
                return fileName?.ToString();
                
            return activity.AssociatedFile;
        }
        
        /// <summary>
        /// Detecta correlações entre atividades em uma sessão
        /// </summary>
        [McpTool("DetectCorrelations")]
        [Description("Detects correlations between activities in a session")]
        public async Task<IEnumerable<InterpretedRelationEvent>> DetectCorrelationsAsync(
            [Description("Session ID to detect correlations for")]
            string sessionId,
            
            [Description("Time window in minutes to analyze")]
            int timeWindowMinutes = 60,
            
            [Description("Minimum strength threshold for correlations (0-1)")]
            double minStrengthThreshold = 0.5)
        {
            _logger.LogInformation("Detectando correlações para sessão {SessionId}, janela de {TimeWindow} minutos",
                sessionId, timeWindowMinutes);
                
            if (!_options.EnableCorrelationAnalysis)
            {
                _logger.LogWarning("Análise de correlação está desabilitada nas configurações");
                return new List<InterpretedRelationEvent>();
            }
            
            // Obter atividades recentes na janela de tempo
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddMinutes(-timeWindowMinutes);
            
            var activities = (await _activityRepository.GetActivityEventsInTimeRangeAsync(
                startTime, endTime, _options.MaxEventsPerActivity))
                .Where(a => a.SessionId == sessionId)
                .ToList();
                
            if (activities.Count < 2)
            {
                _logger.LogInformation("Insuficientes atividades para detectar correlações");
                return new List<InterpretedRelationEvent>();
            }
            
            // Detectar correlações (versão simplificada - a real usaria algoritmos mais sofisticados)
            var correlations = new List<InterpretedRelationEvent>();
            
            // Detecção baseada em tempo - atividades próximas temporalmente
            var temporalCorrelations = DetectTemporalCorrelations(activities, minStrengthThreshold);
            correlations.AddRange(temporalCorrelations);
            
            // Detecção baseada em conteúdo - atividades com conteúdo similar
            var contentCorrelations = DetectContentCorrelations(activities, minStrengthThreshold);
            correlations.AddRange(contentCorrelations);
            
            // Salvar correlações detectadas
            foreach (var correlation in correlations)
            {
                await _relationRepository.SaveRelationEventAsync(correlation);
            }
            
            _logger.LogInformation("Detectadas {Count} correlações", correlations.Count);
            
            return correlations;
        }
        
        /// <summary>
        /// Adiciona uma correlação manual entre atividades
        /// </summary>
        [McpTool("AddManualCorrelation")]
        [Description("Adds a manual correlation between activities")]
        public async Task<InterpretedRelationEvent> AddManualCorrelationAsync(
            [Description("Type of relation (causal, semantic, temporal, etc)")]
            string relationType,
            
            [Description("Comma-separated list of activity IDs to correlate")]
            string activityIds,
            
            [Description("Description of the relation")]
            string description,
            
            [Description("Strength of the relation (0-1)")]
            double strength = 0.9,
            
            [Description("Optional comma-separated tags")]
            string? tags = null)
        {
            var activityIdList = activityIds.Split(',')
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();
                
            if (activityIdList.Count < 2)
            {
                throw new ArgumentException("At least two activity IDs must be provided", nameof(activityIds));
            }
            
            _logger.LogInformation("Adicionando correlação manual de tipo {Type} entre {Count} atividades",
                relationType, activityIdList.Count);
                
            var relation = new InterpretedRelationEvent
            {
                RelationType = relationType,
                Description = description,
                RelationStrength = Math.Clamp(strength, 0, 1),
                RelatedEventIds = activityIdList,
                Timestamp = DateTime.UtcNow
            };
            
            // Adicionar tags se fornecidas
            if (!string.IsNullOrWhiteSpace(tags))
            {
                relation.Tags = tags
                    .Split(',')
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();
            }
            
            // Salvar a relação
            await _relationRepository.SaveRelationEventAsync(relation);
            
            return relation;
        }
        
        /// <summary>
        /// Obtém correlações para atividades específicas
        /// </summary>
        [McpTool("GetCorrelationsForActivities")]
        [Description("Gets correlations for specific activities")]
        public async Task<IEnumerable<InterpretedRelationEvent>> GetCorrelationsForActivitiesAsync(
            [Description("Comma-separated list of activity IDs")]
            string activityIds,
            
            [Description("Optional relation type filter")]
            string? relationType = null)
        {
            var activityIdList = activityIds.Split(',')
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();
                
            if (activityIdList.Count == 0)
            {
                return new List<InterpretedRelationEvent>();
            }
            
            _logger.LogInformation("Obtendo correlações para {Count} atividades, tipo: {Type}",
                activityIdList.Count, relationType ?? "todos");
                
            var relations = await _relationRepository.GetRelationsForEventsAsync(activityIdList);
            
            // Filtrar por tipo se especificado
            if (!string.IsNullOrWhiteSpace(relationType))
            {
                relations = relations.Where(r => 
                    r.RelationType.Equals(relationType, StringComparison.OrdinalIgnoreCase));
            }
            
            return relations;
        }
        
        /// <summary>
        /// Busca correlações por consulta textual
        /// </summary>
        [McpTool("SearchCorrelations")]
        [Description("Searches for correlations based on text query")]
        public async Task<IEnumerable<InterpretedRelationEvent>> SearchCorrelationsAsync(
            [Description("Text query to search for")]
            string query,
            
            [Description("Maximum number of results to return")]
            int maxResults = 10)
        {
            _logger.LogInformation("Buscando correlações com consulta: {Query}", query);
            
            return await _relationRepository.SearchRelationsAsync(query, maxResults);
        }
        
        /// <summary>
        /// Detecta correlações temporais entre atividades
        /// </summary>
        private IEnumerable<InterpretedRelationEvent> DetectTemporalCorrelations(
            List<ActivityJournalEvent> activities, double minStrengthThreshold)
        {
            var correlations = new List<InterpretedRelationEvent>();
            
            // Ordenar atividades por timestamp
            var orderedActivities = activities
                .OrderBy(a => a.Timestamp)
                .ToList();
                
            // Janela deslizante para encontrar atividades próximas no tempo
            const int maxTimeWindowSeconds = 300; // 5 minutos
            
            for (int i = 0; i < orderedActivities.Count - 1; i++)
            {
                var current = orderedActivities[i];
                var next = orderedActivities[i + 1];
                
                // Calcular diferença de tempo
                var timeDiff = (next.Timestamp - current.Timestamp).TotalSeconds;
                
                if (timeDiff <= maxTimeWindowSeconds)
                {
                    // Calcular força da correlação baseada na proximidade temporal
                    // Quanto menor a diferença, mais forte a correlação
                    double strength = 1.0 - (timeDiff / maxTimeWindowSeconds);
                    
                    if (strength >= minStrengthThreshold)
                    {
                        correlations.Add(new InterpretedRelationEvent
                        {
                            RelationType = "temporal",
                            Description = $"Atividades sequenciais com {timeDiff:F1} segundos de intervalo",
                            RelatedEventIds = new List<string> { current.Id, next.Id },
                            RelationStrength = strength,
                            Timestamp = DateTime.UtcNow,
                            Tags = new List<string> { "auto-detected", "temporal" }
                        });
                    }
                }
            }
            
            return correlations;
        }
        
        /// <summary>
        /// Detecta correlações baseadas em conteúdo entre atividades
        /// </summary>
        private IEnumerable<InterpretedRelationEvent> DetectContentCorrelations(
            List<ActivityJournalEvent> activities, double minStrengthThreshold)
        {
            var correlations = new List<InterpretedRelationEvent>();
            
            // Esta é uma versão simplificada - a real usaria embeddings e similaridade semântica
            // Busca simples por ocorrências de termos comuns
            
            // Extrair termos significativos de cada atividade
            var activityTerms = new Dictionary<string, HashSet<string>>();
            
            foreach (var activity in activities)
            {
                if (string.IsNullOrWhiteSpace(activity.Content))
                    continue;
                    
                // Extrair termos simples - na versão real usaria NLP mais avançado
                var terms = ExtractSignificantTerms(activity.Content);
                activityTerms[activity.Id] = terms;
            }
            
            // Comparar pares de atividades
            for (int i = 0; i < activities.Count; i++)
            {
                for (int j = i + 1; j < activities.Count; j++)
                {
                    var activityA = activities[i];
                    var activityB = activities[j];
                    
                    // Se alguma atividade não tem termos, pular
                    if (!activityTerms.ContainsKey(activityA.Id) || !activityTerms.ContainsKey(activityB.Id))
                        continue;
                        
                    var termsA = activityTerms[activityA.Id];
                    var termsB = activityTerms[activityB.Id];
                    
                    // Calcular intersection e união
                    var intersection = termsA.Intersect(termsB).Count();
                    var union = termsA.Union(termsB).Count();
                    
                    if (union == 0)
                        continue;
                        
                    // Calcular coeficiente de Jaccard para similaridade
                    double similarity = (double)intersection / union;
                    
                    if (similarity >= minStrengthThreshold)
                    {
                        correlations.Add(new InterpretedRelationEvent
                        {
                            RelationType = "semantic",
                            Description = $"Atividades com conteúdo similar ({intersection} termos em comum)",
                            RelatedEventIds = new List<string> { activityA.Id, activityB.Id },
                            RelationStrength = similarity,
                            Timestamp = DateTime.UtcNow,
                            Tags = new List<string> { "auto-detected", "semantic" }
                        });
                    }
                }
            }
            
            return correlations;
        }
        
        /// <summary>
        /// Extrai termos significativos de um texto
        /// </summary>
        private HashSet<string> ExtractSignificantTerms(string text)
        {
            // Versão simplificada - na real usaria extração de entidades e NLP
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Normalizar e dividir em termos
            var normalizedText = new string(text.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
            var terms = normalizedText.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Filtrar termos curtos e palavras comuns
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "a", "an", "the", "and", "or", "but", "if", "then", "else", "when",
                "at", "from", "by", "with", "about", "against", "between", "into",
                "through", "during", "before", "after", "above", "below", "to", "of",
                "in", "on", "uma", "um", "o", "a", "os", "as", "de", "da", "do",
                "que", "e", "é", "para", "com", "por", "em", "no", "na"
            };
            
            foreach (var term in terms)
            {
                // Ignorar termos muito curtos e stopwords
                if (term.Length >= 3 && !stopWords.Contains(term))
                {
                    result.Add(term);
                }
            }
            
            return result;
        }
    }

    /// <summary>
    /// Representa dados de uma atividade capturada
    /// </summary>
    public class ActivityData
    {
        public string Id { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string ActivityType { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Representa entidades extraídas de uma atividade
    /// </summary>
    public class EntityExtraction
    {
        public string ActivityId { get; set; } = string.Empty;
        public List<Entity> Entities { get; set; } = new();
    }

    /// <summary>
    /// Representa uma entidade extraída
    /// </summary>
    public class Entity
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public float Relevance { get; set; }
    }

    /// <summary>
    /// Representa uma correlação entre atividades ou entidades
    /// </summary>
    public class ActivityCorrelation
    {
        public string EntityA { get; set; } = string.Empty;
        public string EntityB { get; set; } = string.Empty;
        public float Similarity { get; set; }
        public string CorrelationType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
