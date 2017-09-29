using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Tonari.Unity.SceneNavigator
{
    public interface INavigatableScene
    {
        SceneArgs SceneArgs { get; set; }

        SceneArgs ParentSceneArgs { get; }
        void SetParentSceneArgs(SceneArgs args);

        Guid? ResultRequirementId { get; set; }

        GameObject RootObject { get; }
        Canvas RootCanvas { get; }
        void SetRootCanvas(Canvas canvas);

        void SetNavigator(Navigator navigator);

        Task ResetAsync(SceneArgs args, TransitionMode mode);

        void Initialize();

        Task EnterAsync(TransitionMode mode);
        Task LeaveAsync(TransitionMode mode);

        void SetLogger(ILogger logger);
    }
}