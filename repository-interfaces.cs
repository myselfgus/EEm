using EemCore.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EemCore.Data
{
    /// <summary>
    /// Interface para repositório de eventos de atividade
    /// </summary>
    public interface IActivityRepository
    {
        /// <summary>
        /// Salva um novo evento de atividade no armazenamento
        /// </summary>
        /// <param name="activityEvent">Evento de atividade a ser salvo</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>O ID do evento salvo</returns>
        Task<string> SaveActivityEventAsync(ActivityJournalEvent activityEvent, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obtém eventos de atividade para uma sessão específica
        /// </summary>
        /// <param name="sessionId">ID da sessão</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Lista de eventos de atividade</returns>
        Task<IEnumerable<ActivityJournalEvent>> GetActivityEventsForSessionAsync(string sessionId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obtém eventos de atividade dentro de uma janela de tempo específica
        /// </summary>
        /// <param name="startTime">Hora de início da janela</param>
        /// <param name="endTime">Hora de fim da janela</param>
        /// <param name="maxEvents">Número máximo de eventos a retornar</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Lista de eventos de atividade</returns>
        Task<IEnumerable<ActivityJournalEvent>> GetActivityEventsInTimeRangeAsync(
            DateTime startTime, 
            DateTime endTime, 
            int maxEvents = 1000,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Busca eventos de atividade baseado em uma consulta textual
        /// </summary>
        /// <param name="searchQuery">Texto para busca</param>
        /// <param name="maxResults">Número máximo de resultados</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Lista de eventos de atividade relevantes</returns>
        Task<IEnumerable<ActivityJournalEvent>> SearchActivityEventsAsync(
            string searchQuery, 
            int maxResults = 10,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Exclui eventos mais antigos que um período de retenção
        /// </summary>
        /// <param name="retentionPeriod">Período de retenção</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Número de eventos excluídos</returns>
        Task<int> PurgeOldEventsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Interface para repositório de relações interpretadas
    /// </summary>
    public interface IRelationRepository
    {
        /// <summary>
        /// Salva um novo evento de relação no armazenamento
        /// </summary>
        /// <param name="relationEvent">Evento de relação a ser salvo</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>O ID da relação salva</returns>
        Task<string> SaveRelationEventAsync(InterpretedRelationEvent relationEvent, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obtém relações para eventos de atividade específicos
        /// </summary>
        /// <param name="activityEventIds">IDs dos eventos de atividade</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Lista de relações interpretadas</returns>
        Task<IEnumerable<InterpretedRelationEvent>> GetRelationsForEventsAsync(
            IEnumerable<string> activityEventIds, 
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Obtém relações dentro de uma janela de tempo específica
        /// </summary>
        /// <param name="startTime">Hora de início da janela</param>
        /// <param name="endTime">Hora de fim da janela</param>
        /// <param name="maxRelations">Número máximo de relações a retornar</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Lista de relações interpretadas</returns>
        Task<IEnumerable<InterpretedRelationEvent>> GetRelationsInTimeRangeAsync(
            DateTime startTime, 
            DateTime endTime, 
            int maxRelations = 1000,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Busca relações baseado em uma consulta textual
        /// </summary>
        /// <param name="searchQuery">Texto para busca</param>
        /// <param name="maxResults">Número máximo de resultados</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Lista de relações interpretadas relevantes</returns>
        Task<IEnumerable<InterpretedRelationEvent>> SearchRelationsAsync(
            string searchQuery, 
            int maxResults = 10,
            CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Interface para repositório de fluxos eulerianos
    /// </summary>
    public interface IEulerianFlowRepository
    {
        /// <summary>
        /// Salva um novo fluxo euleriano no armazenamento
        /// </summary>
        /// <param name="flow">Fluxo euleriano a ser salvo</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>O ID do fluxo salvo</returns>
        Task<string> SaveFlowAsync(EulerianFlow flow, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obtém fluxos eulerianos dentro de uma janela de tempo específica
        /// </summary>
        /// <param name="startTime">Hora de início da janela</param>
        /// <param name="endTime">Hora de fim da janela</param>
        /// <param name="maxFlows">Número máximo de fluxos a retornar</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Lista de fluxos eulerianos</returns>
        Task<IEnumerable<EulerianFlow>> GetFlowsInTimeRangeAsync(
            DateTime startTime, 
            DateTime endTime, 
            int maxFlows = 100,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Obtém um fluxo euleriano específico pelo ID
        /// </summary>
        /// <param name="flowId">ID do fluxo</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>O fluxo euleriano, se encontrado</returns>
        Task<EulerianFlow?> GetFlowByIdAsync(string flowId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Busca fluxos eulerianos baseado em uma consulta textual
        /// </summary>
        /// <param name="searchQuery">Texto para busca</param>
        /// <param name="maxResults">Número máximo de resultados</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Lista de fluxos eulerianos relevantes</returns>
        Task<IEnumerable<EulerianFlow>> SearchFlowsAsync(
            string searchQuery, 
            int maxResults = 10,
            CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Interface para repositório de scripts GenAIScript
    /// </summary>
    public interface IScriptRepository
    {
        /// <summary>
        /// Salva um novo script no armazenamento
        /// </summary>
        /// <param name="script">Informações do script</param>
        /// <param name="content">Conteúdo do script</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>O ID do script salvo</returns>
        Task<string> SaveScriptAsync(ScriptInfo script, string content, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obtém um script pelo nome
        /// </summary>
        /// <param name="scriptName">Nome do script</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Informações e conteúdo do script, se encontrado</returns>
        Task<(ScriptInfo Info, string Content)?> GetScriptByNameAsync(string scriptName, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Lista todos os scripts disponíveis
        /// </summary>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Lista de informações de scripts</returns>
        Task<IEnumerable<ScriptInfo>> ListScriptsAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Exclui um script específico
        /// </summary>
        /// <param name="scriptId">ID do script</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Verdadeiro se o script foi excluído com sucesso</returns>
        Task<bool> DeleteScriptAsync(string scriptId, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Classe base abstrata para repositórios que usam Azure Blob Storage
    /// </summary>
    public abstract class BaseBlobRepository
    {
        protected readonly Services.ResilientBlobServiceClient _blobServiceClient;
        protected readonly Microsoft.SemanticKernel.Memory.ISemanticTextMemory _memory;
        protected readonly ILogger _logger;
        
        public BaseBlobRepository(
            Services.ResilientBlobServiceClient blobServiceClient,
            Microsoft.SemanticKernel.Memory.ISemanticTextMemory memory,
            ILogger logger)
        {
            _blobServiceClient = blobServiceClient;
            _memory = memory;
            _logger = logger;
        }
        
        /// <summary>
        /// Garante que um container existe
        /// </summary>
        /// <param name="containerName">Nome do container</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        protected async Task EnsureContainerExistsAsync(string containerName, CancellationToken cancellationToken)
        {
            await _blobServiceClient.CreateBlobContainerAsync(containerName, cancellationToken);
        }
        
        /// <summary>
        /// Normaliza um nome de blob
        /// </summary>
        protected string NormalizeBlobName(string name)
        {
            // Remove caracteres inválidos para nomes de blob
            return name.Replace('\\', '/').Replace('?', '_').Replace('&', '_')
                       .Replace(':', '_').Replace('*', '_').Replace('"', '_')
                       .Replace('<', '_').Replace('>', '_').Replace('|', '_');
        }
    }
}
