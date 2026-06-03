using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class ScoreStorage
{
    private static string _currentAccount;

    public static string CurrentAccount
    {
        get => _currentAccount ?? string.Empty;
        set => _currentAccount = SanitizeAccount(value);
    }

    private static string FilePath => GetAccountFilePath(CurrentAccount);

    private static string GetAccountFilePath(string account)
    {
        var fileName = string.IsNullOrEmpty(account)
            ? "kids_score_data.json"
            : $"kids_score_data_{account}.json";
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    private static string SanitizeAccount(string account)
    {
        if (string.IsNullOrEmpty(account))
            return string.Empty;

        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = account.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalidChars, chars[i]) >= 0)
                chars[i] = '_';
        }
        return new string(chars);
    }

    public static AppData Load()
    {
        var filePath = FilePath;

        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                AppData data = JsonUtility.FromJson<AppData>(json);

                if (data != null)
                {
                    Normalize(data);
                    return data;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("读取积分数据失败：" + e);
            }
        }

        AppData defaultData = CreateDefaultData();
        Save(defaultData);
        return defaultData;
    }

    public static void Save(AppData data)
    {
        if (data == null)
            return;

        Normalize(data);

        var filePath = FilePath;

        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(filePath, json, Encoding.UTF8);
            Debug.Log("积分数据已保存：" + filePath);
        }
        catch (Exception e)
        {
            Debug.LogError("保存积分数据失败：" + e);
        }
    }

    public static void DeleteSave()
    {
        var filePath = FilePath;
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    public static List<string> GetAllAccounts()
    {
        var accounts = new List<string>();
        var dir = Application.persistentDataPath;

        if (!Directory.Exists(dir))
            return accounts;

        const string prefix = "kids_score_data_";
        const string suffix = ".json";

        foreach (var filePath in Directory.GetFiles(dir, $"{prefix}*{suffix}"))
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (fileName.StartsWith(prefix) && fileName.Length > prefix.Length)
            {
                var account = fileName.Substring(prefix.Length);
                if (!string.IsNullOrEmpty(account))
                    accounts.Add(account);
            }
        }

        return accounts;
    }

    public static bool AccountExists(string account)
    {
        if (string.IsNullOrEmpty(account))
            return false;

        var sanitized = SanitizeAccount(account);
        return File.Exists(GetAccountFilePath(sanitized));
    }

    public static void SeedTestData()
    {
        var data = CreateDefaultData();
        var today = DateTime.Today;

        var kid1 = data.kids[0].id;
        var kid2 = data.kids[1].id;

        var personalIds = new[]
        {
            "start_homework", "focus_15min", "finish_homework", "self_check",
            "fix_wrong", "reading", "pack_bag", "good_mood"
        };

        var coopIds = new[] { "no_disturb", "no_laugh", "clean_together" };

        var kid1Scores = new[]
        {
            new[] { 1, 2, 1, 1, 1, 0, 1, 1 },
            new[] { 1, 2, 1, 1, 1, 1, 0, 1 },
            new[] { 1, 1, 1, 1, 0, 1, 1, 1 },
            new[] { 1, 2, 1, 1, 1, 1, 1, 1 },
            new[] { 1, 2, 1, 0, 1, 1, 1, 1 },
            new[] { 1, 1, 1, 1, 0, 0, 1, 1 },
            new[] { 1, 1, 0, 0, 1, 1, 0, 1 },
        };

        var kid2Scores = new[]
        {
            new[] { 1, 1, 1, 0, 1, 1, 0, 1 },
            new[] { 1, 2, 1, 0, 1, 0, 1, 1 },
            new[] { 1, 1, 0, 0, 1, 1, 0, 1 },
            new[] { 1, 2, 1, 1, 0, 1, 1, 1 },
            new[] { 1, 1, 0, 1, 1, 0, 1, 1 },
            new[] { 1, 1, 0, 0, 1, 1, 0, 1 },
            new[] { 0, 1, 0, 0, 1, 1, 0, 1 },
        };

        var coopScores = new[]
        {
            new[] { 1, 1, 0 },
            new[] { 1, 1, 1 },
            new[] { 1, 0, 1 },
            new[] { 1, 1, 1 },
            new[] { 1, 1, 0 },
            new[] { 1, 0, 0 },
            new[] { 0, 1, 0 },
        };

        for (int day = 0; day < 7; day++)
        {
            var date = today.AddDays(-6 + day);
            var dateKey = date.ToString("yyyy-MM-dd");
            var record = data.GetOrCreateDailyRecord(dateKey);

            FillChildRecord(record, kid1, personalIds, kid1Scores[day]);
            FillChildRecord(record, kid2, personalIds, kid2Scores[day]);
            FillCoopStates(record, coopIds, coopScores[day]);
        }

        Save(data);
        Debug.Log($"[ScoreStorage] 测试数据已生成，共 7 天记录");
    }

    private static void FillChildRecord(DailyRecord record, string childId,
        string[] itemIds, int[] counts)
    {
        var childRecord = record.GetOrCreateChildRecord(childId);
        for (int i = 0; i < itemIds.Length; i++)
        {
            if (counts[i] > 0)
            {
                var state = childRecord.GetOrCreateState(itemIds[i]);
                state.count = counts[i];
            }
        }
    }

    private static void FillCoopStates(DailyRecord record,
        string[] itemIds, int[] counts)
    {
        for (int i = 0; i < itemIds.Length; i++)
        {
            if (counts[i] > 0)
            {
                var state = record.GetOrCreateCooperationState(itemIds[i]);
                state.count = counts[i];
            }
        }
    }

    private static void Normalize(AppData data)
    {
        if (data.kids == null)
            data.kids = new List<ChildData>();

        if (data.personalItems == null)
            data.personalItems = new List<ScoreConfigItem>();

        if (data.cooperationItems == null)
            data.cooperationItems = new List<ScoreConfigItem>();

        if (data.dailyRecords == null)
            data.dailyRecords = new List<DailyRecord>();

        if (data.kids.Count == 0)
        {
            data.kids.Add(new ChildData("kid_1", "老大"));
            data.kids.Add(new ChildData("kid_2", "老二"));
        }

        if (data.personalItems.Count == 0)
        {
            data.personalItems.Add(new ScoreConfigItem("start_homework", "按时开始写作业", 1, 1));
            data.personalItems.Add(new ScoreConfigItem("focus_15min", "专注15分钟", 1, 2));
            data.personalItems.Add(new ScoreConfigItem("finish_homework", "作业全部完成，不用催", 2, 1));
            data.personalItems.Add(new ScoreConfigItem("self_check", "写完后自己检查", 1, 1));
            data.personalItems.Add(new ScoreConfigItem("fix_wrong", "错题主动订正", 1, 1));
            data.personalItems.Add(new ScoreConfigItem("reading", "阅读/朗读15分钟", 1, 1));
            data.personalItems.Add(new ScoreConfigItem("pack_bag", "整理书包和文具", 1, 1));
            data.personalItems.Add(new ScoreConfigItem("good_mood", "情绪稳定，有问题好好说", 1, 1));
        }

        if (data.cooperationItems.Count == 0)
        {
            data.cooperationItems.Add(new ScoreConfigItem("no_disturb", "写作业期间互不打扰", 1, 1));
            data.cooperationItems.Add(new ScoreConfigItem("no_laugh", "不嘲笑、不捣乱", 1, 1));
            data.cooperationItems.Add(new ScoreConfigItem("clean_together", "两人一起收拾桌面", 1, 1));
        }
    }

    private static AppData CreateDefaultData()
    {
        AppData data = new AppData();
        Normalize(data);
        return data;
    }
}