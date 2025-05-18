using Microsoft.MCP;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;
using EemCore.Data.Repositories;
using EemCore.Models;
using EemCore.Services;
using System.Text;
using EemCore.Configuration;
using Microsoft.Extensions.Options;

namespace EemCore.Agents
{
    /// <summary>
    /// Agente responsável por capturar e gerenciar eventos de atividade
    /// </summary>
    [McpToolType]
    public class CaptureAgent
    {
        private readonly IActivityRepository _activityRepository;
        private readonly IdeIntegrationService _ideIntegrationService;
        private readonly EemOptions _options;
        private readonly ILogger<CaptureAgent> _logger;
        
        public CaptureAgent(
            IActivityRepository activityRepository,
            IdeIntegrationService ideIntegrationService,
            IOptions<EemOptions> options,
            ILogger<CaptureAgent> logger)
        {
            _activityRepository = activityRepository;
            _ideIntegrationService = ideIntegrationService;
            _options = options.Value;
            _logger = logger;
        }
        
        /// <summary>
        /// Método principal para captura de atividades do sistema.
        /// Este método seria acionado por um Azure Functions Timer Trigger.
        /// </summary>
        [McpTool("CaptureTick")]
        [Description("Executes a capture tick to record user activities")]
        public async Task<string> CaptureTickAsync(
            [Description("Optional session ID to use for this capture")]
            string? sessionId = null)
        {
            try
            {
                _logger.LogInformation("Iniciando tick de captura de atividades, SessionId: {SessionId}", 
                    sessionId ?? "novo");
                
                // Gerar ID de sessão se não fornecido
                string activeSessionId = sessionId ?? Guid.NewGuid().ToString();
                
                // Capturar atividades de diferentes fontes
                var ideActivities = await CaptureIdeActivitiesAsync(activeSessionId);
                var aiActivities = await CaptureAiInteractionsAsync(activeSessionId);
                var webActivities = await CaptureWebActivitiesAsync(activeSessionId);
                
                // Consolidar todas as atividades
                var allActivities = new List<ActivityJournalEvent>();
                allActivities.AddRange(ideActivities);
                allActivities.AddRange(aiActivities);
                allActivities.AddRange(webActivities);
                
                // Salvar atividades capturadas
                int savedCount = 0;
                foreach (var activity in allActivities)
                {
                    await _activityRepository.SaveActivityEventAsync(activity);
                    savedCount++;
                }
                
                var summary = new StringBuilder();
                summary.AppendLine($"Captura concluída para sessão: {activeSessionId}");
                summary.AppendLine($"- Atividades IDE: {ideActivities.Count}");
                summary.AppendLine($"- Interações AI: {aiActivities.Count}");
                summary.AppendLine($"- Atividades Web: {webActivities.Count}");
                summary.AppendLine($"Total salvo: {savedCount} atividades");
                
                _logger.LogInformation("Captura concluída, {Count} atividades salvas", savedCount);
                
                return summary.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante captura de atividades");
                return $"Erro durante captura de atividades: {ex.Message}";
            }
        }

        /// <summary>
        /// Captura uma atividade específica do usuário (acionada manualmente)
        /// </summary>
        [McpTool("CaptureManualActivity")]
        [Description("Explicitly captures a specific user activity")]
        public async Task<string> CaptureManualActivityAsync(
            [Description("Content of the activity to capture")]
            string content,
            
            [Description("Type of activity, e.g., 'code_edit', 'search', 'ai_interaction'")]
            string activityType,
            
            [Description("Source of the activity, e.g., 'Visual Studio', 'Browser', 'ChatGPT'")]
            string source,
            
            [Description("Optional session ID to associate with the activity")]
            string? sessionId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    return "Erro: O conteúdo da atividade não pode estar vazio.";
                }
                
                // Criar evento de atividade
                var activityEvent = new ActivityJournalEvent
                {
                    ActivityType = activityType,
                    Content = content,
                    Source = source,
                    SessionId = sessionId ?? Guid.NewGuid().ToString()
                };
                
                // Salvar o evento
                string eventId = await _activityRepository.SaveActivityEventAsync(activityEvent);
                
                _logger.LogInformation(
                    "Atividade capturada manualmente: {Id}, Tipo: {Type}, Fonte: {Source}", 
                    eventId, activityType, source);
                
