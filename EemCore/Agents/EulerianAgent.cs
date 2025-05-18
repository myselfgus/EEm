using Microsoft.MCP;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureAI;

namespace EemCore.Agents
{
    /// <summary>
    /// Implementação do Agente Euleriano para o sistema Εεm (Ευ-εnable-memory).
    /// 
    /// O Agente Euleriano é responsável por:
    /// 1. Processar arquivos .aje e .ire para criar fluxos estruturados
    /// 2. Gerar arquivos .e (Eulerian) representando contextos persistentes
    /// 3. Construir e manter a meta-estrutura .Re (Relation Eulerian)
    /// 4. Executar automaticamente ao final de sessão ou a cada 24 horas
    /// 
    /// Esta classe implementa a lógica central do processamento euleriano,
    /// que transforma dados brutos em estruturas navegáveis de contexto.
    /// </summary>
    [McpToolType]
    public class EulerianAgent
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly CosmosClient _cosmosClient;
        private readonly IKernel _kernel;
        private readonly ILogger<EulerianAgent> _logger;

        private const string AjeContainer = "aje-files";
        private const string IreContainer = "ire-files";
        private const string EContainer = "e-files";
        private const string GraphDatabase = "eemmemory";
        private const string GraphContainer = "relationalgraph";

        /// <summary>
        /// Construtor do Agente Euleriano com injeção de dependências
        /// </summary>
        public EulerianAgent(
            BlobServiceClient blobServiceClient,
            CosmosClient cosmosClient,
            IKernel kernel,
            ILogger<EulerianAgent> logger)
        {
            _blobServiceClient = blobServiceClient;
            _cosmosClient = cosmosClient;
            _kernel = kernel;
            _logger = logger;
        }

