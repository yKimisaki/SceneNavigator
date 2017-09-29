using Tonari.Unity.SceneNavigator;
using UnityEngine;
using UnityEngine.UI;

namespace Tonari.Unity.NavigationSystemSample
{
    public class CanvasCustomizer : ICanvasCustomizer
    {
        private Camera _camera;

        public CanvasCustomizer(Camera camera)
        {
            this._camera = camera;
        }

        public void Customize(Canvas canvas)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = this._camera;

            var canvasScaler = canvas.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(640, 1136);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0;
            canvasScaler.referencePixelsPerUnit = 100;

            var graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();
            graphicRaycaster.ignoreReversedGraphics = true;
            graphicRaycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        }
    }
}
