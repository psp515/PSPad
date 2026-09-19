using System.Reflection;
using System.Text.Json.Serialization;
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
                new MapIdConvention(),
                new MapJsonIncludeFieldsConvention()
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

    // BSON automap only sees public members, so a private field an aggregate opts into its own
    // System.Text.Json replica serialization via [JsonInclude] (e.g. Inbox._items) is otherwise
    // invisible here: it round-trips fine client-side but silently comes back empty from Mongo.
    sealed class MapJsonIncludeFieldsConvention : ConventionBase, IClassMapConvention
    {
        public void Apply(BsonClassMap classMap)
        {
            foreach (var field in classMap.ClassType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.GetCustomAttribute<JsonIncludeAttribute>() is not null &&
                    classMap.AllMemberMaps.All(member => member.MemberInfo != field))
                {
                    classMap.MapField(field.Name);
                }
            }
        }
    }
}
