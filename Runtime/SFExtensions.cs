using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SFramework.Modules.SFConditions.Runtime
{
    public static partial class SFExtensions
    {
        public static bool AllMet(this ISFCondition[] conditions, bool editorAlwaysTrue)
        {
            if (editorAlwaysTrue && Application.isEditor) return true;
            return AllMet(conditions);
        }

        public static bool AnyMet(this ISFCondition[] conditions, bool editorAlwaysTrue)
        {
            if (editorAlwaysTrue && Application.isEditor) return true;
            return AnyMet(conditions);
        }

        public static bool AllMet(this ISFCondition[] conditions)
        {
            if (conditions == null)
            {
                return true;
            }

            var result = true;
            foreach (var condition in conditions)
            {
                if (condition.Met()) continue;
                result = false;
                break;
            }

            return result;
        }

        public static bool AnyMet(this ISFCondition[] conditions)
        {
            if (conditions == null)
            {
                return true;
            }

            var result = false;

            for (var i = 0; i < conditions.Length; i++)
            {
                var condition = conditions[i];
                if (!condition.Met()) continue;
                result = true;
                break;
            }

            return result;
        }

        public static bool IsEmpty(this IEnumerable<ISFCondition> conditions)
        {
            return conditions == null || !conditions.Any();
        }
    }
}