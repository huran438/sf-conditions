using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace SFramework.Modules.SFConditions.Runtime
{
    public class SFConditionConverter : JsonConverter
    {
        private readonly Dictionary<string, Type> _injectableTypes;

        private const BindingFlags BindingFlags = System.Reflection.BindingFlags.FlattenHierarchy | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;

        private const string TypeFieldName = "$Type";
        
        public SFConditionConverter()
        {
            _injectableTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => !type.IsAbstract && type.IsClass && typeof(ISFCondition).IsAssignableFrom(type))
                .ToDictionary(typeInfo => typeInfo.Name, t => t);
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(ISFCondition).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            var jsonObject = JObject.Load(reader);
            var typeToken = jsonObject[TypeFieldName];

            if (typeToken == null)
            {
                throw new JsonSerializationException($"Missing {TypeFieldName} field.");
            }

            if (!_injectableTypes.TryGetValue(typeToken.ToString(), out var restrictionType))
            {
                throw new JsonSerializationException($"Unknown restriction type: {typeToken} in object {objectType.FullName}");
            }

            var restrictionInstance = Activator.CreateInstance(restrictionType);
            PopulateObject(jsonObject, restrictionInstance, serializer);

            return restrictionInstance;
        }

        private void PopulateObject(JObject jsonObject, object targetObject, JsonSerializer serializer)
        {
            foreach (var property in jsonObject.Properties())
            {
                if (property.Name == TypeFieldName) continue;

                var targetMember = FindMatchingMember(targetObject, property.Name);

                if (targetMember == null)
                {
                    continue;
                }

                var memberType = targetMember is PropertyInfo propertyInfo
                    ? propertyInfo.PropertyType
                    : ((FieldInfo)targetMember).FieldType;

                var deserializedValue = property.Value.ToObject(memberType, serializer);

                if (targetMember is PropertyInfo info)
                {
                    info.SetValue(targetObject, deserializedValue, null);
                }
                else if (targetMember is FieldInfo fieldInfo)
                {
                    fieldInfo.SetValue(targetObject, deserializedValue);
                }
            }
        }

        private MemberInfo FindMatchingMember(object targetObject, string jsonPropertyName)
        {
            var targetType = targetObject.GetType();
            var member = targetType.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => string.Equals(m.Name, jsonPropertyName, StringComparison.OrdinalIgnoreCase));

            return member;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is not ISFCondition) return;


            var type = value.GetType();

            var jObject = new JObject { new JProperty(TypeFieldName, type.Name) };

            var members = type.GetMembers(BindingFlags)
                .Where(m => m.MemberType == MemberTypes.Field);

            foreach (var member in members)
            {
                var memberType = member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;
                var memberValue = member is FieldInfo fieldInfo
                    ? fieldInfo.GetValue(value)
                    : ((PropertyInfo)member).GetValue(value);
                
                if (typeof(ISFCondition).IsAssignableFrom(memberType))
                {
                    var nestedObject = memberValue;
                    jObject.Add(member.Name, nestedObject != null ? JToken.FromObject(nestedObject, serializer) : null);
                }
                else if (memberType.IsArray && memberValue != null &&
                         typeof(ISFCondition).IsAssignableFrom(memberType.GetElementType()))
                {
                    var array = (Array)memberValue;
                    var jArray = new JArray();

                    foreach (var element in array)
                    {
                        jArray.Add(element != null ? JToken.FromObject(element, serializer) : JValue.CreateNull());
                    }

                    jObject.Add(member.Name, jArray);
                }
                else
                {
                    jObject.Add(member.Name,
                        memberValue != null ? JToken.FromObject(memberValue, serializer) : JValue.CreateNull());
                }
            }

            jObject.WriteTo(writer);
        }
    }
}