using Cysharp.Threading.Tasks;

public sealed class UIBlurWindowModule : UIModuleBase, IUIWindowOpenBlocker
{
    public UniTask PrepareBeforeShow()
    {
        return Context.Runtime.Blur.Attach(Context.Window, Context.Window.Config.BlurMode, DestroyToken);
    }

    protected override void OnStop()
    {
        Context.Runtime.Blur.Detach(Context.Window);
    }
}
