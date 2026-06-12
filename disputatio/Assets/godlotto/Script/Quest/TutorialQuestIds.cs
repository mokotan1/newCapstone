/// <summary>
/// 튜토리얼 퀘스트·단계 id 상수. Fungus 연동·CompleteStep 호출 시 매직 스트링을 피한다.
/// </summary>
public static class TutorialQuestIds
{
    public const string LightTheManor = "light_the_manor";
    public const string BottleKey = "bottle_key";

    public static class LightTheManorSteps
    {
        public const string GoKitchen = "go_kitchen";
        public const string RaiseBreaker = "raise_breaker";
        public const string InspectHall = "inspect_hall";
    }

    public static class BottleKeySteps
    {
        public const string FindBottle = "find_bottle";
        public const string FillBottle = "fill_bottle";
        public const string TakeKey = "take_key";
    }
}