        /// <summary>
        /// Processa eventos e gera fluxos eulerianos. Este método seria acionado:
        /// 1. Por um Azure Function Timer Trigger (execução diária)
        /// 2. Por um evento de finalização de sessão
        /// </summary>
        [McpTool("ProcessEulerianFlows")]
        [Description("Processes activity events to generate eulerian flows and update the meta-structure")]
        public async Task<string> ProcessEulerianFlowsAsync(
            [Description("Session ID to process, or 'all' for all recent sessions")]
            string sessionId = "all",
            
            [Description("Time window in hours to process (0 for all data)")]
            int timeWindowHours = 24)
        {
            _logger.LogInformation($"Iniciando processamento euleriano para sessão: {sessionId}");
            
            try
            {
                // 1. Coletar eventos .aje e .ire para processamento
                var eventsToProcess = await CollectEventsAsync(sessionId, timeWindowHours);
                
                if (eventsToProcess.Item1.Count == 0 && eventsToProcess.Item2.Count == 0)
                {
                    return "Nenhum evento encontrado para processamento euleriano.";
                }
                
                _logger.LogInformation($"Coletados {eventsToProcess.Item1.Count} eventos .aje e " +
                                      $"{eventsToProcess.Item2.Count} eventos .ire");
                
                // 2. Identificar fluxos lógicos de atividade
                var flows = await IdentifyFlowsAsync(eventsToProcess.Item1, eventsToProcess.Item2);
                
                _logger.LogInformation($"Identificados {flows.Count} fluxos eulerianos");
                
                // 3. Salvar fluxos como arquivos .e
                int savedFlowsCount = await SaveEulerianFlowsAsync(flows);
                
                // 4. Atualizar a meta-estrutura .Re
                int updatedNodes = await UpdateRelationalGraphAsync(flows);
                
                return $"Processamento euleriano concluído. Gerados {savedFlowsCount} fluxos " +
                       $"e atualizados {updatedNodes} nós no grafo relacional.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro no processamento euleriano: {ex.Message}");
                return $"Erro no processamento euleriano: {ex.Message}";
            }
        }

        /// <summary>
        /// Coleta eventos .aje e .ire para processamento
        /// </summary>
        private async Task<(List<ActivityJournalEvent>, List<InterpretedRelationEvent>)> 
            CollectEventsAsync(string sessionId, int timeWindowHours)
        {
            var ajeEvents = new List<ActivityJournalEvent>();
            var ireEvents = new List<InterpretedRelationEvent>();
            
            // Calcular janela de tempo
            var cutoffTime = timeWindowHours > 0 
                ? DateTime.UtcNow.AddHours(-timeWindowHours) 
                : DateTime.MinValue;
            
            // 1. Coletar eventos .aje
            var ajeContainerClient = _blobServiceClient.GetBlobContainerClient(AjeContainer);
            
            // Definir prefixo de busca
            string ajePrefix = sessionId != "all" ? $"{sessionId}/" : "";
            
            await foreach (var blob in ajeContainerClient.GetBlobsAsync(prefix: ajePrefix))
            {
                // Verificar se o blob está dentro da janela de tempo
                if (blob.Properties.CreatedOn.HasValue && 
                    blob.Properties.CreatedOn.Value.UtcDateTime >= cutoffTime)
                {
                    try
                    {
                        var blobClient = ajeContainerClient.GetBlobClient(blob.Name);
                        var response = await blobClient.DownloadContentAsync();
                        
                        var ajeEvent = JsonSerializer.Deserialize<ActivityJournalEvent>(
                            response.Value.Content.ToString());
                            
                        if (ajeEvent != null)
                        {
                            ajeEvents.Add(ajeEvent);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Erro ao ler blob .aje: {blob.Name}");
                    }
                }
            }
            
            // 2. Coletar eventos .ire (processo similar)
            var ireContainerClient = _blobServiceClient.GetBlobContainerClient(IreContainer);
            string irePrefix = sessionId != "all" ? $"{sessionId}/" : "";
            
            await foreach (var blob in ireContainerClient.GetBlobsAsync(prefix: irePrefix))
            {
                if (blob.Properties.CreatedOn.HasValue && 
                    blob.Properties.CreatedOn.Value.UtcDateTime >= cutoffTime)
                {
                    try
                    {
                        var blobClient = ireContainerClient.GetBlobClient(blob.Name);
                        var response = await blobClient.DownloadContentAsync();
                        
                        var ireEvent = JsonSerializer.Deserialize<InterpretedRelationEvent>(
                            response.Value.Content.ToString());
                            
                        if (ireEvent != null)
                        {
                            ireEvents.Add(ireEvent);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Erro ao ler blob .ire: {blob.Name}");
                    }
                }
            }
            
            return (ajeEvents, ireEvents);
        }

        /// <summary>
        /// Identifica fluxos eulerianos baseados nos eventos coletados
        /// </summary>
        private async Task<List<EulerianFlow>> IdentifyFlowsAsync(
            List<ActivityJournalEvent> ajeEvents, 
            List<InterpretedRelationEvent> ireEvents)
        {
            var flows = new List<EulerianFlow>();
            
            // Agrupar eventos por sessão
            var sessionGroups = ajeEvents.GroupBy(e => e.SessionId);
            
            foreach (var sessionGroup in sessionGroups)
            {
                var sessionId = sessionGroup.Key;
                var sessionEvents = sessionGroup.OrderBy(e => e.Timestamp).ToList();
                
                if (sessionEvents.Count == 0)
                {
                    continue;
                }
                
                // Encontrar relações relevantes para essa sessão
                var sessionRelations = ireEvents
                    .Where(r => r.SourceEvents.Any(se => 
                        sessionEvents.Any(e => 
                            e.Timestamp == se.Timestamp && 
                            e.Content == se.Content && 
                            e.ActivityType == se.ActivityType)))
                    .ToList();
                
                // Determinar janela de tempo da sessão
                var sessionStart = sessionEvents.Min(e => e.Timestamp);
                var sessionEnd = sessionEvents.Max(e => e.Timestamp);
                
                // Em uma implementação real, usaríamos algoritmos de detecção de padrão
                // Para esta documentação executável, criamos um fluxo simples por sessão
                var flow = new EulerianFlow
                {
                    FlowId = Guid.NewGuid().ToString(),
                    TimeRange = (sessionStart, sessionEnd),
                    RelatedEvents = sessionRelations.ToArray(),
                    FlowType = "session_activity",
                    Structure = GenerateFlowStructure(sessionEvents, sessionRelations)
                };
                
                flows.Add(flow);
                
                // Opcionalmente, identificar sub-fluxos por tipo de atividade
                var activityTypes = sessionEvents
                    .Select(e => e.ActivityType)
                    .Distinct();
                    
                foreach (var activityType in activityTypes)
                {
                    var typeEvents = sessionEvents
                        .Where(e => e.ActivityType == activityType)
                        .OrderBy(e => e.Timestamp)
                        .ToList();
                        
                    if (typeEvents.Count > 1)  // Apenas criar fluxo se houver múltiplos eventos
                    {
                        var typeStart = typeEvents.Min(e => e.Timestamp);
                        var typeEnd = typeEvents.Max(e => e.Timestamp);
                        
                        var subFlow = new EulerianFlow
                        {
                            FlowId = Guid.NewGuid().ToString(),
                            TimeRange = (typeStart, typeEnd),
                            RelatedEvents = sessionRelations
                                .Where(r => r.SourceEvents.Any(se => 
                                    typeEvents.Any(e => 
                                        e.Timestamp == se.Timestamp && 
                                        e.Content == se.Content)))
                                .ToArray(),
                            FlowType = $"activity_{activityType}",
                            Structure = GenerateFlowStructure(typeEvents, 
                                sessionRelations.Where(r => r.SourceEvents.Any(se => 
                                    typeEvents.Any(e => 
                                        e.Timestamp == se.Timestamp && 
                                        e.Content == se.Content)))
                                .ToList())
                        };
                        
                        flows.Add(subFlow);
                    }
                }
            }
            
            return flows;
        }

        /// <summary>
        /// Gera a estrutura do fluxo em formato JSON
        /// </summary>
        private string GenerateFlowStructure(
            List<ActivityJournalEvent> events, 
            List<InterpretedRelationEvent> relations)
        {
            // Em uma implementação real, isto criaria uma representação estruturada
            // dos eventos e suas relações
            
            // Para documentação executável, usamos uma representação simplificada
            var structure = new
            {
                events = events.Select(e => new 
                {
                    timestamp = e.Timestamp,
                    type = e.ActivityType,
                    source = e.Source,
                    content = e.Content.Length > 50 
                        ? e.Content.Substring(0, 47) + "..." 
                        : e.Content
                }).ToArray(),
                
                relations = relations.Select(r => new
                {
                    type = r.RelationType,
                    interpretation = r.Interpretation,
                    confidence = r.Confidence,
                    sources = r.SourceEvents.Length
                }).ToArray(),
                
                summary = $"Fluxo com {events.Count} eventos e {relations.Count} relações"
            };
            
            return JsonSerializer.Serialize(structure, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }

        /// <summary>
        /// Salva os fluxos eulerianos como arquivos .e
        /// </summary>
        private async Task<int> SaveEulerianFlowsAsync(List<EulerianFlow> flows)
        {
            if (flows.Count == 0)
            {
                return 0;
            }
            
            // Obter o container para arquivos .e
            var containerClient = _blobServiceClient.GetBlobContainerClient(EContainer);
            await containerClient.CreateIfNotExistsAsync();
            
            int savedCount = 0;
            
            foreach (var flow in flows)
            {
                try
                {
                    // Nome do blob: timestamp_flowId.e
                    var timestamp = flow.TimeRange.Item1.ToString("yyyyMMddHHmmss");
                    var blobName = $"{timestamp}_{flow.FlowId}.e";
                    var blobClient = containerClient.GetBlobClient(blobName);
                    
                    // Salvar como JSON
                    await blobClient.UploadAsync(
                        BinaryData.FromString(JsonSerializer.Serialize(flow)),
                        overwrite: true
                    );
                    
                    savedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Erro ao salvar fluxo euleriano: {ex.Message}");
                    // Continuar com os próximos fluxos
                }
            }
            
            return savedCount;
        }

        /// <summary>
        /// Atualiza o grafo relacional (.Re) baseado nos fluxos identificados
        /// </summary>
        private async Task<int> UpdateRelationalGraphAsync(List<EulerianFlow> flows)
        {
            if (flows.Count == 0)
            {
                return 0;
            }
            
            // Em uma implementação real, isto atualizaria o Cosmos DB Graph API
            
            // Para a documentação executável, simulamos a atualização
            int updatedNodes = 0;
            
            foreach (var flow in flows)
            {
                // 1. Criar nó para o fluxo
                var flowNode = new RelationEulerian
                {
                    NodeId = flow.FlowId,
                    NodeType = "flow",
                    Connections = Array.Empty<(string, string)>(),
                    Properties = new Dictionary<string, string>
                    {
                        ["flowType"] = flow.FlowType,
                        ["startTime"] = flow.TimeRange.Item1.ToString("o"),
                        ["endTime"] = flow.TimeRange.Item2.ToString("o"),
                        ["eventCount"] = flow.RelatedEvents.Length.ToString()
                    }
                };
                
                // Simular adição ao grafo
                _logger.LogInformation($"Adicionado nó de fluxo: {flow.FlowId} ({flow.FlowType})");
                updatedNodes++;
                
                // 2. Criar nós para eventos relacionados e conectar ao fluxo
                foreach (var relEvent in flow.RelatedEvents)
                {
                    var eventNode = new RelationEulerian
                    {
                        NodeId = Guid.NewGuid().ToString(),
                        NodeType = "relation",
                        Connections = new[] { (flow.FlowId, "partOf") },
                        Properties = new Dictionary<string, string>
                        {
                            ["relationType"] = relEvent.RelationType,
                            ["confidence"] = relEvent.Confidence.ToString(),
                            ["timestamp"] = relEvent.Timestamp.ToString("o")
                        }
                    };
                    
                    // Simular adição ao grafo
                    _logger.LogInformation($"Adicionado nó de relação conectado ao fluxo {flow.FlowId}");
                    updatedNodes++;
                }
                
                // 3. Conectar com outros fluxos relacionados temporalmente
                var overlappingFlows = flows
                    .Where(f => f.FlowId != flow.FlowId)
                    .Where(f => 
                        (f.TimeRange.Item1 <= flow.TimeRange.Item2 && 
                         f.TimeRange.Item2 >= flow.TimeRange.Item1))
                    .ToList();
                    
                foreach (var overlapping in overlappingFlows)
                {
                    // Simular criação de conexão bidirecional
                    _logger.LogInformation(
                        $"Conectando fluxos relacionados: {flow.FlowId} <-> {overlapping.FlowId}");
                    updatedNodes++;
                }
            }
            
            return updatedNodes;
        }

        /// <summary>
        /// Executa análise euleriana sob demanda para uma consulta específica
        /// </summary>
        [McpTool("AnalyzeContext")]
        [Description("Performs on-demand eulerian analysis for a specific query")]
        public async Task<string> AnalyzeContextAsync(
            [Description("Query to analyze for contextual patterns")]
            string query,
            
            [Description("Time window in hours to analyze (0 for all data)")]
            int timeWindowHours = 24,
            
            [Description("Depth of analysis (1-3, where 3 is most detailed)")]
            int depth = 2)
        {
            _logger.LogInformation($"Iniciando análise euleriana para query: '{query}'");
            
            try
            {
                // Em uma implementação real, isto usaria o Azure OpenAI para 
                // fazer análise semântica profunda dos dados relacionados à consulta
                
                // Para documentação executável, criamos uma resposta simulada
                var analysisResult = new StringBuilder();
                
                analysisResult.AppendLine($"# Análise Euleriana para: '{query}'");
                analysisResult.AppendLine();
                analysisResult.AppendLine($"## Janela temporal: {(timeWindowHours > 0 ? $"últimas {timeWindowHours} horas" : "todo histórico")}");
                analysisResult.AppendLine();
                
                // Simular fluxos identificados
                analysisResult.AppendLine("## Fluxos Identificados");
                analysisResult.AppendLine();
                
                for (int i = 1; i <= 3; i++)
                {
                    analysisResult.AppendLine($"### Fluxo {i}");
                    analysisResult.AppendLine();
                    analysisResult.AppendLine($"- **Tipo**: {(i == 1 ? "Desenvolvimento" : i == 2 ? "Pesquisa" : "Interação AI")}");
                    analysisResult.AppendLine($"- **Período**: {DateTime.UtcNow.AddHours(-i*2):g} a {DateTime.UtcNow.AddHours(-i):g}");
                    analysisResult.AppendLine($"- **Principais atividades**: {(i == 1 ? "Edição de código, Depuração" : i == 2 ? "Pesquisa web, Leitura documentação" : "Consultas GitHub Copilot, Edição guiada")}");
                    analysisResult.AppendLine($"- **Relevância para '{query}'**: {(4-i) * 25}%");
                    analysisResult.AppendLine();
                }
                
                // Adicionar mais detalhes conforme a profundidade solicitada
                if (depth >= 2)
                {
                    analysisResult.AppendLine("## Padrões de Atividade");
                    analysisResult.AppendLine();
                    analysisResult.AppendLine("1. **Ciclo de desenvolvimento iterativo**");
                    analysisResult.AppendLine("   - Edição de código → Consulta AI → Depuração → Edição de código");
                    analysisResult.AppendLine("2. **Pesquisa antes de implementação**");
                    analysisResult.AppendLine("   - Pesquisa web → Leitura documentação → Edição de código");
                }
                
                if (depth >= 3)
                {
                    analysisResult.AppendLine();
                    analysisResult.AppendLine("## Insight Contextual");
                    analysisResult.AppendLine();
                    analysisResult.AppendLine($"Com base na análise euleriana dos fluxos de atividade relacionados a '{query}', ");
                    analysisResult.AppendLine("identifica-se um padrão de desenvolvimento que alterna entre pesquisa, ");
                    analysisResult.AppendLine("implementação guiada por AI e ciclos de depuração. Este padrão sugere ");
                    analysisResult.AppendLine("uma abordagem exploratória para a tarefa atual, potencialmente indicando ");
                    analysisResult.AppendLine("que o desenvolvedor está trabalhando com tecnologias relativamente novas.");
                }
                
                return analysisResult.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro na análise euleriana: {ex.Message}");
                return $"Erro na análise euleriana: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// Representa um evento de relação interpretada (.ire)
    /// </summary>
    public class InterpretedRelationEvent
    {
        public DateTime Timestamp { get; set; }
        public ActivityJournalEvent[] SourceEvents { get; set; } = Array.Empty<ActivityJournalEvent>();
        public string RelationType { get; set; } = string.Empty;
        public string Interpretation { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Representa um fluxo euleriano (.e)
    /// </summary>
    public class EulerianFlow
    {
        public string FlowId { get; set; } = string.Empty;
        public (DateTime, DateTime) TimeRange { get; set; }
        public InterpretedRelationEvent[] RelatedEvents { get; set; } = Array.Empty<InterpretedRelationEvent>();
        public string FlowType { get; set; } = string.Empty;
        public string Structure { get; set; } = string.Empty;
    }

    /// <summary>
    /// Representa um nó na meta-estrutura relacional euleriana (.Re)
    /// </summary>
    public class RelationEulerian
    {
        public string NodeId { get; set; } = string.Empty;
        public string NodeType { get; set; } = string.Empty;
        public (string, string)[] Connections { get; set; } = Array.Empty<(string, string)>();
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }
}
