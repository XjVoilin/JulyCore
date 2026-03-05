using JulyCore.Core;

/// <summary>
/// 语言包切换事件
/// </summary>
public class LanguageChangedEvent : IEvent
{
    public string OldLanguageCode { get; set; }
    public string CurLanguageCode { get; set; }

    public LanguageChangedEvent(string oldLanguageCode, string curLanguageCode)
    {
        OldLanguageCode = oldLanguageCode;
        CurLanguageCode = curLanguageCode;
    }
}