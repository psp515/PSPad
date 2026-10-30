using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;

namespace PSPad.Api.Tests.Commands;

public sealed class StaleReads
{
    Area? _stale;
    int _remaining;

    public void Serve(Area stale, int loads)
    {
        _stale = stale;
        _remaining = loads;
    }

    public Area? Next()
    {
        if (_stale is null || _remaining == 0)
        {
            return null;
        }

        _remaining--;
        return BsonSerializer.Deserialize<Area>(_stale.ToBson());
    }
}
