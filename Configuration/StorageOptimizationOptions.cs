namespace AldaJoyeros.Configuration;

// Switching this off restores the previous queries without changing stored data.
public sealed class StorageOptimizationOptions
{
    public bool Enabled { get; set; } = true;
}
