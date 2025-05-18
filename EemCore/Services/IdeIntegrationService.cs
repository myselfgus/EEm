using EemCore.Configuration;
using EemCore.Data.Repositories;
using EemCore.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EemCore.Services
{
    /// <summary>
    /// Servi�o para integra��o com IDEs populares (Visual Studio, VS Code, JetBrains)
    /// </summary>
    public class IdeIntegrationService
    {
        private readonly IActivityRepository _activityRepository;
        private readonly ILogger<IdeIntegrationService> _logger;
        private readonly EemOptions _options;
        
        public IdeIntegrationService(
            IActivityRepository activityRepository,
            IOptions<EemOptions> options,
            ILogger<IdeIntegrationService> logger)
        {
            _activityRepository = activityRepository;
            _options = options.Value;
            _logger = logger;
        }
        
        /// <summary>
        /// Captura atividade de edi��o de c�digo de uma IDE
        /// </summary>
        public async Task<ActivityJournalEvent> CaptureCodeEditActivity(
            string filePath,
            string content,
            string languageId,
            string sessionId,
            Dictionary<string, string>? metadata = null)
        {
            // Filtrar tipos de arquivo baseado nas configura��es de privacidade
            if (_options.PrivacySettings.FilterFileTypes.Length > 0)
            {
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                if (!_options.PrivacySettings.FilterFileTypes.Contains(extension))
                {
                    _logger.LogInformation("Arquivo ignorado devido a filtragem de tipo: {FilePath}", filePath);
                    
                    // Ainda criar um evento, mas com conte�do filtrado
                    return new ActivityJournalEvent
                    {
                        ActivityType = "code_edit_filtered",
                        Source = "IDE",
                        Content = $"Edi��o em arquivo {extension} (filtrado por pol�tica de privacidade)",
                        SessionId = sessionId,
                        AssociatedFile = filePath,
                        Metadata = metadata?.ToDictionary(kv => kv.Key, kv => (object)kv.Value) 
                                   ?? new Dictionary<string, object>()
                    };
                }
            }
            
            // Verificar e filtrar informa��es sens�veis do conte�do, se necess�rio
            string safeContent = content;
            if (_options.PrivacySettings.ExcludeSecrets)
            {
                safeContent = FilterSensitiveContent(safeContent);
            }
            
            var journalEvent = new ActivityJournalEvent
            {
                ActivityType = "code_edit",
                Source = "IDE",
                Content = safeContent,
                SessionId = sessionId,
                AssociatedFile = filePath,
                Metadata = metadata?.ToDictionary(kv => kv.Key, kv => (object)kv.Value) 
                           ?? new Dictionary<string, object>()
            };
            
            // Adicionar metadados adicionais
            journalEvent.Metadata["language"] = languageId;
            journalEvent.Metadata["contentLength"] = content.Length.ToString();
            
            // Gerar hash de conte�do para detec��o de duplica��o
            journalEvent.GenerateContentHash();
            
            _logger.LogInformation("Capturada atividade de edi��o de c�digo: {FilePath}", filePath);
            
            return journalEvent;
        }
        
        /// <summary>
        /// Captura atividade de navega��o ou sele��o de arquivos na IDE
        /// </summary>
        public ActivityJournalEvent CaptureNavigationActivity(
            string filePath,
            string action,
            string sessionId,
            Dictionary<string, string>? metadata = null)
        {
            var journalEvent = new ActivityJournalEvent
            {
                ActivityType = "navigation",
                Source = "IDE",
                Content = $"{action}: {filePath}",
                SessionId = sessionId,
                AssociatedFile = filePath,
                Metadata = metadata?.ToDictionary(kv => kv.Key, kv => (object)kv.Value) 
                           ?? new Dictionary<string, object>()
            };
            
            // Adicionar metadados adicionais
            journalEvent.Metadata["action"] = action;
            journalEvent.Metadata["fileExtension"] = Path.GetExtension(filePath);
            
            _logger.LogInformation("Capturada atividade de navega��o: {Action} - {FilePath}", action, filePath);
            
            return journalEvent;
        }
        
        /// <summary>
        /// Captura intera��o com assistente AI
        /// </summary>
        public ActivityJournalEvent CaptureAiInteractionActivity(
            string query,
            string response,
            string assistantName,
            string sessionId,
            Dictionary<string, string>? metadata = null)
        {
            // Filtrar informa��es sens�veis, se necess�rio
            string safeQuery = query;
            string safeResponse = response;
            
            if (_options.PrivacySettings.ExcludeSecrets)
            {
                safeQuery = FilterSensitiveContent(safeQuery);
                safeResponse = FilterSensitiveContent(safeResponse);
            }
            
            var journalEvent = new ActivityJournalEvent
            {
                ActivityType = "ai_interaction",
                Source = assistantName,
                Content = $"Query: {safeQuery}\n\nResponse: {safeResponse}",
                SessionId = sessionId,
                Metadata = metadata?.ToDictionary(kv => kv.Key, kv => (object)kv.Value) 
                           ?? new Dictionary<string, object>()
            };
            
            // Adicionar metadados adicionais
            journalEvent.Metadata["assistantName"] = assistantName;
            journalEvent.Metadata["queryLength"] = query.Length.ToString();
            journalEvent.Metadata["responseLength"] = response.Length.ToString();
            
            _logger.LogInformation("Capturada intera��o com assistente AI: {AssistantName}", assistantName);
            
            return journalEvent;
        }
        
        /// <summary>
        /// M�todo para filtrar conte�do sens�vel (implementa��o b�sica)
        /// </summary>
        private string FilterSensitiveContent(string content)
        {
            // Implementa��o simples para exemplo - em produ��o, usaria t�cnicas mais robustas
            var patterns = new[]
            {
                // Padr�es de tokens/chaves
                @"['""]?[a-zA-Z0-9_-]{30,}['""]?",
                // Conex�es com banco de dados
                @"(Server|Data Source|Initial Catalog|User ID|Password)=[^;]+;",
                // URLs com credenciais
                @"https?://[^:]+:[^@]+@",
                // Chaves de API referenciadas
                @"api[_-]?key\s*[:=]\s*['""]?\w+['""]?"
            };
            
            string filtered = content;
            
            foreach (var pattern in patterns)
            {
                filtered = System.Text.RegularExpressions.Regex.Replace(
                    filtered,
                    pattern,
                    match => "[CONTE�DO SENS�VEL REMOVIDO]",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            }
            
            return filtered;
        }

        /// <summary>
        /// Captura um evento de atividade do IDE
        /// </summary>
        public async Task<string> CaptureIdeActivityAsync(
            string source,
            string activityType,
            string content,
            string sessionId,
            Dictionary<string, object>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Capturando atividade de IDE: {Source}, {Type}", source, activityType);
            
            var activityEvent = new ActivityJournalEvent
            {
                Source = source,
                ActivityType = activityType,
                Content = content,
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                Metadata = metadata ?? new Dictionary<string, object>()
            };
            
            return await _activityRepository.SaveActivityEventAsync(activityEvent, cancellationToken);
        }
        
        /// <summary>
        /// Registra um evento de c�digo modificado
        /// </summary>
        public async Task<string> RegisterCodeModificationAsync(
            string filePath, 
            string content, 
            int startLine, 
            int endLine, 
            string sessionId)
        {
            _logger.LogInformation(
                "Registrando modifica��o de c�digo em {FilePath} (linhas {Start}-{End})",
                filePath, startLine, endLine);
                
            // Em uma implementa��o real, isso registraria mudan�as reais no c�digo
            // via hooks de edi��o de IDE
            
            return Guid.NewGuid().ToString();
        }
        
        /// <summary>
        /// Registra uma navega��o entre arquivos
        /// </summary>
        public async Task<string> RegisterFileNavigationAsync(
            string fromFile,
            string toFile,
            string sessionId)
        {
            _logger.LogInformation(
                "Registrando navega��o de arquivo: {From} -> {To}",
                fromFile, toFile);
                
            // Em uma implementa��o real, isso registraria navega��o real entre arquivos
            // via hooks de navega��o de IDE
            
            return Guid.NewGuid().ToString();
        }
        
        /// <summary>
        /// Registra um evento de busca na IDE
        /// </summary>
        public async Task<string> RegisterSearchEventAsync(
            string searchQuery,
            string searchScope,
            string sessionId)
        {
            _logger.LogInformation(
                "Registrando busca na IDE: {Query} (escopo: {Scope})",
                searchQuery, searchScope);
                
            // Em uma implementa��o real, isso registraria buscas reais na IDE
            // via hooks de busca de IDE
            
            return Guid.NewGuid().ToString();
        }
        
        /// <summary>
        /// Sincroniza a atividade do integrador de IDE
        /// </summary>
        public async Task<bool> SyncIdeActivityAsync()
        {
            // Em uma implementa��o real, isso sincronizaria todas as atividades pendentes
            _logger.LogInformation("Sincronizando atividades de IDE");
            return true;
        }
        
        /// <summary>
        /// Obt�m atividades recentes para uma sess�o IDE
        /// </summary>
        public async Task<IEnumerable<ActivityJournalEvent>> GetRecentActivitiesAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Obtendo atividades recentes para sess�o: {SessionId}", sessionId);
            
            return await _activityRepository.GetActivityEventsForSessionAsync(sessionId, cancellationToken);
        }
        
        /// <summary>
        /// Busca atividades relacionadas a um contexto espec�fico
        /// </summary>
        public async Task<IEnumerable<ActivityJournalEvent>> SearchRelatedActivitiesAsync(
            string contextQuery,
            int maxResults = 10,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Buscando atividades relacionadas a: {Query}", contextQuery);
            
            return await _activityRepository.SearchActivityEventsAsync(contextQuery, maxResults, cancellationToken);
        }
        
        /// <summary>
        /// Exporta eventos de atividade para um formato compat�vel com a IDE
        /// </summary>
        public string ExportActivitiesForIde(IEnumerable<ActivityJournalEvent> activities, string format = "json")
        {
            _logger.LogInformation("Exportando atividades em formato: {Format}", format);
            
            // Implementa��o simplificada - real teria mais op��es de formato
            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                return System.Text.Json.JsonSerializer.Serialize(activities, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            else if (format.Equals("markdown", StringComparison.OrdinalIgnoreCase))
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("# Atividades Capturadas");
                sb.AppendLine();
                
                foreach (var activity in activities)
                {
                    sb.AppendLine($"## {activity.ActivityType} ({activity.Timestamp})");
                    sb.AppendLine($"**Fonte**: {activity.Source}");
                    sb.AppendLine($"**ID da Sess�o**: {activity.SessionId}");
                    sb.AppendLine();
                    sb.AppendLine("```");
                    sb.AppendLine(activity.Content);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
                
                return sb.ToString();
            }
            
            throw new ArgumentException($"Formato de exporta��o n�o suportado: {format}");
        }
    }
}