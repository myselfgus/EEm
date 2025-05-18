using Microsoft.MCP;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureAI;
using EemCore.Data.Repositories;
using EemCore.Models;
using System.Text;
using EemCore.Configuration;
using Microsoft.Extensions.Options;

namespace EemCore.Agents
{
    /// <summary>
    /// Implementação do Agente Euleriano para o sistema Εεm (Ευ-εnable-memory).
    /// 
    /// O Agente Euleriano é responsável por:
    /// 1. Processar eventos de atividade e relação em fluxos eulerianos
    /// 2. Identificar padrões em sequências de atividades
    /// 3. Construir e manter grafos de fluxo de informações
    /// 4. Garantir que cada informação seja processada exatamente uma vez
    /// </summary>
    [McpToolType]
    public class EulerianAgent
    {
        private readonly IActivityRepository _activityRepository;
        private readonly IRelationRepository _relationRepository;
        private readonly IEulerianFlowRepository _flowRepository;
        private readonly EemOptions _options;
        private readonly ILogger<EulerianAgent> _logger;
        private bool _isActive = true;

        public EulerianAgent(
            IActivityRepository activityRepository,
            IRelationRepository relationRepository,
            IEulerianFlowRepository flowRepository,
            IOptions<EemOptions> options,
            ILogger<EulerianAgent> logger)
        {
            _activityRepository = activityRepository;
            _relationRepository = relationRepository;
            _flowRepository = flowRepository;
            _options = options.Value;
            _logger = logger;
            _isActive = _options.EnableEulerianProcessing;
        }

