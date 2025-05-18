using EemCore.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EemCore.Data.Repositories
{
    /// <summary>
    /// Interface para reposit�rio de eventos de atividade
    /// </summary>
    public interface IActivityRepository
    {
        /// <summary>
        /// Salva um novo evento de atividade no armazenamento
        /// </summary>
        Task<string> SaveActivityEventAsync(ActivityJournalEvent activityEvent, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obt�m eventos de atividade para uma sess�o espec�fica
        /// </summary>
        Task<IEnumerable<ActivityJournalEvent>> GetActivityEventsForSessionAsync(string sessionId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obt�m eventos de atividade dentro de uma janela de tempo espec�fica
        /// </summary>
        Task<IEnumerable<ActivityJournalEvent>> GetActivityEventsInTimeRangeAsync(
            DateTime startTime, 
            DateTime endTime, 
            int maxEvents = 1000,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Busca eventos de atividade baseado em uma consulta textual
        /// </summary>
        Task<IEnumerable<ActivityJournalEvent>> SearchActivityEventsAsync(
            string searchQuery, 
            int maxResults = 10,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Exclui eventos mais antigos que um per�odo de reten��o
        /// </summary>
        Task<int> PurgeOldEventsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Interface para reposit�rio de rela��es interpretadas
    /// </summary>
    public interface IRelationRepository
    {
        /// <summary>
        /// Salva um novo evento de rela��o no armazenamento
        /// </summary>
        Task<string> SaveRelationEventAsync(InterpretedRelationEvent relationEvent, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obt�m rela��es para eventos de atividade espec�ficos
        /// </summary>
        Task<IEnumerable<InterpretedRelationEvent>> GetRelationsForEventsAsync(
            IEnumerable<string> activityEventIds, 
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Obt�m rela��es dentro de uma janela de tempo espec�fica
        /// </summary>
        Task<IEnumerable<InterpretedRelationEvent>> GetRelationsInTimeRangeAsync(
            DateTime startTime, 
            DateTime endTime, 
            int maxRelations = 1000,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Busca rela��es baseado em uma consulta textual
        /// </summary>
        Task<IEnumerable<InterpretedRelationEvent>> SearchRelationsAsync(
            string searchQuery, 
            int maxResults = 10,
            CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Interface para reposit�rio de fluxos eulerianos
    /// </summary>
    public interface IEulerianFlowRepository
    {
        /// <summary>
        /// Salva um novo fluxo euleriano no armazenamento
        /// </summary>
        Task<string> SaveFlowAsync(EulerianFlow flow, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obt�m fluxos eulerianos dentro de uma janela de tempo espec�fica
        /// </summary>
        Task<IEnumerable<EulerianFlow>> GetFlowsInTimeRangeAsync(
            DateTime startTime, 
            DateTime endTime, 
            int maxFlows = 100,
            CancellationToken cancellationToken = default);
            
        /// <summary>
        /// Obt�m um fluxo euleriano espec�fico pelo ID
        /// </summary>
        Task<EulerianFlow?> GetFlowByIdAsync(string flowId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Busca fluxos eulerianos baseado em uma consulta textual
        /// </summary>
        Task<IEnumerable<EulerianFlow>> SearchFlowsAsync(
            string searchQuery, 
            int maxResults = 10,
            CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Interface para reposit�rio de scripts GenAIScript
    /// </summary>
    public interface IScriptRepository
    {
        /// <summary>
        /// Salva um novo script no armazenamento
        /// </summary>
        Task<string> SaveScriptAsync(ScriptInfo script, string content, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Obt�m um script pelo nome
        /// </summary>
        Task<(ScriptInfo Info, string Content)?> GetScriptByNameAsync(string scriptName, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Lista todos os scripts dispon�veis
        /// </summary>
        Task<IEnumerable<ScriptInfo>> ListScriptsAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Exclui um script espec�fico
        /// </summary>
        Task<bool> DeleteScriptAsync(string scriptId, CancellationToken cancellationToken = default);
    }
}