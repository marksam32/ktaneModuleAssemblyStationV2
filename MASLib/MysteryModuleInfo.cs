using System.Reflection;
using UnityEngine;

namespace MASLib
{
    internal class MysteryModuleInfo
    {
        internal int Index { get; }
        internal GameObject Module { get; }
        internal string ModuleType { get; }
        
        private FieldInfo _solvedField { get; }
        private Component _moduleComponent { get; }

        //Uses reflection to figure out if the module is solved
        internal bool IsSolved => (bool)_solvedField.GetValue(_moduleComponent);

        internal MysteryModuleInfo(int index, GameObject module, FieldInfo solvedField, Component moduleComponent, string moduleType)
        {
            Index = index;
            Module = module;
            _solvedField = solvedField;
            _moduleComponent = moduleComponent;
            ModuleType = moduleType;
        }
    }
}