namespace Reel.Server.Application.Models;

public sealed record CreateCollectionRequest(string Name);

public sealed record RenameCollectionRequest(string Name);

public sealed record CollectionItemRequest(int CollectionId, string Path);

public sealed record ReorderItemsRequest(int CollectionId, string[] Paths);
