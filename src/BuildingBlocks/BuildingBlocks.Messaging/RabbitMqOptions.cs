namespace BuildingBlocks.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public bool UseInMemory { get; set; } = false;
    public string Host { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
