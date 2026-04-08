namespace Reel.Server.Application.Models;

public sealed record CreateCollectionRequest(string Name);

public sealed record RenameCollectionRequest(string Name);

public sealed record CollectionItemRequest(string Path);

public sealed record ReorderItemsRequest(string[] Paths);
