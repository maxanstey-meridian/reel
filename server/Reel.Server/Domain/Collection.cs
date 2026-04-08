namespace Reel.Server.Domain;

public sealed record Collection(int Id, string Name, DateTime CreatedAt, int ItemCount);

public sealed record CollectionItem(string RelativePath, int Position, DateTime AddedAt);

public sealed record CollectionWithItems(int Id, string Name, DateTime CreatedAt, CollectionItem[] Items);
