namespace Reel.Server.Domain;

public sealed record Collection(int Id, string Name, string CreatedAt, int ItemCount);

public sealed record CollectionItem(string RelativePath, int Position, string AddedAt);

public sealed record CollectionWithItems(int Id, string Name, string CreatedAt, CollectionItem[] Items);