        /// <summary>
        /// Gera um fluxo euleriano a partir de atividades e relações
        /// </summary>
        [McpTool("GenerateFlow")]
        [Description("Generates an Eulerian flow from activities and relations")]
        public async Task<EulerianFlow> GenerateFlowAsync(
            [Description("Name for the flow")]
            string name,

            [Description("Session ID to build flow from")]
            string sessionId,

            [Description("Optional time window in minutes")]
            int? timeWindowMinutes = null,

            [Description("Optional focus topic to prioritize")]
            string? focusTopic = null,

            [Description("Optional comma-separated categories")]
            string? categories = null)
        {
            _logger.LogInformation("Gerando fluxo euleriano: {Name}, Sessão: {SessionId}", name, sessionId);

            if (!_options.EnableEulerianProcessing)
            {
                _logger.LogWarning("Processamento euleriano está desabilitado nas configurações");
                throw new InvalidOperationException("Eulerian processing is disabled in system configuration");
            }

            // Determinar janela de tempo
            var endTime = DateTime.UtcNow;
            var startTime = timeWindowMinutes.HasValue
                ? endTime.AddMinutes(-timeWindowMinutes.Value)
                : endTime.AddHours(-24); // Default 24 horas

            // Obter atividades no intervalo de tempo
            var activities = (await _activityRepository.GetActivityEventsInTimeRangeAsync(
                startTime, endTime, _options.MaxEventsPerActivity))
                .Where(a => a.SessionId == sessionId)
                .ToList();

            if (!activities.Any())
            {
                _logger.LogWarning("Não há atividades para gerar fluxo na sessão: {SessionId}", sessionId);
                throw new InvalidOperationException("No activities found for the specified session and time range");
            }

            // Aplicar filtro de tópico se fornecido
            if (!string.IsNullOrWhiteSpace(focusTopic))
            {
                var focusActivities = await _activityRepository.SearchActivityEventsAsync(
                    focusTopic, _options.MaxEventsPerActivity);

                // Combinar atividades do intervalo de tempo com atividades do tópico
                activities = activities
                    .Union(focusActivities.Where(a => a.SessionId == sessionId))
                    .Distinct()
                    .OrderBy(a => a.Timestamp)
                    .ToList();
            }

            // Obter relações para as atividades
            var activityIds = activities.Select(a => a.Id).ToList();
            var relations = await _relationRepository.GetRelationsForEventsAsync(activityIds);

            // Criar fluxo euleriano
            var flow = new EulerianFlow
            {
                Name = name,
                Timestamp = DateTime.UtcNow,
                Summary = GenerateFlowSummary(activities, relations)
            };

            // Adicionar categorias se fornecidas
            if (!string.IsNullOrWhiteSpace(categories))
            {
                flow.Categories = categories
                    .Split(',')
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToList();
            }

            // Criar nós do grafo a partir das atividades
            foreach (var activity in activities)
            {
                flow.Nodes.Add(new FlowNode
                {
                    NodeType = activity.ActivityType,
                    EventId = activity.Id,
                    Label = GenerateNodeLabel(activity),
                    Metadata = new Dictionary<string, object>
                    {
                        ["timestamp"] = activity.Timestamp,
                        ["source"] = activity.Source
                    }
                });
            }

            // Criar arestas do grafo a partir das relações
            var nodeIdMap = flow.Nodes.ToDictionary(n => n.EventId, n => n.Id);

            foreach (var relation in relations)
            {
                // Se a relação conecta nós que estão no grafo, criar uma aresta
                if (relation.RelatedEventIds.Count >= 2)
                {
                    for (int i = 0; i < relation.RelatedEventIds.Count - 1; i++)
                    {
                        var sourceEventId = relation.RelatedEventIds[i];
                        var targetEventId = relation.RelatedEventIds[i + 1];

                        if (nodeIdMap.ContainsKey(sourceEventId) && nodeIdMap.ContainsKey(targetEventId))
                        {
                            flow.Edges.Add(new FlowEdge
                            {
                                SourceId = nodeIdMap[sourceEventId],
                                TargetId = nodeIdMap[targetEventId],
                                RelationType = relation.RelationType,
                                Weight = relation.RelationStrength
                            });
                        }
                    }
                }
            }

            // Adicionar arestas temporais (se não existirem relações explícitas)
            if (!flow.Edges.Any())
            {
                // Ordenar nós por timestamp da atividade
                var orderedNodes = flow.Nodes
                    .OrderBy(n => activities.First(a => a.Id == n.EventId).Timestamp)
                    .ToList();

                // Criar arestas temporais entre nós sequenciais
                for (int i = 0; i < orderedNodes.Count - 1; i++)
                {
                    flow.Edges.Add(new FlowEdge
                    {
                        SourceId = orderedNodes[i].Id,
                        TargetId = orderedNodes[i + 1].Id,
                        RelationType = "temporal_sequence",
                        Weight = 1.0
                    });
                }
            }

            // Salvar fluxo no repositório
            await _flowRepository.SaveFlowAsync(flow);

            _logger.LogInformation("Fluxo euleriano gerado: {Id}, {Name}, Nós: {Nodes}, Arestas: {Edges}",
                flow.Id, flow.Name, flow.Nodes.Count, flow.Edges.Count);

            return flow;
        }

        /// <summary>
        /// Obtém um fluxo euleriano pelo ID
        /// </summary>
        [McpTool("GetFlow")]
        [Description("Gets an Eulerian flow by ID")]
        public async Task<EulerianFlow?> GetFlowAsync(
            [Description("ID of the flow to retrieve")]
            string flowId)
        {
            _logger.LogInformation("Obtendo fluxo euleriano: {Id}", flowId);

            return await _flowRepository.GetFlowByIdAsync(flowId);
        }

        /// <summary>
        /// Busca fluxos eulerianos baseado em consulta textual
        /// </summary>
        [McpTool("SearchFlows")]
        [Description("Searches for Eulerian flows based on text query")]
        public async Task<IEnumerable<EulerianFlow>> SearchFlowsAsync(
            [Description("Text query to search for")]
            string query,

            [Description("Maximum number of results to return")]
            int maxResults = 10)
        {
            _logger.LogInformation("Buscando fluxos eulerianos com consulta: {Query}", query);

            return await _flowRepository.SearchFlowsAsync(query, maxResults);
        }

        /// <summary>
        /// Obtém fluxos eulerianos dentro de uma janela de tempo
        /// </summary>
        [McpTool("GetFlowsByTimeRange")]
        [Description("Gets Eulerian flows within a specific time range")]
        public async Task<IEnumerable<EulerianFlow>> GetFlowsByTimeRangeAsync(
            [Description("Start time in ISO 8601 format")]
            string startTimeIso,

            [Description("End time in ISO 8601 format")]
            string endTimeIso,

            [Description("Maximum number of flows to return")]
            int maxFlows = 100)
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

