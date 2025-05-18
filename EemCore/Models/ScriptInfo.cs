namespace EemCore.Models
{
    /// <summary>
    /// Informações sobre um script GenAIScript
    /// </summary>
    public class ScriptInfo
    {
        /// <summary>
        /// Identificador único do script
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Nome do script
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Descrição do script
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp de criação do script
        /// </summary>
        public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp da última modificação do script
        /// </summary>
        public DateTime ModifiedDateTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Indica se o script está ativo
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Tags para o script
        /// </summary>
        public List<string> Tags { get; set; } = new();
    }
}