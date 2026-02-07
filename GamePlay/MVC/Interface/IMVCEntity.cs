using System.Threading;
using Cysharp.Threading.Tasks;

namespace Vant.MVC
{
    /// <summary>
    /// MVC Entity interface for unified lifecycle management.
    /// </summary>
    public interface IMVCEntity
    {
        UniTask Reset(object controller, AbstractGeneralViewBase view, object viewModel, bool showOnInit, CancellationToken token = default);

        void Destroy();
    }
}
