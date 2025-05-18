using System.Text.Json.Serialization;

namespace EemCore.Models
{
    /// <summary>
    /// Representa um fluxo euleriano de atividades (.e)
    /// </summary>
    public class EulerianFlow
    {
        /// <summary>
        /// Identificador único do fluxo
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Timestamp de quando o fluxo foi criado
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Nome descritivo do fluxo
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Sequência ordenada de eventos no fluxo
        /// </summary>
        public List<FlowNode> Nodes { get; set; } = new();

        /// <summary>
        /// Conexões entre os nós (arestas do grafo)
        /// </summary>
        public List<FlowEdge> Edges { get; set; } = new();

        /// <summary>
        /// Categorias associadas ao fluxo
        /// </summary>
        public List<string> Categories { get; set; } = new();

        /// <summary>
        /// Resumo do fluxo
        /// </summary>
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Representa um nó em um fluxo euleriano
    /// </summary>
    public class FlowNode
    {
        /// <summary>
        /// Identificador único do nó
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Tipo de nó
        /// </summary>
        public string NodeType { get; set; } = string.Empty;

        /// <summary>
        /// ID do evento associado
        /// </summary>
        public string EventId { get; set; } = string.Empty;

        /// <summary>
        /// Rótulo do nó
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Metadados adicionais para o nó
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Representa uma aresta em um fluxo euleriano
    /// </summary>
    public class FlowEdge
    {
        /// <summary>
        /// Identificador único da aresta
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// ID do nó de origem
        /// </summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// ID do nó de destino
        /// </summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de relacionamento
        /// </summary>
        public string RelationType { get; set; } = string.Empty;

        /// <summary>
        /// Peso ou força da conexão
        /// </summary>
        public double Weight { get; set; } = 1.0;
    }
}