using Cysharp.Threading.Tasks;

public interface IUIWindowOpenBlocker
{
    UniTask PrepareBeforeShow();
}