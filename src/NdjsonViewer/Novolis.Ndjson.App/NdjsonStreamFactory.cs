namespace Novolis.Ndjson.App;

public delegate ValueTask<Stream> NdjsonStreamFactory(CancellationToken cancellationToken);
