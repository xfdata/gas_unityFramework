using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class WeekDayRowView : UIModuleBase
{
    private UITextRef _txtDay;
    private UITextRef _txtChildA;
    private UITextRef _txtChildB;
    private UITextRef _txtCoop;
    private UITextRef _txtComment;

    public void Setup(UIViewBinder binder,
        string dayName,
        int childAScore, List<ScoreConfigItem> personalItems,
        int childBScore,
        int cooperationScore, List<ScoreConfigItem> cooperationItems,
        string comment)
    {
        B = binder;

        Cache(ref _txtDay, "TxtDay");
        Cache(ref _txtChildA, "TxtChildA");
        Cache(ref _txtChildB, "TxtChildB");
        Cache(ref _txtCoop, "TxtCoop");
        Cache(ref _txtComment, "TxtComment");

        int personalMax = ScoreUtils.CalcMax(personalItems);
        int coopMax = ScoreUtils.CalcMax(cooperationItems);

        _txtDay.TMPText.text = dayName;
        _txtChildA.TMPText.text = $"老大 {childAScore}/{personalMax}";
        _txtChildB.TMPText.text = $"老二 {childBScore}/{personalMax}";
        _txtCoop.TMPText.text = $"合作 {cooperationScore}/{coopMax}";
        _txtComment.TMPText.text = comment ?? string.Empty;
    }

    protected override void OnStop()
    {
        if (B?.Source != null)
            Object.Destroy(B.Source.gameObject);
    }
}