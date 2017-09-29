using System;
using System.Threading.Tasks;
using Tonari.Unity.SceneNavigator;
using UnityEngine;

namespace Tonari.Unity.NavigationSystemSample
{
    public class TransitionAnimator
    {
        private RuntimeAnimatorController _animator;

        public Task OnNavigatedAsync(INavigatableScene nextScene, INavigatableScene prevScene, TransitionMode mode)
        {
            if (this._animator == null)
            {
                this._animator = Resources.Load<RuntimeAnimatorController>("Animator/NavigationAnimator");
            }

            var nextSceneAnimator = nextScene.RootObject.GetComponent<Animator>();
            if (nextSceneAnimator == null)
            {
                nextSceneAnimator = nextScene.RootObject.AddComponent<Animator>();
            }
            nextSceneAnimator.runtimeAnimatorController = this._animator;

            var prevSceneAnimator = prevScene.RootObject.GetComponent<Animator>();
            if (prevSceneAnimator == null)
            {
                prevSceneAnimator = prevScene.RootObject.AddComponent<Animator>();
            }
            prevSceneAnimator.runtimeAnimatorController = this._animator;

            if (mode.HasFlag(TransitionMode.KeepCurrent))
            {
                if (mode.HasFlag(TransitionMode.New))
                {
                    nextSceneAnimator.Play("TransitionOpen");
                }
                else if (mode.HasFlag(TransitionMode.Back))
                {
                    prevSceneAnimator.Play("TransitionClose");
                }
            }

            return Task.Delay(TimeSpan.FromSeconds(0.3));
        }
    }
}