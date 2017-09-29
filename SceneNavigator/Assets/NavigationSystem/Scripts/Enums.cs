using System;

namespace Tonari.Unity.SceneNavigator
{
    [Flags]
    public enum NavigationOption
    {
        None = 0,

        Push = 1 << 1,
        Pop = 1 << 2,

        Override = 1 << 31,
        
        Popup = Push | Override,
    }

    [Flags]
    public enum TransitionMode
    {
        None = 0,

        New = 1 << 1,
        KeepCurrent = 1 << 2,
        Back = 1 << 3,
    }
}
