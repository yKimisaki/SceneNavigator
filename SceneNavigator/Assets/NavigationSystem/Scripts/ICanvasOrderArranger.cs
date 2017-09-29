
namespace Tonari.Unity.SceneNavigator
{
    public interface ICanvasOrderArranger
    {
        int InitialOrder { get; }
        int GetOrder(int parentOrder, NavigationOption option);
    }
}
