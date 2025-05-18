using System.Text.Json.Serialization;

namespace EemCore.Models
{
    /// <summary>
    /// Representa um nó na meta-estrutura relacional euleriana (.Re)
    /// Serve como componente base do grafo para navegação contextual
    /// </summary>
    public class RelationEulerian
    {
        /// <summary>
        /// Identificador único do nó no grafo
        /// </summary>
        [JsonPropertyName("nodeId")]
        public string NodeId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Tipo de nó no grafo relacional
        /// (ex: 'flow', 'entity', 'concept', 'activity')
        /// </summary>
        [JsonPropertyName("nodeType")]
        public string NodeType { get; set; } = string.Empty;

        /// <summary>
        /// Conexões entre este nó e outros nós
        /// Cada item é uma tupla (ID do nó alvo, tipo de conexão)
        /// </summary>
        [JsonPropertyName("connections")]
        public List<Connection> Connections { get; set; } = new List<Connection>();

        /// <summary>
        /// Propriedades específicas deste nó
        /// </summary>
        [JsonPropertyName("properties")]
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Vetor de embedding para busca semântica
        /// </summary>
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }

        /// <summary>
        /// Timestamp de criação deste nó
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp da última atualização deste nó
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Versão do nó para controle de concorrência
        /// </summary>
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        /// <summary>
        /// Identificador da partição para Cosmos DB
        /// </summary>
        [JsonPropertyName("partitionKey")]
        public string PartitionKey { get; set; } = string.Empty;

        /// <summary>
        /// Adiciona uma conexão a outro nó
        /// </summary>
        /// <param name="targetNodeId">ID do nó alvo</param>
        /// <param name="connectionType">Tipo de conexão</param>
        /// <param name="weight">Peso da conexão</param>
        /// <param name="properties">Propriedades adicionais da conexão</param>
        public void AddConnection(string targetNodeId, string connectionType, double weight = 1.0, Dictionary<string, string>? properties = null)
        {
            if (string.IsNullOrEmpty(targetNodeId))
                throw new ArgumentException("ID do nó alvo não pode ser vazio");

            if (string.IsNullOrEmpty(connectionType))
                throw new ArgumentException("Tipo de conexão não pode ser vazio");

            // Verificar se já existe esta conexão
            var existingConnection = Connections.FirstOrDefault(c => 
                c.TargetNodeId == targetNodeId && c.ConnectionType == connectionType);

            if (existingConnection != null)
            {
                // Atualizar conexão existente
                existingConnection.Weight = weight;
                
                if (properties != null)
                {
                    foreach (var prop in properties)
                    {
                        existingConnection.Properties[prop.Key] = prop.Value;
                    }
                }
            }
            else
            {
                // Criar nova conexão
                var connection = new Connection
                {
                    TargetNodeId = targetNodeId,
                    ConnectionType = connectionType,
                    Weight = weight,
                    Properties = properties ?? new Dictionary<string, string>()
                };

                Connections.Add(connection);
            }

            // Atualizar timestamp
            UpdatedAt = DateTime.UtcNow;
            Version++;
        }

        /// <summary>
        /// Remove uma conexão específica
        /// </summary>
        public bool RemoveConnection(string targetNodeId, string connectionType)
        {
            if (string.IsNullOrEmpty(targetNodeId) || string.IsNullOrEmpty(connectionType))
                return false;

            int initialCount = Connections.Count;
            
            Connections.RemoveAll(c => 
                c.TargetNodeId == targetNodeId && c.ConnectionType == connectionType);
            
            bool removed = Connections.Count < initialCount;
            
            if (removed)
            {
                UpdatedAt = DateTime.UtcNow;
                Version++;
            }
            
            return removed;
        }

        /// <summary>
        /// Adiciona ou atualiza uma propriedade do nó
        /// </summary>
        public void SetProperty(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Chave da propriedade não pode ser vazia");

            Properties[key] = value;
            UpdatedAt = DateTime.UtcNow;
            Version++;
        }

        /// <summary>
        /// Remove uma propriedade do nó
        /// </summary>
        public bool RemoveProperty(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            bool removed = Properties.Remove(key);
            
            if (removed)
            {
                UpdatedAt = DateTime.UtcNow;
                Version++;
            }
            
            return removed;
        }
    }

    /// <summary>
    /// Representa uma conexão entre nós no grafo relacional
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// ID do nó alvo da conexão
        /// </summary>
        [JsonPropertyName("targetNodeId")]
        public string TargetNodeId { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de conexão
        /// (ex: 'contains', 'references', 'follows', 'precedes')
        /// </summary>
        [JsonPropertyName("connectionType")]
        public string ConnectionType { get; set; } = string.Empty;

        /// <summary>
        /// Peso da conexão (relevância)
        /// </summary>
        [JsonPropertyName("weight")]
        public double Weight { get; set; } = 1.0;

        /// <summary>
        /// Propriedades adicionais da conexão
        /// </summary>
        [JsonPropertyName("properties")]
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }
}