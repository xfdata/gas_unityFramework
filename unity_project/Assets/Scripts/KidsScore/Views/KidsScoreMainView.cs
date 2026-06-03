using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KidsScoreMainView : ViewBase
{
    private AppData _appData;
    private DateTime _currentDate;
    private int _currentPageIndex;
    private GameObject _scoreItemPrefab;
    private GameObject _weekDayRowPrefab;

    private ChildScorePanel _childAPanel;
    private ChildScorePanel _childBPanel;
    private CooperationScorePanel _cooperationPanel;
    private TodaySummaryPanel _summaryPanel;
    private readonly List<WeekDayRowView> _weekDayRows = new();

    private UIObjectRef _dailyScorePage;
    private UIObjectRef _weeklySummaryPage;
    private UIObjectRef _rewardPage;
    private UIButtonRef _btnDaily;
    private UIButtonRef _btnWeek;
    private UIButtonRef _btnReward;
    private UIImageRef _imgDaily;
    private UIImageRef _imgWeek;
    private UIImageRef _imgReward;
    private UITextRef _txtDate;
    private UIButtonRef _btnPrevDay;
    private UIButtonRef _btnToday;
    private UIButtonRef _btnNextDay;
    private UIViewBinder _childABinder;
    private UIViewBinder _childBBinder;
    private UIViewBinder _cooperationBinder;
    private UIViewBinder _summaryBinder;
    private UITextRef _txtWeekRange;
    private UITextRef _txtChildAWeek;
    private UITextRef _txtChildBWeek;
    private UITextRef _txtCoopWeek;
    private UIObjectRef _weekDayList;
    private UITextRef _txtReward;

    private static readonly string[] DayOfWeekNames = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };

    protected override async UniTask OnOpen(object param)
    {
        if (param is KidsScoreOpenParam p)
        {
            _scoreItemPrefab = p.ScoreItemPrefab;
            _weekDayRowPrefab = p.WeekDayRowPrefab;
        }

        _appData = ScoreStorage.Load();
        _currentDate = DateTime.Today;

        CacheBindings();
        BindNavigation();
        BindDateButtons();
        CreateSubViews();
        SwitchPage(0);
    }

    protected override UniTask OnClose(object result)
    {
        ScoreStorage.Save(_appData);
        return UniTask.CompletedTask;
    }

    private void CacheBindings()
    {
        Cache(ref _dailyScorePage, "DailyScorePage");
        Cache(ref _weeklySummaryPage, "WeeklySummaryPage");
        Cache(ref _rewardPage, "RewardPage");
        Cache(ref _btnDaily, "BtnDaily");
        Cache(ref _btnWeek, "BtnWeek");
        Cache(ref _btnReward, "BtnReward");
        Cache(ref _imgDaily, "ImgDaily");
        Cache(ref _imgWeek, "ImgWeek");
        Cache(ref _imgReward, "ImgReward");
        Cache(ref _txtDate, "TxtDate");
        Cache(ref _btnPrevDay, "BtnPrevDay");
        Cache(ref _btnToday, "BtnToday");
        Cache(ref _btnNextDay, "BtnNextDay");
        _childABinder = GetBinder("ChildAPanel");
        _childBBinder = GetBinder("ChildBPanel");
        _cooperationBinder = GetBinder("CooperationPanel");
        _summaryBinder = GetBinder("SummaryPanel");
        Cache(ref _txtWeekRange, "TxtWeekRange");
        Cache(ref _txtChildAWeek, "TxtChildAWeek");
        Cache(ref _txtChildBWeek, "TxtChildBWeek");
        Cache(ref _txtCoopWeek, "TxtCoopWeek");
        Cache(ref _weekDayList, "WeekDayList");
        Cache(ref _txtReward, "TxtReward");
    }

    private void CreateSubViews()
    {
        _childAPanel = AddModule(new ChildScorePanel());
        _childBPanel = AddModule(new ChildScorePanel());
        _cooperationPanel = AddModule(new CooperationScorePanel());
        _summaryPanel = AddModule(new TodaySummaryPanel());
        _summaryPanel.Setup(_summaryBinder);
    }

    #region 底部导航

    private void BindNavigation()
    {
        _btnDaily.Button.onClick.AddListener(() => SwitchPage(0));
        _btnWeek.Button.onClick.AddListener(() => SwitchPage(1));
        _btnReward.Button.onClick.AddListener(() => SwitchPage(2));
    }

    private void SwitchPage(int index)
    {
        _currentPageIndex = index;

        _dailyScorePage.GameObject.SetActive(index == 0);
        _weeklySummaryPage.GameObject.SetActive(index == 1);
        _rewardPage.GameObject.SetActive(index == 2);

        RefreshNavHighlight();

        switch (index)
        {
            case 0: RefreshDailyPage(); break;
            case 1: RefreshWeeklyPage(); break;
            case 2: break;
        }
    }

    private void RefreshNavHighlight()
    {
        Color active = Color.white;
        Color inactive = new Color(0.6f, 0.6f, 0.6f, 1f);

        _imgDaily.Image.color = _currentPageIndex == 0 ? active : inactive;
        _imgWeek.Image.color = _currentPageIndex == 1 ? active : inactive;
        _imgReward.Image.color = _currentPageIndex == 2 ? active : inactive;
    }

    #endregion

    #region 日期导航

    private void BindDateButtons()
    {
        _btnPrevDay.Button.onClick.AddListener(GoToPrevDay);
        _btnToday.Button.onClick.AddListener(GoToToday);
        _btnNextDay.Button.onClick.AddListener(GoToNextDay);
    }

    private void GoToPrevDay()
    {
        _currentDate = _currentDate.AddDays(-1);
        RefreshDailyPage();
    }

    private void GoToToday()
    {
        _currentDate = DateTime.Today;
        RefreshDailyPage();
    }

    private void GoToNextDay()
    {
        _currentDate = _currentDate.AddDays(1);
        RefreshDailyPage();
    }

    private static string GetDateKey(DateTime date) => date.ToString("yyyy-MM-dd");

    private static string GetDateDisplay(DateTime date)
    {
        string dayName = DayOfWeekNames[(int)date.DayOfWeek];
        return $"{date:yyyy-MM-dd}  {dayName}";
    }

    #endregion

    #region 今日积分页

    private void RefreshDailyPage()
    {
        if (_appData == null) return;

        _txtDate.TMPText.text = GetDateDisplay(_currentDate);

        string dateKey = GetDateKey(_currentDate);
        DailyRecord record = _appData.GetOrCreateDailyRecord(dateKey);

        RefreshChildPanel(record, 0, _childAPanel, _childABinder);
        RefreshChildPanel(record, 1, _childBPanel, _childBBinder);
        RefreshCooperationPanel(record);
        RefreshSummaryPanel();
    }

    private void RefreshChildPanel(DailyRecord record, int kidIndex,
        ChildScorePanel panel, UIViewBinder panelBinder)
    {
        if (panel == null || _appData.kids.Count <= kidIndex) return;

        var child = _appData.kids[kidIndex];
        var childRecord = record.GetOrCreateChildRecord(child.id);

        panel.Setup(panelBinder, _scoreItemPrefab,
            child, _appData.personalItems, childRecord.states, OnScoreChanged);
    }

    private void RefreshCooperationPanel(DailyRecord record)
    {
        _cooperationPanel?.Setup(_cooperationBinder, _scoreItemPrefab,
            _appData.cooperationItems, record.cooperationStates, OnScoreChanged);
    }

    private void RefreshSummaryPanel()
    {
        if (_summaryPanel == null || _appData.kids.Count < 2) return;

        int childAScore = _childAPanel?.GetTotalScore() ?? 0;
        int childBScore = _childBPanel?.GetTotalScore() ?? 0;
        int coopScore = _cooperationPanel?.GetTotalScore() ?? 0;

        int personalMax = ScoreUtils.CalcMax(_appData.personalItems);
        int coopMax = ScoreUtils.CalcMax(_appData.cooperationItems);

        _summaryPanel.RefreshUI(
            childAScore, personalMax,
            childBScore, personalMax,
            coopScore, coopMax,
            string.Empty, string.Empty);
    }

    private void OnScoreChanged()
    {
        RefreshSummaryPanel();
        ScoreStorage.Save(_appData);
    }

    #endregion

    #region 本周汇总页

    private void RefreshWeeklyPage()
    {
        if (_appData == null) return;

        DateTime today = DateTime.Today;
        int daysFromMonday = ((int)today.DayOfWeek == 0) ? 6 : (int)today.DayOfWeek - 1;
        DateTime monday = today.AddDays(-daysFromMonday);
        DateTime sunday = monday.AddDays(6);

        _txtWeekRange.TMPText.text = $"本周日期：{monday:MM-dd} ~ {sunday:MM-dd}";

        int childATotal = 0, childBTotal = 0, coopTotal = 0;
        int personalMax = ScoreUtils.CalcMax(_appData.personalItems) * 7;
        int coopMax = ScoreUtils.CalcMax(_appData.cooperationItems) * 7;

        ClearWeekDayRows();

        for (int i = 0; i < 7; i++)
        {
            DateTime day = monday.AddDays(i);
            string dateKey = GetDateKey(day);
            DailyRecord record = _appData.FindDailyRecord(dateKey);

            int aScore = 0, bScore = 0, cScore = 0;

            if (record != null)
            {
                if (_appData.kids.Count > 0)
                    aScore = GetChildScore(record, _appData.kids[0].id);
                if (_appData.kids.Count > 1)
                    bScore = GetChildScore(record, _appData.kids[1].id);
                cScore = ScoreUtils.CalcTotal(record.cooperationStates, _appData.cooperationItems);
            }

            childATotal += aScore;
            childBTotal += bScore;
            coopTotal += cScore;

            string dayName = DayOfWeekNames[(int)day.DayOfWeek];

            if (_weekDayRowPrefab != null)
            {
                var go = GameObject.Instantiate(_weekDayRowPrefab, _weekDayList.Transform);
                var bindBehaviour = go.GetComponent<CSharpUIBindBehaviour>();
                var rowBinder = new UIViewBinder(bindBehaviour);
                var row = new WeekDayRowView();
                AddModule(row);
                row.Setup(rowBinder, dayName,
                    aScore, _appData.personalItems,
                    bScore,
                    cScore, _appData.cooperationItems, "");
                _weekDayRows.Add(row);
            }
        }

        int personalDayMax = ScoreUtils.CalcMax(_appData.personalItems);
        _txtChildAWeek.TMPText.text = $"老大：{childATotal} / {personalMax}分";
        _txtChildBWeek.TMPText.text = $"老二：{childBTotal} / {personalMax}分";
        _txtCoopWeek.TMPText.text = $"合作：{coopTotal} / {coopMax}分";

        RefreshAvailableRewards(childATotal, childBTotal, coopTotal);
    }

    private int GetChildScore(DailyRecord record, string childId)
    {
        var childRecord = record.FindChildRecord(childId);
        if (childRecord == null) return 0;
        return ScoreUtils.CalcTotal(childRecord.states, _appData.personalItems);
    }

    private void ClearWeekDayRows()
    {
        foreach (var row in _weekDayRows)
        {
            if (row is IDisposable d) d.Dispose();
        }
        _weekDayRows.Clear();
    }

    private void RefreshAvailableRewards(int childAScore, int childBScore, int coopScore)
    {
        var rewards = new List<string>();

        string childAReward = GetPersonalReward(childAScore);
        string childBReward = GetPersonalReward(childBScore);
        string coopReward = GetCooperationReward(coopScore);

        if (!string.IsNullOrEmpty(childAReward)) rewards.Add($"老大：{childAReward}");
        if (!string.IsNullOrEmpty(childBReward)) rewards.Add($"老二：{childBReward}");
        if (!string.IsNullOrEmpty(coopReward)) rewards.Add($"合作：{coopReward}");

        _txtReward.TMPText.text = rewards.Count > 0
            ? "本周可兑换：\n" + string.Join("\n", rewards)
            : "本周暂无奖励可兑换";
    }

    private static string GetPersonalReward(int score)
    {
        if (score >= 65) return "特别选择权一次（选晚饭、选动画、选家庭游戏）";
        if (score >= 60) return "周末活动选择权一次（公园、图书馆、户外游戏）";
        if (score >= 50) return "亲子陪伴奖励一次（讲故事、下棋、做手工）";
        if (score >= 40) return "小奖励一次（贴纸、橡皮、小文具）";
        return null;
    }

    private static string GetCooperationReward(int score)
    {
        if (score >= 18) return "周末家庭活动一次（公园、游乐场、烘焙、手工）";
        if (score >= 15) return "家庭电影/动画时间一次";
        if (score >= 10) return "两人一起选一个家庭小游戏";
        return null;
    }

    #endregion

    protected override void OnStop()
    {
        _btnDaily?.Button?.onClick.RemoveAllListeners();
        _btnWeek?.Button?.onClick.RemoveAllListeners();
        _btnReward?.Button?.onClick.RemoveAllListeners();
        _btnPrevDay?.Button?.onClick.RemoveAllListeners();
        _btnToday?.Button?.onClick.RemoveAllListeners();
        _btnNextDay?.Button?.onClick.RemoveAllListeners();
    }
}