                return $"Atividade capturada com sucesso. ID: {eventId}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao capturar atividade manual");
                return $"Erro ao capturar atividade: {ex.Message}";
            }
        }

        /// <summary>
        /// Limpa eventos de atividade antigos
        /// </summary>
        [McpTool("PurgeOldActivities")]
        [Description("Purges activity events older than the specified retention period")]
        public async Task<string> PurgeOldActivitiesAsync(
            [Description("Retention period in days")]
            int retentionDays = 90)
        {
            try
            {
                if (retentionDays < 1)
                {
                    return "Erro: O período de retenção deve ser de pelo menos 1 dia.";
                }
                
                var retentionPeriod = TimeSpan.FromDays(retentionDays);
                
                _logger.LogInformation("Iniciando limpeza de eventos antigos (retenção: {Days} dias)", 
                    retentionDays);
                
                int purgedCount = await _activityRepository.PurgeOldEventsAsync(retentionPeriod);
                
                _logger.LogInformation("Limpeza concluída, {Count} eventos antigos excluídos", purgedCount);
                
                return $"Limpeza concluída. {purgedCount} eventos antigos foram excluídos.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante limpeza de eventos antigos");
                return $"Erro durante limpeza de eventos antigos: {ex.Message}";
            }
        }

        // Métodos privados para captura de diferentes fontes

        /// <summary>
        /// Captura atividades da IDE (Visual Studio, VS Code)
        /// </summary>
        private async Task<List<ActivityJournalEvent>> CaptureIdeActivitiesAsync(string sessionId)
        {
            _logger.LogInformation("Capturando atividades de IDE para sessão {SessionId}", sessionId);
            
            // Em uma implementação real, integraríamos com:
            // - VS Code Extension API
            // - Visual Studio extensibility
            // - JetBrains platform, etc.
            
            // Simulação para exemplo - em produção, estas seriam atividades reais capturadas
            var activities = new List<ActivityJournalEvent>();
            
            // Simular algumas atividades de edição de código
            var codeEditActivity = await _ideIntegrationService.CaptureIdeActivityAsync(
                "IDE",
                "code_edit",
                "public class Program { /* código editado */ }",
                sessionId,
                new Dictionary<string, object> { 
                    ["project"] = "EemServer",
                    ["file"] = "Program.cs",
                    ["language"] = "csharp"
                }
            );
            activities.Add(new ActivityJournalEvent {
                Id = codeEditActivity,
                ActivityType = "code_edit",
                Content = "public class Program { /* código editado */ }",
                Source = "IDE",
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object> { 
                    ["project"] = "EemServer",
                    ["file"] = "Program.cs",
                    ["language"] = "csharp"
                }
            });
            
            // Simular navegação entre arquivos
            var navigationActivity = await _ideIntegrationService.CaptureIdeActivityAsync(
                "IDE",
                "navigation",
                "EemCore/Data/Repositories/ActivityRepository.cs",
                sessionId,
                new Dictionary<string, object> { 
                    ["project"] = "EemCore", 
                    ["action"] = "file_open" 
                }
            );
            activities.Add(new ActivityJournalEvent {
                Id = navigationActivity,
                ActivityType = "navigation",
                Content = "EemCore/Data/Repositories/ActivityRepository.cs",
                Source = "IDE",
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object> { 
                    ["project"] = "EemCore", 
                    ["action"] = "file_open" 
                }
            });
            
            return activities;
        }

        /// <summary>
        /// Captura interações com assistentes de IA
        /// </summary>
        private async Task<List<ActivityJournalEvent>> CaptureAiInteractionsAsync(string sessionId)
        {
            _logger.LogInformation("Capturando interações com IA para sessão {SessionId}", sessionId);
            
            // Em uma implementação real, integraríamos com:
            // - Visual Studio IntelliCode
            // - GitHub Copilot
            // - ChatGPT, etc.
            
            // Simulação para exemplo
            var activities = new List<ActivityJournalEvent>();
            
            // Simular interação com Copilot
            var aiInteractionActivity = await _ideIntegrationService.CaptureIdeActivityAsync(
                "GitHub Copilot",
                "ai_interaction",
                "```csharp\npublic async Task<string> SaveActivityAsync(ActivityEvent activity)\n{\n    // implementação\n}\n```",
                sessionId,
                new Dictionary<string, object> {
                    ["prompt"] = "Implementar método para salvar atividade"
                }
            );
            activities.Add(new ActivityJournalEvent {
                Id = aiInteractionActivity,
                ActivityType = "ai_interaction",
                Content = "```csharp\npublic async Task<string> SaveActivityAsync(ActivityEvent activity)\n{\n    // implementação\n}\n```",
                Source = "GitHub Copilot",
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object> {
                    ["prompt"] = "Implementar método para salvar atividade"
                }
            });
            
            return activities;
        }

        /// <summary>
        /// Captura navegação web e consultas relacionadas
        /// </summary>
        private async Task<List<ActivityJournalEvent>> CaptureWebActivitiesAsync(string sessionId)
        {
            _logger.LogInformation("Capturando atividades web para sessão {SessionId}", sessionId);
            
            // Em uma implementação real, integraríamos com:
            // - Extensões de navegador
            // - Histórico de navegação
            // - API de busca, etc.
            
            // Simulação para exemplo
            var activities = new List<ActivityJournalEvent>();
            
            // Simular uma busca na web
            var webActivity = new ActivityJournalEvent
            {
                ActivityType = "web_search",
                Content = "Busca: 'Azure Cosmos DB repository pattern'",
                Source = "Browser",
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["url"] = "https://learn.microsoft.com/search?q=cosmos+db+repository+pattern",
                    ["browser"] = "Chrome"
                }
            };
            
            activities.Add(webActivity);
            
            return activities;
        }

        /// <summary>
        /// Captura um evento de atividade e salva no armazenamento
        /// </summary>
        [McpTool("CaptureActivity")]
        [Description("Captures a new activity event and stores it in the Εεm memory system")]
        public async Task<ActivityJournalEvent> CaptureActivityAsync(
            [Description("Type of activity (edit, navigation, query, etc)")]
            string activityType,
            
            [Description("Content of the activity")]
            string content,
            
            [Description("Source of the activity (IDE, AI Assistant, etc)")]
            string source,
            
            [Description("Session ID to associate with the activity")]
            string sessionId,
            
            [Description("Optional metadata as JSON string")]
            string? metadataJson = null)
        {
            _logger.LogInformation("Capturando atividade: {Type} da fonte {Source}", activityType, source);
            
            var activityEvent = new ActivityJournalEvent
            {
                ActivityType = activityType,
                Content = content,
                Source = source,
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow
            };
            
            // Processar metadados se fornecidos
            if (!string.IsNullOrWhiteSpace(metadataJson))
            {
                try
                {
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson);
                    if (metadata != null)
                    {
                        foreach (var item in metadata)
                        {
                            activityEvent.Metadata[item.Key] = item.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao processar metadados para atividade");
                }
            }
            
            // Salvar no repositório
            await _activityRepository.SaveActivityEventAsync(activityEvent);
            
            return activityEvent;
        }
        
        /// <summary>
        /// Obtém eventos de atividade recentes para uma sessão
        /// </summary>
        [McpTool("GetRecentActivities")]
        [Description("Gets recent activity events for a specific session")]
        public async Task<IEnumerable<ActivityJournalEvent>> GetRecentActivitiesAsync(
            [Description("Session ID to get activities for")]
            string sessionId,
            
            [Description("Maximum number of activities to return")]
            int maxCount = 20)
        {
            _logger.LogInformation("Obtendo atividades recentes para sessão: {SessionId}, máximo: {MaxCount}", sessionId, maxCount);
            
            var activities = await _activityRepository.GetActivityEventsForSessionAsync(sessionId);
            
            return activities
                .OrderByDescending(a => a.Timestamp)
                .Take(maxCount);
        }
        
        /// <summary>
        /// Busca eventos de atividade por consulta textual
        /// </summary>
        [McpTool("SearchActivities")]
        [Description("Searches for activity events based on text query")]
        public async Task<IEnumerable<ActivityJournalEvent>> SearchActivitiesAsync(
            [Description("Text query to search for")]
            string query,
            
            [Description("Maximum number of results to return")]
            int maxResults = 10)
        {
            _logger.LogInformation("Buscando atividades com consulta: {Query}, máximo: {MaxResults}", query, maxResults);
            
            return await _activityRepository.SearchActivityEventsAsync(query, maxResults);
        }
        
        /// <summary>
        /// Obtém eventos de atividade dentro de uma janela de tempo
        /// </summary>
        [McpTool("GetActivitiesByTimeRange")]
        [Description("Gets activity events within a specific time range")]
        public async Task<IEnumerable<ActivityJournalEvent>> GetActivitiesByTimeRangeAsync(
            [Description("Start time in ISO 8601 format")]
            string startTimeIso,
            
            [Description("End time in ISO 8601 format")]
            string endTimeIso,
            
            [Description("Maximum number of events to return")]
            int maxEvents = 100)
        {
            if (!DateTime.TryParse(startTimeIso, out var startTime))
            {
                _logger.LogWarning("Formato inválido para startTime: {StartTime}", startTimeIso);
                throw new ArgumentException("Start time must be in ISO 8601 format", nameof(startTimeIso));
            }
            
            if (!DateTime.TryParse(endTimeIso, out var endTime))
            {
                _logger.LogWarning("Formato inválido para endTime: {EndTime}", endTimeIso);
                throw new ArgumentException("End time must be in ISO 8601 format", nameof(endTimeIso));
            }
            
            _logger.LogInformation("Obtendo atividades no intervalo: {Start} a {End}, máximo: {MaxEvents}",
                startTime, endTime, maxEvents);
                
            return await _activityRepository.GetActivityEventsInTimeRangeAsync(
                startTime, endTime, maxEvents);
        }
        
        /// <summary>
        /// Exclui eventos antigos com base no período de retenção configurado
        /// </summary>
        [McpTool("PurgeOldEvents")]
        [Description("Deletes old events based on the configured retention period")]
        public async Task<int> PurgeOldEventsAsync(
            [Description("Optional override for retention period in days")]
            int? retentionDaysOverride = null)
        {
            int retentionDays = retentionDaysOverride ?? _options.RetentionPeriodDays;
            
            _logger.LogInformation("Excluindo eventos mais antigos que {Days} dias", retentionDays);
            
            var retentionPeriod = TimeSpan.FromDays(retentionDays);
            return await _activityRepository.PurgeOldEventsAsync(retentionPeriod);
        }
    }
}
