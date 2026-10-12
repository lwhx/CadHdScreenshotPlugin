namespace CadHdScreenshot.Plugin
{
	internal sealed class CaptureOutcome
	{
		public string Message { get; }

		private CaptureOutcome(string message)
		{
			Message = message;
		}

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
