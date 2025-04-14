using UnityEngine;

namespace Unity.Behavior
{
    internal interface IRepeater
    {
        public bool AllowMultipleRepeatsPerTick { get; set; }
    }
}
