using Cysharp.Threading.Tasks;

public sealed class UIMaskWindowModule : UIModuleBase, IUIWindowOpenBlocker
{
    public UniTask PrepareBeforeShow()
    {
        Context.Runtime.Mask.Show(Context.Window, Context.Window.Config.MaskMode);
        return UniTask.CompletedTask;
    }

    protected override void OnStop()
    {
        Context.Runtime.Mask.Hide(Context.Window);
    }
}