            _logger.LogInformation("Obtendo fluxos eulerianos no intervalo: {Start} a {End}, máximo: {MaxFlows}",
                startTime, endTime, maxFlows);

            return await _flowRepository.GetFlowsInTimeRangeAsync(
                startTime, endTime, maxFlows);
        }

        /// <summary>
        /// Exporta um fluxo euleriano para formato específico
        /// </summary>
        [McpTool("ExportFlow")]
        [Description("Exports an Eulerian flow to a specific format")]
        public async Task<string> ExportFlowAsync(
            [Description("ID of the flow to export")]
            string flowId,

            [Description("Format to export to (json, dot, mermaid)")]
            string format = "json")
        {
            _logger.LogInformation("Exportando fluxo euleriano: {Id}, formato: {Format}", flowId, format);

            var flow = await _flowRepository.GetFlowByIdAsync(flowId);

            if (flow == null)
            {
                _logger.LogWarning("Fluxo não encontrado: {Id}", flowId);
                throw new ArgumentException($"Flow not found with ID: {flowId}");
            }

            format = format.ToLowerInvariant();

            if (format == "json")
            {
                return JsonSerializer.Serialize(flow, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            else if (format == "dot")
            {
                return ExportToDotFormat(flow);
            }
            else if (format == "mermaid")
            {
                return ExportToMermaidFormat(flow);
            }
            else
            {
                throw new ArgumentException($"Unsupported export format: {format}");
            }
        }

        /// <summary>
        /// Cria um novo fluxo euleriano baseado em eventos de atividade
        /// </summary>
        [McpTool("CreateEulerianFlow")]
        [Description("Creates a new Eulerian flow based on activity events")]
        public async Task<string> CreateEulerianFlowAsync(
            [Description("Session ID to create flow from")]
            string sessionId,

            [Description("Name for the flow")]
            string flowName,

            [Description("Maximum number of activities to include")]
            int maxActivities = 50)
        {
            if (!_isActive)
            {
                _logger.LogWarning("Tentativa de criar fluxo euleriano enquanto o processamento está desativado");
                return "Processamento euleriano desativado nas configurações.";
            }

            try
            {
                _logger.LogInformation("Criando fluxo euleriano '{Name}' para sessão {SessionId}",
                    flowName, sessionId);

                // Obter eventos de atividade recentes para a sessão
                var activities = (await _activityRepository.GetActivityEventsForSessionAsync(sessionId))
                    .OrderBy(a => a.Timestamp)
                    .Take(maxActivities)
                    .ToList();

                if (activities.Count == 0)
                {
                    return "Não há atividades suficientes para criar um fluxo euleriano.";
                }

                // Criar um novo fluxo euleriano
                var flow = new EulerianFlow
                {
                    Name = flowName,
                    Timestamp = DateTime.UtcNow,
                    Categories = new List<string> { "auto-generated" }
                };

                // Adicionar nós para cada evento de atividade
                foreach (var activity in activities)
                {
                    var node = new FlowNode
                    {
                        NodeType = activity.ActivityType,
                        EventId = activity.Id,
                        Label = GetLabelForActivity(activity),
                        Metadata = activity.Metadata
                    };

                    flow.Nodes.Add(node);
                }

                // Criar arestas entre os nós (conexões baseadas em tempo)
                for (int i = 0; i < flow.Nodes.Count - 1; i++)
                {
                    var edge = new FlowEdge
                    {
                        SourceId = flow.Nodes[i].Id,
                        TargetId = flow.Nodes[i + 1].Id,
                        RelationType = "sequential",
                        Weight = 1.0
                    };

                    flow.Edges.Add(edge);
                }

                // Adicionar conexões semânticas (em uma implementação real, usaria análise de similaridade)
                await AddSemanticConnections(flow, activities);

                // Gerar resumo do fluxo
                flow.Summary = $"Fluxo euleriano '{flowName}' com {flow.Nodes.Count} nós e {flow.Edges.Count} conexões, " +
                               $"baseado em atividades de {activities.Min(a => a.Timestamp):yyyy-MM-dd HH:mm} a " +
                               $"{activities.Max(a => a.Timestamp):yyyy-MM-dd HH:mm}.";

                // Salvar o fluxo no repositório
                string flowId = await _flowRepository.SaveFlowAsync(flow);

                _logger.LogInformation("Fluxo euleriano criado com sucesso: {Id}", flowId);

                return flowId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar fluxo euleriano");
                return $"Erro ao criar fluxo euleriano: {ex.Message}";
            }
        }

        /// <summary>
        /// Obtém um fluxo euleriano por ID
        /// </summary>
        [McpTool("GetEulerianFlow")]
        [Description("Gets an Eulerian flow by ID")]
        public async Task<string?> GetEulerianFlowAsync(
            [Description("ID of the flow to retrieve")]
            string flowId)
        {
            if (!_isActive)
            {
                _logger.LogWarning("Tentativa de obter fluxo euleriano enquanto o processamento está desativado");
                return "Processamento euleriano desativado nas configurações.";
            }

            try
            {
                var flow = await _flowRepository.GetFlowByIdAsync(flowId);

                if (flow == null)
                {
                    return $"Fluxo euleriano com ID {flowId} não encontrado.";
                }

                // Serializar o fluxo para JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(flow, options);

                return json;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter fluxo euleriano: {Id}", flowId);
                return $"Erro ao obter fluxo euleriano: {ex.Message}";
            }
        }

        /// <summary>
        /// Busca fluxos eulerianos com base em uma consulta
        /// </summary>
        [McpTool("SearchEulerianFlows")]
        [Description("Searches for Eulerian flows based on a query")]
        public async Task<string> SearchEulerianFlowsAsync(
            [Description("Search query")]
            string query,

            [Description("Maximum number of results")]
            int maxResults = 5)
        {
            if (!_isActive)
            {
                _logger.LogWarning("Tentativa de buscar fluxos eulerianos enquanto o processamento está desativado");
                return "Processamento euleriano desativado nas configurações.";
            }

            try
            {
                var flows = await _flowRepository.SearchFlowsAsync(query, maxResults);

                if (flows.Count() == 0)
                {
                    return $"Nenhum fluxo euleriano encontrado para a consulta '{query}'.";
                }

                // Formatar resultados como texto
                var results = flows.Select(f => new
                {
                    Id = f.Id,
                    Name = f.Name,
                    Timestamp = f.Timestamp,
                    NodesCount = f.Nodes.Count,
                    EdgesCount = f.Edges.Count,
                    Summary = f.Summary
                });

                // Serializar os resultados
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(results, options);

                return json;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar fluxos eulerianos");
                return $"Erro ao buscar fluxos eulerianos: {ex.Message}";
            }
        }

        /// <summary>
        /// Obtém o status atual do agente euleriano
        /// </summary>
        [McpTool("GetStatus")]
        [Description("Gets the current status of the Eulerian agent")]
        public async Task<EulerianAgentStatus> GetStatusAsync()
        {
            // Em uma implementação real, verificaria mais métricas de estado
            return new EulerianAgentStatus
            {
                IsActive = _isActive,
                ProcessingEnabled = _options.EnableEulerianProcessing,
                StatusMessage = _isActive ? "Agente euleriano ativo" : "Agente euleriano inativo"
            };
        }

        /// <summary>
        /// Gera um resumo do fluxo euleriano
        /// </summary>
        private string GenerateFlowSummary(List<ActivityJournalEvent> activities, IEnumerable<InterpretedRelationEvent> relations)
        {
            if (!activities.Any())
            {
                return "Fluxo vazio sem atividades.";
            }

            var sb = new StringBuilder();

            // Estatísticas básicas
            sb.Append($"Fluxo de {activities.Count} atividades");

            if (relations.Any())
            {
                sb.Append($" com {relations.Count()} relações");
            }

            // Período de tempo
            var minTime = activities.Min(a => a.Timestamp);
            var maxTime = activities.Max(a => a.Timestamp);
            var duration = maxTime - minTime;

            sb.Append($" no período de {minTime:yyyy-MM-dd HH:mm} a {maxTime:yyyy-MM-dd HH:mm}");
            sb.Append($" (duração: {FormatTimeSpan(duration)})");

            // Tipos de atividade
            var activityTypes = activities
                .GroupBy(a => a.ActivityType)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => $"{g.Key} ({g.Count()})")
                .ToList();

            if (activityTypes.Any())
            {
                sb.Append($". Principais tipos: {string.Join(", ", activityTypes)}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gera um rótulo para um nó de grafo
        /// </summary>
        private string GenerateNodeLabel(ActivityJournalEvent activity)
        {
            // Criar um rótulo conciso baseado no tipo e conteúdo
            string shortContent = activity.Content;

            // Truncar conteúdo longo
            if (shortContent.Length > 50)
            {
                shortContent = shortContent.Substring(0, 47) + "...";
            }

            // Remover quebras de linha
            shortContent = shortContent.Replace("\n", " ").Replace("\r", "");

            // Formatar label dependendo do tipo
            switch (activity.ActivityType.ToLowerInvariant())
            {
                case "edit":
                case "coding":
                    return $"Edit: {shortContent}";

                case "navigation":
                    return $"Nav: {shortContent}";

                case "search":
                case "query":
                    return $"Search: {shortContent}";

                case "execution":
                case "run":
                    return $"Run: {shortContent}";

                default:
                    return $"{activity.ActivityType}: {shortContent}";
            }
        }

        /// <summary>
        /// Exporta fluxo para formato DOT (Graphviz)
        /// </summary>
        private string ExportToDotFormat(EulerianFlow flow)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"digraph \"{flow.Name}\" {{");
            sb.AppendLine("  // Graph attributes");
            sb.AppendLine("  graph [rankdir=LR, fontname=\"Arial\", labelloc=\"t\"];");
            sb.AppendLine("  node [shape=box, style=filled, fillcolor=lightblue, fontname=\"Arial\"];");
            sb.AppendLine("  edge [fontname=\"Arial\"];");
            sb.AppendLine($"  label=\"{flow.Name} - {flow.Summary}\";");
            sb.AppendLine();

            // Nós
            sb.AppendLine("  // Nodes");
            foreach (var node in flow.Nodes)
            {
                string color = "lightblue";
                switch (node.NodeType.ToLowerInvariant())
                {
                    case "edit":
                    case "coding": color = "lightgreen"; break;
                    case "navigation": color = "lightyellow"; break;
                    case "search": color = "lightcyan"; break;
                    case "execution": color = "lightcoral"; break;
                }

                sb.AppendLine($"  \"{node.Id}\" [label=\"{node.Label}\", fillcolor={color}];");
            }

            sb.AppendLine();

            // Arestas
            sb.AppendLine("  // Edges");
            foreach (var edge in flow.Edges)
            {
                string style = edge.RelationType.ToLowerInvariant() switch
                {
                    "temporal" => "dashed",
                    "causal" => "solid",
                    "semantic" => "dotted",
                    _ => "solid"
                };

                sb.AppendLine($"  \"{edge.SourceId}\" -> \"{edge.TargetId}\" [label=\"{edge.RelationType} ({edge.Weight:F2})\", style={style}];");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Exporta fluxo para formato Mermaid
        /// </summary>
        private string ExportToMermaidFormat(EulerianFlow flow)
        {
            var sb = new StringBuilder();

            sb.AppendLine("```mermaid");
            sb.AppendLine("graph LR");
            sb.AppendLine($"    %% {flow.Name} - {flow.Summary}");
            sb.AppendLine();

            // Nós
            sb.AppendLine("    %% Nodes");
            foreach (var node in flow.Nodes)
            {
                string style = node.NodeType.ToLowerInvariant() switch
                {
                    "edit" => "fill:#d4ffdd",
                    "coding" => "fill:#d4ffdd",
                    "navigation" => "fill:#ffffd4",
                    "search" => "fill:#d4f4ff",
                    "execution" => "fill:#ffd4d4",
                    _ => "fill:#f9f9f9"
                };

                sb.AppendLine($"    {node.Id}[\"{node.Label}\"]:::type{node.NodeType.Replace(" ", "")} style=\"{style}\"");
            }

            sb.AppendLine();

            // Arestas
            sb.AppendLine("    %% Edges");
            foreach (var edge in flow.Edges)
            {
                string linkStyle = edge.RelationType.ToLowerInvariant() switch
                {
                    "temporal" => " -.->|\"temporal\"|",
                    "causal" => " ==>|\"causal\"|",
                    "semantic" => " -->|\"semantic\"|",
                    _ => " -->|\"" + edge.RelationType + "\"|"
                };

                sb.AppendLine($"    {edge.SourceId}{linkStyle} {edge.TargetId}");
            }

            // Estilos
            sb.AppendLine();
            sb.AppendLine("    %% Class definitions");
            sb.AppendLine("    classDef typeEdit stroke:#28a745,color:#333");
            sb.AppendLine("    classDef typeCoding stroke:#28a745,color:#333");
            sb.AppendLine("    classDef typeNavigation stroke:#ffc107,color:#333");
            sb.AppendLine("    classDef typeSearch stroke:#17a2b8,color:#333");
            sb.AppendLine("    classDef typeExecution stroke:#dc3545,color:#333");

            sb.AppendLine("```");

            return sb.ToString();
        }

        /// <summary>
        /// Formata um TimeSpan para exibição amigável
        /// </summary>
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
            {
                return $"{timeSpan.Days}d {timeSpan.Hours}h {timeSpan.Minutes}m";
            }
            else if (timeSpan.TotalHours >= 1)
            {
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
            }
            else if (timeSpan.TotalMinutes >= 1)
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            else
            {
                return $"{timeSpan.Seconds}s";
            }
        }

        /// <summary>
        /// Gera um rótulo para um nó baseado em uma atividade
        /// </summary>
        private string GetLabelForActivity(ActivityJournalEvent activity)
        {
            // Gerar um rótulo com base no tipo de atividade
            if (activity.ActivityType == "code_edit")
            {
                string fileName = activity.Metadata.TryGetValue("file", out var file)
                    ? file.ToString() ?? "unknown_file"
                    : "unknown_file";

                return $"Edição: {fileName}";
            }
            else if (activity.ActivityType == "navigation")
            {
                return $"Navegação: {activity.Content.Substring(0, Math.Min(30, activity.Content.Length))}";
            }
            else if (activity.ActivityType == "ai_interaction")
            {
                string prompt = activity.Metadata.TryGetValue("prompt", out var p)
                    ? p.ToString() ?? "unknown_prompt"
                    : "unknown_prompt";

                return $"AI: {prompt.Substring(0, Math.Min(30, prompt.Length))}";
            }
            else if (activity.ActivityType == "web_search")
            {
                return $"Busca: {activity.Content.Substring(0, Math.Min(30, activity.Content.Length))}";
            }
            else
            {
                return $"{activity.ActivityType}: {activity.Timestamp:HH:mm:ss}";
            }
        }

        /// <summary>
        /// Adiciona conexões semânticas entre nós de um fluxo
        /// </summary>
        private async Task AddSemanticConnections(EulerianFlow flow, List<ActivityJournalEvent> activities)
        {
            // Em uma implementação real, analisaria a similaridade semântica
            // entre atividades e criaria conexões adicionais

            // Simulação simplificada - conectar atividades do mesmo tipo
            var nodesByType = flow.Nodes.GroupBy(n => n.NodeType)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in nodesByType)
            {
                var nodes = group.ToList();

                // Conectar nós do mesmo tipo que não são adjacentes
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = i + 2; j < nodes.Count; j++) // Pular conexões adjacentes
                    {
                        // Verificar se já existe uma conexão
                        if (!flow.Edges.Any(e =>
                            (e.SourceId == nodes[i].Id && e.TargetId == nodes[j].Id) ||
                            (e.SourceId == nodes[j].Id && e.TargetId == nodes[i].Id)))
                        {
                            var edge = new FlowEdge
                            {
                                SourceId = nodes[i].Id,
                                TargetId = nodes[j].Id,
                                RelationType = "semantic",
                                Weight = 0.5 // Peso menor para conexões semânticas
                            };

                            flow.Edges.Add(edge);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Representa o status do agente euleriano
    /// </summary>
    public class EulerianAgentStatus
    {
        public bool IsActive { get; set; }
        public bool ProcessingEnabled { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
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
