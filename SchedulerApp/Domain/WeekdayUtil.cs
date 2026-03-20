namespace SchedulerApp.Domain;

public static class WeekdayUtil
{
    public static string ToChinese(int weekday) => weekday switch
    {
        1 => "周一",
        2 => "周二",
        3 => "周三",
        4 => "周四",
        5 => "周五",
        6 => "周六",
        7 => "周日",
        _ => "未知"
    };
}

