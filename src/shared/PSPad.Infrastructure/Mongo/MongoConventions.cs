using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using PSPad.Abstractions;

namespace PSPad.Infrastructure.Mongo;

public static class MongoConventions
{
    static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        BsonSerializer.RegisterSerializer(new DateOnlySerializer());

        ConventionRegistry.Register(
            "pspad",
            new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
                new MapIdConvention()
            },
            type => typeof(Aggregate).IsAssignableFrom(type) || type.Namespace?.StartsWith("PSPad") == true);
    }

    sealed class MapIdConvention : ConventionBase, IClassMapConvention
    {
        public void Apply(BsonClassMap classMap)
        {
            var id = classMap.AllMemberMaps.FirstOrDefault(member => member.MemberName == "Id");
            if (id is not null)
            {
                classMap.SetIdMember(id);
            }
        }
    }
}
