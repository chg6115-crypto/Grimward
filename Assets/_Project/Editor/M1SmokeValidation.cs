namespace Grimward.Editor
{
    // M1 검증 진입점을 유지하되 현재 M2 동작을 검증한다.
    public static class M1SmokeValidation
    {
        public static void Run() => M2SmokeValidation.Run();
    }
}