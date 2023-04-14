using UnityEngine;

namespace MASLib
{
    internal static class AnimationConstants
    {
        //Saves memory apparently or smthn idk
        internal static readonly int TrMoveToPress = Animator.StringToHash("TrMoveToPress");
        internal static readonly int TrDown = Animator.StringToHash("TrDown");
        internal static readonly int TrUp = Animator.StringToHash("TrUp");
        internal static readonly int TrPressDone = Animator.StringToHash("TrPressDone");
        internal static readonly int TrSpin = Animator.StringToHash("TrSpin");
    }
}
