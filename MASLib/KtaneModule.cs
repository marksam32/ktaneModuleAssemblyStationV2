using UnityEngine;

namespace MASLib
{
    internal class KtaneModule
    {
        internal ModuleType Type { get; }
        internal GameObject GameObject { get; }
        internal string Name { get; }

        internal bool MysModHidden { get; set; }
        internal int? MysModIndex { get; }

        internal KtaneModule(ModuleType type, GameObject gameObject, string name, bool mysModHidden = false, int? mysModIndex = null)
        {
            Type = type;
            GameObject = gameObject;
            Name = name;
            MysModHidden = mysModHidden;
            MysModIndex = mysModIndex;
        }
    }
}
