using System;
using System.Threading.Tasks;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Tonari.Unity.SceneNavigator
{
    public static class UIExtension
    {
        public static void OnClick(this Button button, SceneSharedParameter sharedParameter, Func<Task> call)
        {
            UnityAction wappedCall = async () =>
            {
                try
                {
                    if (sharedParameter.InputLock)
                    {
                        return;
                    }

                    sharedParameter.InputLock = true;

                    await call();

                    sharedParameter.InputLock = false;
                }
                catch (Exception e)
                {
                    sharedParameter.Logger.LogException(e);
                }
            };

            button.onClick.AddListener(wappedCall);
        }
    }
}
