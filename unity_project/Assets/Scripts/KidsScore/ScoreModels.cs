using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class AppData
{
    public int version = 1;

    public List<ChildData> kids = new List<ChildData>();
    public List<ScoreConfigItem> personalItems = new List<ScoreConfigItem>();
    public List<ScoreConfigItem> cooperationItems = new List<ScoreConfigItem>();
    public List<DailyRecord> dailyRecords = new List<DailyRecord>();

    public DailyRecord GetOrCreateDailyRecord(string dateKey)
    {
        if (dailyRecords == null)
            dailyRecords = new List<DailyRecord>();

        DailyRecord record = dailyRecords.FirstOrDefault(x => x.dateKey == dateKey);
        if (record == null)
        {
            record = new DailyRecord();
            record.dateKey = dateKey;
            dailyRecords.Add(record);
        }

        return record;
    }

    public DailyRecord FindDailyRecord(string dateKey)
    {
        if (dailyRecords == null)
            return null;

        return dailyRecords.FirstOrDefault(x => x.dateKey == dateKey);
    }
}

[Serializable]
public class ChildData
{
    public string id;
    public string name;

    public ChildData() { }

    public ChildData(string id, string name)
    {
        this.id = id;
        this.name = name;
    }
}

[Serializable]
public class ScoreConfigItem
{
    public string id;
    public string title;

    // 每完成一次获得几分
    public int scorePerCount = 1;

    // 每天最多完成几次
    public int maxCount = 1;

    public ScoreConfigItem() { }

    public ScoreConfigItem(string id, string title, int scorePerCount, int maxCount)
    {
        this.id = id;
        this.title = title;
        this.scorePerCount = scorePerCount;
        this.maxCount = maxCount;
    }
}

[Serializable]
public class DailyRecord
{
    public string dateKey;

    public List<ChildDailyRecord> childRecords = new List<ChildDailyRecord>();
    public List<ScoreState> cooperationStates = new List<ScoreState>();

    public ChildDailyRecord GetOrCreateChildRecord(string childId)
    {
        if (childRecords == null)
            childRecords = new List<ChildDailyRecord>();

        ChildDailyRecord record = childRecords.FirstOrDefault(x => x.childId == childId);
        if (record == null)
        {
            record = new ChildDailyRecord();
            record.childId = childId;
            childRecords.Add(record);
        }

        return record;
    }

    public ChildDailyRecord FindChildRecord(string childId)
    {
        if (childRecords == null)
            return null;

        return childRecords.FirstOrDefault(x => x.childId == childId);
    }

    public ScoreState GetOrCreateCooperationState(string itemId)
    {
        if (cooperationStates == null)
            cooperationStates = new List<ScoreState>();

        ScoreState state = cooperationStates.FirstOrDefault(x => x.itemId == itemId);
        if (state == null)
        {
            state = new ScoreState();
            state.itemId = itemId;
            state.count = 0;
            cooperationStates.Add(state);
        }

        return state;
    }
}

[Serializable]
public class ChildDailyRecord
{
    public string childId;
    public List<ScoreState> states = new List<ScoreState>();

    public ScoreState GetOrCreateState(string itemId)
    {
        if (states == null)
            states = new List<ScoreState>();

        ScoreState state = states.FirstOrDefault(x => x.itemId == itemId);
        if (state == null)
        {
            state = new ScoreState();
            state.itemId = itemId;
            state.count = 0;
            states.Add(state);
        }

        return state;
    }
}

[Serializable]
public class ScoreState
{
    public string itemId;
    public int count;
}

public static class ScoreUtils
{
    public static int CalcTotal(List<ScoreState> states, List<ScoreConfigItem> items)
    {
        if (states == null || items == null)
            return 0;

        int total = 0;

        foreach (ScoreConfigItem item in items)
        {
            ScoreState state = states.FirstOrDefault(x => x.itemId == item.id);
            int count = state == null ? 0 : state.count;

            count = Clamp(count, 0, Math.Max(1, item.maxCount));
            total += count * item.scorePerCount;
        }

        return total;
    }

    public static int CalcMax(List<ScoreConfigItem> items)
    {
        if (items == null)
            return 0;

        int total = 0;
        foreach (ScoreConfigItem item in items)
        {
            total += Math.Max(1, item.maxCount) * item.scorePerCount;
        }

        return total;
    }

    public static int Clamp(int value, int min, int max)
    {
        if (value < min)
            return min;

        if (value > max)
            return max;

        return value;
    }
}