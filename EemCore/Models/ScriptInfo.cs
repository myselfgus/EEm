namespace EemCore.Models
{
    /// <summary>
    /// Informa��es sobre um script GenAIScript
    /// </summary>
    public class ScriptInfo
    {
        /// <summary>
        /// Identificador �nico do script
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Nome do script
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Descri��o do script
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp de cria��o do script
        /// </summary>
        public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp da �ltima modifica��o do script
        /// </summary>
        public DateTime ModifiedDateTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Indica se o script est� ativo
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Tags para o script
        /// </summary>
        public List<string> Tags { get; set; } = new();
    }
}