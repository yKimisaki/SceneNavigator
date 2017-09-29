using System;

namespace Tonari.Unity.SceneNavigator
{
    public class NavigationFailureException : Exception
    {
        public NavigationFailureException(string message) : base(message) { }
    }
}
