using Newtonsoft.Json;
using SFramework.Core.Runtime;
namespace SFramework.Modules.SFConditions.Runtime
{
    [JsonConverter(typeof(SFConditionConverter))]
    public interface ISFCondition : ISFInjectable
    {
        bool Met();
    }
}
