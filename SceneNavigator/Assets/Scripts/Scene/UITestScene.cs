using Tonari.Unity.SceneNavigator;
using UnityEngine.UI;

namespace Tonari.Unity.NavigationSystemSample
{
    public class UITestSceneArgs : SceneArgs
    {
        public UITestSceneArgs() : base("UITest") { }
    }

    public class UITestScene : SceneBase
    {
        public Button PopupButton;

        public override void Initialize()
        {
            this.PopupButton.OnClick(this.SceneShared, async () =>
            {
                await Navigator.NavigateAsPopupAsync<int>(new UIPopupTestSceneArgs());
            });
        }
    }
}
