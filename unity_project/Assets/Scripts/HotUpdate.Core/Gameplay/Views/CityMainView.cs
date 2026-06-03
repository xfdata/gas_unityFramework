using Cysharp.Threading.Tasks;

public class CityMainView : ViewBase
{
    protected override async UniTask OnOpen(object param)
    {
        await Context.Runtime.Open<KidsScoreMainView>();
    }
}