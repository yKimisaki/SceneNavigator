using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Tonari.Unity.SceneNavigator
{
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public abstract class SceneBase : MonoBehaviour, INavigatableScene
    {
        SceneArgs INavigatableScene.SceneArgs { get; set; }

        public SceneArgs ParentSceneArgs { get; private set; }
        void INavigatableScene.SetParentSceneArgs(SceneArgs args)
        {
            this.ParentSceneArgs = args;
        }

        Guid? INavigatableScene.ResultRequirementId { get; set; }

        GameObject INavigatableScene.RootObject => this.gameObject;
        public virtual Canvas RootCanvas { get; private set; }
        void INavigatableScene.SetRootCanvas(Canvas canvas)
        {
            this.RootCanvas = canvas;
        }

        protected Navigator Navigator { get; private set; }
        void INavigatableScene.SetNavigator(Navigator navigator)
        {
            this.Navigator = navigator;
        }

        public virtual async Task ResetAsync(SceneArgs args, TransitionMode mode) { }

        public abstract void Initialize();

        public virtual async Task EnterAsync(TransitionMode mode) { }
        public virtual async Task LeaveAsync(TransitionMode mode) { }

        protected SceneSharedParameter SceneShared { get; }

        void INavigatableScene.SetLogger(ILogger logger)
        {
            this.SceneShared.Logger = logger;
        }

        protected ILogger Logger => this.SceneShared.Logger;

        public SceneBase()
        {
            this.SceneShared = new SceneSharedParameter();
        }
    }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
}