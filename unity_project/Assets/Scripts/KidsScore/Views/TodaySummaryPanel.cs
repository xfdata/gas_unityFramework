using TMPro;
using UnityEngine;

public class TodaySummaryPanel : UIModuleBase
{
    private UITextRef _txtChildAScore;
    private UITextRef _txtChildBScore;
    private UITextRef _txtCoopScore;
    private TMP_InputField _inputComment;
    private TMP_InputField _inputGoal;

    public string TodayComment => _inputComment?.text ?? string.Empty;
    public string TomorrowGoal => _inputGoal?.text ?? string.Empty;

    public void Setup(UIViewBinder binder)
    {
        B = binder;

        Cache(ref _txtChildAScore, "TxtChildAScore");
        Cache(ref _txtChildBScore, "TxtChildBScore");
        Cache(ref _txtCoopScore, "TxtCoopScore");
        Cache(ref _inputComment, "InputComment");
        Cache(ref _inputGoal, "InputGoal");
    }

    public void RefreshUI(
        int childAScore, int childAMax,
        int childBScore, int childBMax,
        int cooperationScore, int cooperationMax,
        string comment, string goal)
    {
        if (B == null) return;

        _txtChildAScore.TMPText.text = $"老大：{childAScore} / {childAMax}分";
        _txtChildBScore.TMPText.text = $"老二：{childBScore} / {childBMax}分";
        _txtCoopScore.TMPText.text = $"合作：{cooperationScore} / {cooperationMax}分";

        _inputComment.text = comment ?? string.Empty;
        _inputGoal.text = goal ?? string.Empty;
    }
}