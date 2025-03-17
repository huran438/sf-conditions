using System;
using SFramework.Core.Runtime;
using UnityEngine;

namespace SFramework.Modules.SFConditions.Runtime
{
    [Serializable]
    public abstract class SFCondition : ISFCondition
    {
        protected SFCondition()
        {
            this.Inject();
        }

        public abstract bool Met();
    }
}