namespace Reel.Server.Application.Models;

public sealed record CreateCollectionRequest(string Name);

public sealed record RenameCollectionRequest(int Id, string Name);

public sealed record CollectionItemRequest(int Id, string Path);

public sealed record ReorderItemsRequest(int Id, string[] Paths);
