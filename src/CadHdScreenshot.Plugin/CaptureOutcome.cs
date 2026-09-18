namespace CadHdScreenshot.Plugin
{
    internal sealed class CaptureOutcome
    {
        private CaptureOutcome(string message)
        {
            Message = message;
        }

        public string Message { get; }

        public static CaptureOutcome Cancelled()
        {
            return new CaptureOutcome("已取消");
        }

        public static CaptureOutcome Completed(string message)
        {
            return new CaptureOutcome(message);
        }
    }
}
