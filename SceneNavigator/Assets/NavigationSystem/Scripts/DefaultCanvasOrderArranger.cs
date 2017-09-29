
namespace Tonari.Unity.SceneNavigator
{
    public class DefaultCanvasOrderArranger : ICanvasOrderArranger
    {
        public int InitialOrder
        {
            get
            {
                return 100;
            }
        }

        public int GetOrder(int parentOrder, NavigationOption option)
        {
            if (option.HasFlag(NavigationOption.Override))
            {
                return parentOrder + 1;
            }

            return parentOrder;
        }
    }
}
