using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SFramework.Modules.SFConditions.Runtime
{
    public class SFConditionConverter : JsonConverter
    {
        private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
        private const string TypeFieldName = "$Type";

        private readonly ILookup<string, Type> _shortNames;
        private readonly ILookup<string, Type> _qualifiedNames;

        public SFConditionConverter()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(IsConditionType)
                .ToArray();
            _shortNames = types.ToLookup(type => type.Name, StringComparer.Ordinal);
            _qualifiedNames = types.ToLookup(QualifiedName, StringComparer.Ordinal);
        }

        public override bool CanConvert(Type objectType) => typeof(ISFCondition).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            var jsonObject = JObject.Load(reader);
            var discriminator = jsonObject[TypeFieldName];
            if (discriminator == null || discriminator.Type != JTokenType.String || string.IsNullOrEmpty((string)discriminator))
                throw new JsonSerializationException($"Expected a non-empty string {TypeFieldName} field.");

            var id = (string)discriminator;
            var candidates = (_qualifiedNames.Contains(id) ? _qualifiedNames[id] : _shortNames[id])
                .Where(objectType.IsAssignableFrom).ToArray();
            if (candidates.Length == 0)
                throw new JsonSerializationException($"Unknown or incompatible condition type '{id}' for {objectType.FullName}.");
            if (candidates.Length > 1)
                throw new JsonSerializationException($"Ambiguous condition type '{id}'. Use its full type name and assembly name.");

            var conditionType = candidates[0];
            object condition;
            try
            {
                condition = Activator.CreateInstance(conditionType);
            }
            catch (Exception error) when (error is MissingMethodException || error is TargetInvocationException ||
                                          error is MemberAccessException)
            {
                throw new JsonSerializationException($"Could not construct condition {conditionType.FullName}.", error);
            }

            var members = GetDataMembers(conditionType);
            foreach (var property in jsonObject.Properties())
            {
                if (property.Name == TypeFieldName) continue;

                // Prefer an exact match, and accept the old CLR name for renamed members.
                var member = members.FirstOrDefault(m => JsonName(m) == property.Name)
                             ?? members.FirstOrDefault(m => m.Name == property.Name)
                             ?? members.FirstOrDefault(m => string.Equals(JsonName(m), property.Name, StringComparison.OrdinalIgnoreCase))
                             ?? members.FirstOrDefault(m => string.Equals(m.Name, property.Name, StringComparison.OrdinalIgnoreCase));
                switch (member)
                {
                    case FieldInfo field:
                        field.SetValue(condition, property.Value.ToObject(field.FieldType, serializer));
                        break;
                    case PropertyInfo info:
                        info.SetValue(condition, property.Value.ToObject(info.PropertyType, serializer));
                        break;
                }
            }

            return condition;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var type = value.GetType();
            var qualifiedName = QualifiedName(type);
            if (_qualifiedNames[qualifiedName].Count() != 1 || !_qualifiedNames[qualifiedName].Contains(type))
                throw new JsonSerializationException($"Condition type '{qualifiedName}' has no unique registration.");

            var id = _shortNames[type.Name].Count() == 1 ? type.Name : qualifiedName;
            var jsonObject = new JObject { new JProperty(TypeFieldName, id) };

            foreach (var member in GetDataMembers(type))
            {
                var name = JsonName(member);
                if (jsonObject.ContainsKey(name))
                    throw new JsonSerializationException($"Duplicate JSON member '{name}' on condition {type.FullName}.");

                var memberValue = member is FieldInfo field ? field.GetValue(value) : ((PropertyInfo)member).GetValue(value);
                jsonObject.Add(name, memberValue == null ? JValue.CreateNull() : JToken.FromObject(memberValue, serializer));
            }

            jsonObject.WriteTo(writer);
        }

        private static MemberInfo[] GetDataMembers(Type type) => type.GetMembers(PublicInstance)
            .Where(member => member switch
            {
                FieldInfo field => !field.IsInitOnly && !field.IsLiteral,
                PropertyInfo property => property.GetIndexParameters().Length == 0 &&
                                         property.GetGetMethod() != null && property.GetSetMethod() != null,
                _ => false
            })
            .Where(member => !member.IsDefined(typeof(JsonIgnoreAttribute), inherit: true))
            .ToArray();

        private static string JsonName(MemberInfo member) =>
            member.GetCustomAttribute<JsonPropertyAttribute>(inherit: true)?.PropertyName ?? member.Name;

        // Simple assembly names keep serialized IDs independent of assembly versions.
        private static string QualifiedName(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";

        private static bool IsConditionType(Type type)
        {
            try
            {
                return type != null && type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters &&
                       typeof(ISFCondition).IsAssignableFrom(type);
            }
            catch (Exception error) when (IsTypeLoadError(error))
            {
                return false;
            }
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException error)
            {
                return error.Types;
            }
            catch (Exception error) when (IsTypeLoadError(error))
            {
                return Array.Empty<Type>();
            }
        }

        private static bool IsTypeLoadError(Exception error) =>
            error is TypeLoadException || error is FileNotFoundException || error is FileLoadException ||
            error is NotSupportedException;
    }
}
