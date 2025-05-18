using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace EemCore.Models
{
    /// <summary>
    /// Representa um evento de atividade do usuário (.aje)
    /// </summary>
    public class ActivityJournalEvent
    {
        /// <summary>
        /// Identificador único do evento
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Timestamp de quando o evento ocorreu
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tipo de atividade (edição, navegação, consulta, etc.)
        /// </summary>
        [JsonPropertyName("activityType")]
        public string ActivityType { get; set; } = string.Empty;

        /// <summary>
        /// Conteúdo principal do evento
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Fonte da atividade (IDE, Assistente AI, etc.)
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// ID da sessão à qual o evento pertence
        /// </summary>
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Metadados adicionais do evento
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Caminho do arquivo associado (se aplicável)
        /// </summary>
        [JsonPropertyName("associatedFile")]
        public string? AssociatedFile { get; set; }

        /// <summary>
        /// Hash de conteúdo para detecção de duplicação
        /// </summary>
        [JsonPropertyName("contentHash")]
        public string? ContentHash { get; set; }

        /// <summary>
        /// Nome específico do arquivo .aje para armazenamento
        /// </summary>
        [JsonIgnore]
        public string FileName => $"{SessionId}/{Timestamp:yyyyMMddHHmmss}_{Id}.aje";

        /// <summary>
        /// Cria um hash simplificado para detecção de duplicação de conteúdo
        /// </summary>
        public void GenerateContentHash()
        {
            if (string.IsNullOrEmpty(Content))
            {
                ContentHash = null;
                return;
            }

            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(Content);
            var hashBytes = sha.ComputeHash(bytes);
            ContentHash = Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Adiciona um metadado ao evento
        /// </summary>
        public void AddMetadata(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("A chave do metadado não pode ser vazia");

            Metadata[key] = value;
        }
    }
}