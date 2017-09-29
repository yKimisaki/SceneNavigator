using System;

namespace Tonari.Unity.SceneNavigator
{
    public abstract class SceneArgs
    {
        public string SceneName { get; }

        protected SceneArgs(string sceneName)
        {
            this.SceneName = sceneName;
        }
    }
}
